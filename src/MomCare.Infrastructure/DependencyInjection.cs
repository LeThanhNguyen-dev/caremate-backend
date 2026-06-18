using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using MomCare.Data;
using MomCare.Infrastructure.Configurations;
using MomCare.Interfaces;
using MomCare.Repositories;
using MomCare.Services;

namespace MomCare.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PayOSOptions>(configuration.GetSection(PayOSOptions.SectionName));
        services.Configure<FptAiOptions>(configuration.GetSection(FptAiOptions.SectionName));
        services.Configure<GroqOptions>(configuration.GetSection(GroqOptions.SectionName));
        services.AddMemoryCache();

        var urgentConfig = configuration.GetSection("SafetyGuardrails:UrgentKeywords").Get<string[]>();
        var watchConfig = configuration.GetSection("SafetyGuardrails:WatchKeywords").Get<string[]>();
        SafetyGuardrailEngine.Initialize(urgentConfig, watchConfig);

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<INurseService, NurseService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IAdminAiInsightService, AdminAiInsightService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<INurseDiscoveryService, NurseDiscoveryService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationQueryService, NotificationQueryService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ICommunityService, CommunityService>();
        services.AddScoped<IDisputeService, DisputeService>();
        services.AddScoped<INurseServiceManagementService, NurseServiceManagementService>();
        services.AddScoped<ICloudinaryService, CloudinaryService>();
        services.AddScoped<IHealthCheckInService, HealthCheckInService>();
        services.AddScoped<IPackageSessionService, PackageSessionService>();
        services.AddScoped<ICarePlanService, CarePlanService>();
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddScoped<SymptomTagEngine>();
        services.AddScoped<ServiceMatcher>();
        services.AddScoped<GeminiPromptBuilder>();
        services.AddScoped<GeminiCallLogService>();
        services.AddScoped<GeminiReasoningService>();
        services.AddScoped<PlanValidatorEngine>();
        services.AddScoped<UrgentResponseBuilder>();
        services.AddHttpClient<ICccdOcrService, FptAiCccdOcrService>();
        services.AddHttpClient<ILlmService, GroqService>()
            .AddStandardResilienceHandler()
            .Configure(o =>
            {
                o.Retry.MaxRetryAttempts = 3;
                o.Retry.Delay = TimeSpan.FromSeconds(2);
                o.Retry.MaxDelay = TimeSpan.FromSeconds(15);
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(35);
            });

        services.AddDbContext<MomCareContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddIdentityCore<MomCare.Models.ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
        })
        .AddRoles<MomCare.Models.ApplicationRole>()
        .AddEntityFrameworkStores<MomCareContext>()
        .AddDefaultTokenProviders();

        return services;
    }
}
