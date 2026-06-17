using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Infrastructure.Configurations;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class GroqService : ILlmService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly GroqOptions _groqOptions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GroqService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IServiceProvider _serviceProvider;

    public GroqService(
        HttpClient httpClient,
        IOptions<GroqOptions> groqOptions,
        IConfiguration configuration,
        ILogger<GroqService> logger,
        IMemoryCache cache,
        IServiceProvider serviceProvider)
    {
        _httpClient = httpClient;
        _groqOptions = groqOptions.Value;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
        _serviceProvider = serviceProvider;
    }

    public async Task<GeminiGenerateResponse> GenerateAsync(GeminiGenerateRequest request, CancellationToken cancellationToken)
    {
        var apiKey = GetGroqApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Groq API key is not configured.");
        }

        var cacheKey = $"groq_response:{ComputeCacheKey(request)}";
        if (!request.BypassCache && _cache.TryGetValue<GeminiGenerateResponse>(cacheKey, out var cachedResponse) && cachedResponse != null)
        {
            _logger.LogInformation("Returning cached Groq response");
            return cachedResponse;
        }

        var model = GetGroqModel();
        var endpoint = $"{_groqOptions.BaseUrl.TrimEnd('/')}/responses";

        var timeoutSeconds = request.TimeoutSeconds ?? _groqOptions.DefaultTimeoutSeconds;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var stopwatch = Stopwatch.StartNew();
        var success = false;
        string? errorMessage = null;
        var outputTokens = 0;
        var inputPrompt = request.Prompt ?? string.Empty;
        if (request.Contents != null)
        {
            inputPrompt += "|" + string.Join("|", request.Contents.SelectMany(c => c.Parts).Select(p => p.Text));
        }
        var inputTokens = EstimateTokens(request.SystemInstruction) + EstimateTokens(inputPrompt);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = JsonContent.Create(BuildGroqPayload(request, model), options: JsonOptions);

            using var response = await _httpClient.SendAsync(httpRequest, cts.Token);
            var rawResponse = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Groq request failed with status {StatusCode}: {Response}",
                    (int)response.StatusCode,
                    rawResponse);

                var msg = $"Groq API returned {(int)response.StatusCode}: {rawResponse}";
                _logger.LogError("Groq error: {Msg}", msg);
                throw new InvalidOperationException("Groq request failed: " + msg);
            }

            var text = ExtractGroqText(rawResponse);
            outputTokens = EstimateTokens(text);
            var resultResponse = new GeminiGenerateResponse
            {
                Text = text,
                Model = model,
                RawResponse = rawResponse
            };

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
                    _logger.LogWarning(ex, "Failed to persist LLM call log.");
                }
            });
        }
    }

    private string ComputeCacheKey(GeminiGenerateRequest request)
    {
        using var sha256 = SHA256.Create();
        var rawKey = $"{request.SystemInstruction ?? string.Empty}|{request.Prompt ?? string.Empty}|{request.Temperature ?? 0.0}|{JsonSerializer.Serialize(request.Contents, JsonOptions)}";
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToBase64String(hashBytes);
    }

    private string? GetGroqApiKey() =>
        FirstNonEmpty(
            _groqOptions.ApiKey,
            _configuration["Groq:ApiKey"],
            _configuration["GROQ_API_KEY"],
            Environment.GetEnvironmentVariable("GROQ_API_KEY"));

    private string GetGroqModel()
    {
        var configuredModel = FirstNonEmpty(
            _groqOptions.Model,
            _configuration["Groq:Model"],
            _configuration["GROQ_MODEL"],
            Environment.GetEnvironmentVariable("GROQ_MODEL"));

        return string.IsNullOrWhiteSpace(configuredModel) ? "openai/gpt-oss-20b" : configuredModel.Trim();
    }

    private static object BuildGroqPayload(GeminiGenerateRequest request, string model)
    {
        return new
        {
            model,
            input = BuildGroqInput(request),
            temperature = request.Temperature,
            max_output_tokens = request.MaxOutputTokens
        };
    }

    private static string BuildGroqInput(GeminiGenerateRequest request)
    {
        var segments = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.SystemInstruction))
        {
            segments.Add($"System:\n{request.SystemInstruction.Trim()}");
        }

        if (request.Contents != null && request.Contents.Count > 0)
        {
            foreach (var content in request.Contents)
            {
                var messageText = string.Join(
                    "\n",
                    content.Parts
                        .Select(part => part.Text?.Trim())
                        .Where(text => !string.IsNullOrWhiteSpace(text)));

                if (string.IsNullOrWhiteSpace(messageText))
                {
                    continue;
                }

                segments.Add($"{NormalizeRole(content.Role)}:\n{messageText}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            segments.Add($"user:\n{request.Prompt.Trim()}");
        }

        return string.Join("\n\n", segments);
    }

    private static string ExtractGroqText(string rawResponse)
    {
        using var document = JsonDocument.Parse(rawResponse);
        if (document.RootElement.TryGetProperty("output_text", out var outputText) &&
            outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString() ?? string.Empty;
        }

        if (!document.RootElement.TryGetProperty("output", out var output) ||
            output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        return ExtractOutputText(output);
    }

    private static string NormalizeRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return "user";
        }

        var normalized = role.Trim().ToLowerInvariant();
        return normalized switch
        {
            "model" => "assistant",
            "assistant" => "assistant",
            "system" => "system",
            _ => "user"
        };
    }

    private static int EstimateTokens(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : Math.Max(1, text.Length / 4);

    private static string ExtractOutputText(JsonElement output)
    {
        var fragments = new List<string>();

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (!part.TryGetProperty("type", out var typeElement) ||
                    typeElement.ValueKind != JsonValueKind.String ||
                    !string.Equals(typeElement.GetString(), "output_text", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (part.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                {
                    var text = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        fragments.Add(text);
                    }
                }
            }
        }

        return string.Join(string.Empty, fragments);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
