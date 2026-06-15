using MomCare.Dto;

namespace MomCare.Services;

/// <summary>
/// Validates and normalizes structured Gemini care plan reasoning output.
/// </summary>
public class PlanValidatorEngine
{
    /// <summary>
    /// Filters invalid service ids, clamps scores and durations, reindexes sessions, and builds service fallback scores.
    /// </summary>
    public GeminiReasoningResult Validate(GeminiReasoningResult raw, List<ServiceSummaryForAi> validServices)
    {
        var validServiceIds = validServices.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var serviceScores = raw.ServiceScores
            .Where(x => validServiceIds.Contains(x.ServiceId))
            .Select(x => new ServiceScore
            {
                ServiceId = x.ServiceId,
                Score = Math.Clamp(x.Score, 0, 100),
                Reason = string.IsNullOrWhiteSpace(x.Reason) ? "Phù hợp với giai đoạn hậu sản của bạn." : x.Reason.Trim(),
                MatchedNeeds = x.MatchedNeeds.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            })
            .OrderByDescending(x => x.Score)
            .Take(6)
            .ToList();

        if (serviceScores.Count == 0)
        {
            serviceScores = validServices
                .OrderByDescending(x => x.IsPackage)
                .ThenBy(x => x.Price)
                .Take(3)
                .Select(x => new ServiceScore
                {
                    ServiceId = x.Id,
                    Score = 60,
                    Reason = "Phù hợp với giai đoạn hậu sản của bạn."
                })
                .ToList();
        }

        var planItems = raw.PlanItems
            .Take(10)
            .Select((item, index) => new PlanItemSuggestion
            {
                SessionNumber = index + 1,
                SuggestedDate = string.IsNullOrWhiteSpace(item.SuggestedDate) ? $"D+{index + 1}" : item.SuggestedDate.Trim(),
                Focus = string.IsNullOrWhiteSpace(item.Focus) ? $"Buổi {index + 1}" : item.Focus.Trim(),
                Activities = item.Activities.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Take(5).ToList(),
                Note = (item.Note ?? string.Empty).Trim(),
                EstimatedDurationMinutes = Math.Clamp(item.EstimatedDurationMinutes, 30, 240)
            })
            .ToList();

        return new GeminiReasoningResult
        {
            ServiceScores = serviceScores,
            PlanItems = planItems,
            Reasoning = raw.Reasoning,
            IsFromAi = raw.IsFromAi,
            TokensUsed = raw.TokensUsed
        };
    }
}
