using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

/// <summary>
/// Uses Gemini to reason over sanitized symptom tags and produce structured care plan suggestions.
/// </summary>
public class GeminiReasoningService
{
    public const string PromptVersion = "reasoning_v1";

    private readonly ILlmService _llmService;
    private readonly GeminiPromptBuilder _promptBuilder;
    private readonly GeminiCallLogService _callLogService;
    private readonly ILogger<GeminiReasoningService> _logger;

    public GeminiReasoningService(
        ILlmService llmService,
        GeminiPromptBuilder promptBuilder,
        GeminiCallLogService callLogService,
        ILogger<GeminiReasoningService> logger)
    {
        _llmService = llmService;
        _promptBuilder = promptBuilder;
        _callLogService = callLogService;
        _logger = logger;
    }

    /// <summary>
    /// Calls Gemini for structured care plan reasoning and returns a fallback result on any failure.
    /// </summary>
    public async Task<GeminiReasoningResult> ReasonAsync(
        SymptomTagResult tags,
        List<ServiceSummaryForAi> services,
        BookingContextForAi? booking,
        CancellationToken cancellationToken)
    {
        var prompt = _promptBuilder.BuildReasoningPrompt(tags, services, booking);
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        var fallbackUsed = true;
        string? errorMessage = null;
        var outputTokens = 0;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));

            var response = await _llmService.GenerateAsync(new GeminiGenerateRequest
            {
                SystemInstruction = "Bạn chỉ trả về JSON hợp lệ, không markdown, không giải thích ngoài JSON.",
                Prompt = prompt,
                Temperature = 0.3,
                MaxOutputTokens = 2048
            }, timeout.Token);

            var text = StripCodeFence(response.Text);
            outputTokens = EstimateTokens(text);
            var parsed = JsonSerializer.Deserialize<GeminiReasoningOutput>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (parsed is null)
            {
                errorMessage = "AI returned empty reasoning JSON.";
                return Fallback();
            }

            success = true;
            fallbackUsed = false;
            return new GeminiReasoningResult
            {
                ServiceScores = parsed.ServiceScores,
                PlanItems = parsed.PlanItems,
                Reasoning = parsed.Reasoning.Trim(),
                IsFromAi = true,
                TokensUsed = EstimateTokens(prompt) + outputTokens
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
        {
            errorMessage = ex.Message;
            _logger.LogWarning(ex, "AI care plan reasoning failed. Falling back to deterministic plan.");
            return Fallback();
        }
        finally
        {
            stopwatch.Stop();
            await _callLogService.SaveAsync(new GeminiCallLog
            {
                Id = Guid.NewGuid(),
                CallType = "reasoning",
                InputTokens = EstimateTokens(prompt),
                OutputTokens = outputTokens,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Success = success,
                ErrorMessage = errorMessage,
                FallbackUsed = fallbackUsed,
                PromptVersion = PromptVersion,
                CreatedAt = DateTime.UtcNow
            }, CancellationToken.None);
        }
    }

    private static GeminiReasoningResult Fallback() => new()
    {
        IsFromAi = false
    };

    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstLineBreak = trimmed.IndexOf('\n');
        if (firstLineBreak >= 0)
        {
            trimmed = trimmed[(firstLineBreak + 1)..];
        }

        if (trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^3];
        }

        return trimmed.Trim();
    }

    private static int EstimateTokens(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : Math.Max(1, text.Length / 4);
}
