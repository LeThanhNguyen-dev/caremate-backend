using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MomCare.Data;
using MomCare.Models;
using MomCare.Dto;
using MomCare.Infrastructure.Configurations;
using MomCare.Interfaces;

namespace MomCare.Services;

public class GeminiService : IGeminiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IServiceProvider _serviceProvider;

    public GeminiService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        IConfiguration configuration,
        ILogger<GeminiService> logger,
        IMemoryCache cache,
        IServiceProvider serviceProvider)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
        _serviceProvider = serviceProvider;
    }

    public async Task<GeminiGenerateResponse> GenerateAsync(GeminiGenerateRequest request, CancellationToken cancellationToken)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Gemini API key is not configured.");
        }

        // Cache lookup
        var cacheKey = $"gemini_response:{ComputeCacheKey(request)}";
        if (!request.BypassCache && _cache.TryGetValue<GeminiGenerateResponse>(cacheKey, out var cachedResponse) && cachedResponse != null)
        {
            _logger.LogInformation("Returning cached Gemini response");
            return cachedResponse;
        }

        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gemini-2.0-flash" : _options.Model.Trim();
        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/v1beta/models/{NormalizeModelName(model)}:generateContent";

        // Setup custom timeout
        var timeoutSeconds = request.TimeoutSeconds ?? _options.DefaultTimeoutSeconds;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var stopwatch = Stopwatch.StartNew();
        var success = false;
        string? errorMessage = null;
        var outputTokens = 0;
        var inputPrompt = request.Prompt ?? "";
        if (request.Contents != null)
        {
            inputPrompt += "|" + string.Join("|", request.Contents.SelectMany(c => c.Parts).Select(p => p.Text));
        }
        var inputTokens = EstimateTokens(request.SystemInstruction) + EstimateTokens(inputPrompt);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
            httpRequest.Content = JsonContent.Create(BuildPayload(request), options: JsonOptions);

            using var response = await _httpClient.SendAsync(httpRequest, cts.Token);
            var rawResponse = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Gemini request failed with status {StatusCode}: {Response}",
                    (int)response.StatusCode,
                    rawResponse);

                throw new InvalidOperationException("Gemini request failed.");
            }

            var text = ExtractText(rawResponse);
            outputTokens = EstimateTokens(text);
            var resultResponse = new GeminiGenerateResponse
            {
                Text = text,
                Model = model,
                RawResponse = rawResponse
            };

            // Cache the result
            if (!request.BypassCache)
            {
                _cache.Set(cacheKey, resultResponse, TimeSpan.FromMinutes(10));
            }

            success = true;
            return resultResponse;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MomCareContext>();
                    db.GeminiCallLogs.Add(new GeminiCallLog
                    {
                        Id = Guid.NewGuid(),
                        CallType = request.CallType ?? "generate",
                        InputTokens = inputTokens,
                        OutputTokens = outputTokens,
                        LatencyMs = stopwatch.ElapsedMilliseconds,
                        Success = success,
                        ErrorMessage = errorMessage,
                        FallbackUsed = !success,
                        PromptVersion = request.PromptVersion ?? "v1",
                        CreatedAt = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist Gemini call log.");
                }
            });
        }
    }

    private string ComputeCacheKey(GeminiGenerateRequest request)
    {
        using var sha256 = SHA256.Create();
        var rawKey = $"{request.SystemInstruction ?? ""}|{request.Prompt ?? ""}|{request.Temperature ?? 0.0}|{JsonSerializer.Serialize(request.Contents, JsonOptions)}";
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToBase64String(hashBytes);
    }

    private string? GetApiKey() =>
        _options.ApiKey
        ?? _configuration["Gemini:ApiKey"]
        ?? _configuration["GEMINI_API_KEY"]
        ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

    private static string NormalizeModelName(string model)
    {
        return model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? model["models/".Length..]
            : model;
    }

    private static object BuildPayload(GeminiGenerateRequest request)
    {
        object contentsPayload;
        if (request.Contents != null && request.Contents.Count > 0)
        {
            contentsPayload = request.Contents.Select(c => new
            {
                role = string.IsNullOrWhiteSpace(c.Role) ? "user" : c.Role,
                parts = c.Parts.Select(p => new { text = p.Text }).ToArray()
            }).ToArray();
        }
        else
        {
            contentsPayload = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = request.Prompt.Trim() }
                    }
                }
            };
        }

        var payload = new Dictionary<string, object?>
        {
            ["contents"] = contentsPayload
        };

        if (!string.IsNullOrWhiteSpace(request.SystemInstruction))
        {
            payload["systemInstruction"] = new
            {
                parts = new[]
                {
                    new { text = request.SystemInstruction.Trim() }
                }
            };
        }

        if (request.Temperature is not null || request.MaxOutputTokens is not null)
        {
            payload["generationConfig"] = new
            {
                temperature = request.Temperature,
                maxOutputTokens = request.MaxOutputTokens
            };
        }

        return payload;
    }

    private static string ExtractText(string rawResponse)
    {
        using var document = JsonDocument.Parse(rawResponse);
        if (!document.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var firstCandidate = candidates[0];
        if (!firstCandidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        return string.Join(
            string.Empty,
            parts.EnumerateArray()
                .Where(part => part.TryGetProperty("text", out _))
                .Select(part => part.GetProperty("text").GetString())
                .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static int EstimateTokens(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : Math.Max(1, text.Length / 4);
}
