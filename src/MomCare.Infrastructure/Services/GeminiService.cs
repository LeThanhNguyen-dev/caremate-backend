using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

    public GeminiService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        IConfiguration configuration,
        ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<GeminiGenerateResponse> GenerateAsync(GeminiGenerateRequest request, CancellationToken cancellationToken)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Gemini API key is not configured.");
        }

        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gemini-2.0-flash" : _options.Model.Trim();
        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/v1beta/models/{NormalizeModelName(model)}:generateContent";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        httpRequest.Content = JsonContent.Create(BuildPayload(request), options: JsonOptions);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Gemini request failed with status {StatusCode}: {Response}",
                (int)response.StatusCode,
                rawResponse);

            throw new InvalidOperationException("Gemini request failed.");
        }

        var text = ExtractText(rawResponse);
        return new GeminiGenerateResponse
        {
            Text = text,
            Model = model,
            RawResponse = rawResponse
        };
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
        var payload = new Dictionary<string, object?>
        {
            ["contents"] = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = request.Prompt.Trim() }
                    }
                }
            }
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
}
