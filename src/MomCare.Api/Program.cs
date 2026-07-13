using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MomCare.Api.Hubs;
using MomCare.Api.Realtime;
using MomCare.Application;
using MomCare.Data;
using MomCare.Infrastructure;
using MomCare.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading.RateLimiting;

var envCandidates = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), "src", "MomCare.Api", ".env"),
    Path.Combine(AppContext.BaseDirectory, ".env")
};

var envPath = envCandidates.FirstOrDefault(File.Exists);
Dictionary<string, string?>? envOverrides = null;
if (envPath is not null)
{
    Env.Load(envPath);
    envOverrides = LoadEnvOverrides(envPath);
}

var builder = WebApplication.CreateBuilder(args);
if (envOverrides is not null)
{
    builder.Configuration.AddInMemoryCollection(envOverrides);
}
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddHttpClient();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

builder.Services.AddCors(options =>
{
    var configuredOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
        ?? [];

    var envOrigins = (Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var allowedOrigins = configuredOrigins
        .Concat(envOrigins)
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (allowedOrigins.Length == 0 && builder.Environment.IsDevelopment())
    {
        allowedOrigins = ["http://localhost:3000", "http://localhost:5173"];
    }

    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", context => FixedWindow(context, 5, TimeSpan.FromMinutes(5)));
    options.AddPolicy("signup", context => FixedWindow(context, 30, TimeSpan.FromMinutes(5)));
    options.AddPolicy("refresh-token", context => FixedWindow(context, 20, TimeSpan.FromMinutes(5)));
    options.AddPolicy("upload", context => FixedWindow(context, 5, TimeSpan.FromMinutes(10)));
    options.AddPolicy("booking", context => FixedWindow(context, 10, TimeSpan.FromMinutes(1)));
    options.AddPolicy("health-checkin", context => FixedWindow(context, 10, TimeSpan.FromMinutes(5)));
    options.AddPolicy("chat", context => FixedWindow(context, 30, TimeSpan.FromMinutes(1)));
    options.AddPolicy("community", context => FixedWindow(context, 20, TimeSpan.FromMinutes(1)));
    options.AddPolicy("payment", context => FixedWindow(context, 5, TimeSpan.FromMinutes(1)));
});

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();
var requestLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MomCare.Requests");

var shouldSeedData = app.Configuration.GetValue<bool>("SeedData:Enabled");
if (shouldSeedData)
{
    using var scope = app.Services.CreateScope();
    await MomCareSeedData.SeedAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
    app.Use(async (context, next) =>
    {
        context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
        context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
        context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        context.Response.Headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");
        await next();
    });
}
else
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("AllowReactApp");

app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        await next();
    }
    finally
    {
        stopwatch.Stop();
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        requestLogger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms for user {UserId}",
            context.Request.Method,
            context.Request.Path.Value,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            userId ?? "anonymous");
    }
});

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    await next();

    var method = context.Request.Method;
    if (!context.Request.Path.StartsWithSegments("/api/admin") ||
        method is not ("POST" or "PUT" or "PATCH" or "DELETE"))
    {
        return;
    }

    try
    {
        var db = context.RequestServices.GetRequiredService<MomCareContext>();
        var actorUserIdRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        int? actorUserId = int.TryParse(actorUserIdRaw, out var parsedUserId) ? parsedUserId : null;
        var actorName = context.User.FindFirstValue(ClaimTypes.Name)
            ?? context.User.FindFirstValue(ClaimTypes.Email)
            ?? context.User.Identity?.Name;

        db.AuditLogs.Add(new MomCare.Models.AuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            ActorName = actorName,
            Method = method,
            Path = context.Request.Path.Value ?? string.Empty,
            QueryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
            StatusCode = context.Response.StatusCode,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
    catch
    {
        // Audit logging must not break the admin action response.
    }
});

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

static RateLimitPartition<string> FixedWindow(HttpContext context, int permitLimit, TimeSpan window)
{
    return RateLimitPartition.GetFixedWindowLimiter(
        GetRateLimitKey(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0,
            AutoReplenishment = true
        });
}

static string GetRateLimitKey(HttpContext context)
{
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? context.User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

    if (!string.IsNullOrWhiteSpace(userId))
    {
        return $"user:{userId}";
    }

    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    var clientIp = forwardedFor?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    return $"ip:{clientIp}";
}

static Dictionary<string, string?> LoadEnvOverrides(string envPath)
{
    var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    foreach (var rawLine in File.ReadAllLines(envPath))
    {
        var line = rawLine.Trim();
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim().Trim('"');

        values[key] = value;
        Environment.SetEnvironmentVariable(key, value);
    }

    return values;
}
