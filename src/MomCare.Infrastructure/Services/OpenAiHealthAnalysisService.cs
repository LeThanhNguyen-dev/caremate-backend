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
        IReadOnlyList<SuggestedServiceDto> availableServices,
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

        var systemPrompt = "You are a Vietnamese healthcare support assistant for a postpartum mother and newborn care booking platform. You do not diagnose diseases, prescribe medicine, or replace a doctor. You provide general wellness guidance, detect risk signals, summarize trends, recommend suitable home-care services, and create a short care plan. Always include safety advice when symptoms look serious. Return only valid JSON. Do not use markdown. All user-facing text values must be written in natural Vietnamese with diacritics.";
        var userPrompt = BuildUserPrompt(currentCheckIn, recentHistory, availableServices);

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
            TrendSummary = parsed.TrendSummary ?? string.Empty,
            Recommendations = parsed.Recommendations?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [],
            CarePlan = parsed.CarePlan?.Where(x => !string.IsNullOrWhiteSpace(x.Action))
                .Select(x => new CarePlanItemDto
                {
                    Timeframe = x.Timeframe ?? string.Empty,
                    Action = x.Action ?? string.Empty,
                    Reason = x.Reason ?? string.Empty
                })
                .ToList() ?? [],
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

    private static string BuildUserPrompt(
        HealthCheckIn currentCheckIn,
        List<HealthCheckIn> recentHistory,
        IReadOnlyList<SuggestedServiceDto> availableServices)
    {
        var promptObject = new
        {
            instruction = "Analyze the latest postpartum mother and newborn wellness check-in. Use recent history to summarize trends, and suggest only services from availableServices. Return only valid JSON with the exact schema provided. All summary, trendSummary, recommendations, carePlan, and reason values must be in Vietnamese with diacritics.",
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
            availableServices = availableServices.Select(x => new { x.ServiceKey, x.ServiceName }),
            rules = new[]
            {
                "Do not diagnose diseases.",
                "Do not state certainty that the user has a disease.",
                "Do not prescribe medicine or replace medical professionals.",
                "Write all user-facing content in Vietnamese with diacritics.",
                "If there are serious risk signals, set warningLevel to High and recommend contacting a medical facility.",
                "If painLevel >= 8, warningLevel must be at least Medium.",
                "If sleepHours < 5 for 3 recent days, consider a service related to mother care or newborn care if it exists in availableServices.",
                "If mood is Stressed or Anxious for multiple days, consider a mental wellness service if it exists in availableServices.",
                "If milkStatus is Low or Painful, consider a breastfeeding support service if it exists in availableServices.",
                "If babyFeeding is LessThanUsual or RefusesFeeding, consider newborn care or breastfeeding support if it exists in availableServices.",
                "If note includes danger keywords such as sot cao, kho tho, chay mau nhieu, dau du doi, vet mo sung do, vet mo chay dich, set warningLevel to High and recommend contacting a medical facility.",
                "For suggestedServices, serviceKey must exactly match one key from availableServices. If no service fits, return an empty array.",
                "Care plan must be practical, short, and focused on the next 1 to 7 days.",
                "If warningLevel is High, carePlan must include contacting a medical facility or doctor."
            },
            requiredJsonSchema = new
            {
                summary = "string",
                warningLevel = "Low | Medium | High",
                trendSummary = "string",
                recommendations = new[] { "string" },
                carePlan = new[]
                {
                    new
                    {
                        timeframe = "string",
                        action = "string",
                        reason = "string"
                    }
                },
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
        public string? TrendSummary { get; set; }
        public List<string>? Recommendations { get; set; }
        public List<OpenAiCarePlanPayload>? CarePlan { get; set; }
        public List<OpenAiSuggestedServicePayload>? SuggestedServices { get; set; }
    }

    private sealed class OpenAiCarePlanPayload
    {
        public string? Timeframe { get; set; }
        public string? Action { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class OpenAiSuggestedServicePayload
    {
        public string? ServiceKey { get; set; }
        public string? ServiceName { get; set; }
        public string? Reason { get; set; }
    }
}
