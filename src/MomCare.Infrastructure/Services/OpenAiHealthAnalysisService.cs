using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class OpenAiHealthAnalysisService : IOpenAiHealthAnalysisService
{
    private const string OpenAiEndpoint = "https://api.openai.com/v1/chat/completions";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyList<SuggestedServiceDto> AvailableServices =
    [
        new() { ServiceKey = "postpartum-mother-care", ServiceName = "Chăm sóc mẹ sau sinh", Reason = string.Empty },
        new() { ServiceKey = "newborn-care", ServiceName = "Hỗ trợ chăm bé sơ sinh", Reason = string.Empty },
        new() { ServiceKey = "breastfeeding-support", ServiceName = "Tư vấn cho bé bú", Reason = string.Empty },
        new() { ServiceKey = "wound-monitoring-support", ServiceName = "Hỗ trợ theo dõi vết mổ", Reason = string.Empty },
        new() { ServiceKey = "mental-wellness-support", ServiceName = "Hỗ trợ tinh thần sau sinh", Reason = string.Empty },
        new() { ServiceKey = "baby-bath-care", ServiceName = "Tắm bé tại nhà", Reason = string.Empty },
        new() { ServiceKey = "nutrition-guidance", ServiceName = "Tư vấn dinh dưỡng sau sinh", Reason = string.Empty }
    ];

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiHealthAnalysisService> _logger;

    public OpenAiHealthAnalysisService(HttpClient httpClient, ILogger<OpenAiHealthAnalysisService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<HealthAnalysisResult> AnalyzeAsync(
        HealthCheckIn currentCheckIn,
        List<HealthCheckIn> recentHistory,
        CancellationToken cancellationToken)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY is not configured.");
        }

        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4.1-mini";

        using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var systemPrompt = "You are a healthcare support assistant for a postpartum mother and newborn care booking platform. You do not diagnose diseases. You provide general wellness guidance, detect risk signals, and recommend suitable home-care services. Always include safety advice when symptoms look serious. Return only valid JSON. Do not use markdown.";
        var userPrompt = BuildUserPrompt(currentCheckIn, recentHistory);

        var payload = new
        {
            model,
            temperature = 0.2,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI health analysis request failed with status code {StatusCode}", response.StatusCode);
            throw new HttpRequestException($"OpenAI request failed with status {(int)response.StatusCode}.");
        }

        var completion = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(rawResponse, JsonOptions)
            ?? throw new InvalidOperationException("OpenAI returned an empty response.");

        var content = completion.Choices.FirstOrDefault()?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("OpenAI response content was empty.");
        }

        var parsed = JsonSerializer.Deserialize<OpenAiAnalysisPayload>(content, JsonOptions)
            ?? throw new InvalidOperationException("OpenAI response JSON could not be parsed.");

        return new HealthAnalysisResult
        {
            Summary = parsed.Summary ?? string.Empty,
            WarningLevel = NormalizeWarningLevel(parsed.WarningLevel),
            Recommendations = parsed.Recommendations?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [],
            SuggestedServices = parsed.SuggestedServices?.Where(x => !string.IsNullOrWhiteSpace(x.ServiceKey))
                .Select(x => new SuggestedServiceDto
                {
                    ServiceKey = x.ServiceKey ?? string.Empty,
                    ServiceName = x.ServiceName ?? string.Empty,
                    Reason = x.Reason ?? string.Empty
                })
                .ToList() ?? [],
            RawAiResponse = content
        };
    }

    private static string BuildUserPrompt(HealthCheckIn currentCheckIn, List<HealthCheckIn> recentHistory)
    {
        var promptObject = new
        {
            instruction = "Analyze the latest postpartum mother and newborn wellness check-in. Use the recent history and available services. Return only valid JSON with the exact schema provided.",
            currentCheckIn = new
            {
                currentCheckIn.SleepHours,
                currentCheckIn.PainLevel,
                currentCheckIn.Mood,
                currentCheckIn.MilkStatus,
                currentCheckIn.BabyFeeding,
                currentCheckIn.BabySleep,
                currentCheckIn.Note,
                currentCheckIn.CreatedAt
            },
            recentHistory = recentHistory
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.SleepHours,
                    x.PainLevel,
                    x.Mood,
                    x.MilkStatus,
                    x.BabyFeeding,
                    x.BabySleep,
                    x.Note,
                    x.CreatedAt
                }),
            availableServices = AvailableServices.Select(x => new { x.ServiceKey, x.ServiceName }),
            rules = new[]
            {
                "Do not diagnose diseases.",
                "Do not state certainty that the user has a disease.",
                "If there are serious risk signals, set warningLevel to High and recommend contacting a medical facility.",
                "If painLevel >= 8, warningLevel must be at least Medium.",
                "If sleepHours < 5 for 3 recent days, suggest newborn-care or postpartum-mother-care.",
                "If mood is Stressed or Anxious for multiple days, suggest mental-wellness-support.",
                "If milkStatus is Low or Painful, suggest breastfeeding-support.",
                "If babyFeeding is LessThanUsual or RefusesFeeding, suggest newborn-care or breastfeeding-support.",
                "If note includes danger keywords such as sốt cao, khó thở, chảy máu nhiều, đau dữ dội, vết mổ sưng đỏ, vết mổ chảy dịch, set warningLevel to High and recommend contacting a medical facility."
            },
            requiredJsonSchema = new
            {
                summary = "string",
                warningLevel = "Low | Medium | High",
                recommendations = new[] { "string" },
                suggestedServices = new[]
                {
                    new
                    {
                        serviceKey = "string",
                        serviceName = "string",
                        reason = "string"
                    }
                }
            }
        };

        return JsonSerializer.Serialize(promptObject, JsonOptions);
    }

    private static string NormalizeWarningLevel(string? warningLevel)
    {
        return warningLevel?.Trim().ToLowerInvariant() switch
        {
            "high" => "High",
            "medium" => "Medium",
            _ => "Low"
        };
    }

    private sealed class OpenAiChatCompletionResponse
    {
        public List<OpenAiChoice> Choices { get; set; } = [];
    }

    private sealed class OpenAiChoice
    {
        public OpenAiMessage? Message { get; set; }
    }

    private sealed class OpenAiMessage
    {
        public string? Content { get; set; }
    }

    private sealed class OpenAiAnalysisPayload
    {
        public string? Summary { get; set; }
        public string? WarningLevel { get; set; }
        public List<string>? Recommendations { get; set; }
        public List<OpenAiSuggestedServicePayload>? SuggestedServices { get; set; }
    }

    private sealed class OpenAiSuggestedServicePayload
    {
        public string? ServiceKey { get; set; }
        public string? ServiceName { get; set; }
        public string? Reason { get; set; }
    }
}
