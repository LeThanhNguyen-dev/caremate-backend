using System.Globalization;
using System.Text;
using MomCare.Dto;

namespace MomCare.Services;

/// <summary>
/// Validates and normalizes structured care plan reasoning output.
/// </summary>
public class PlanValidatorEngine
{
    private const double MinimumAcceptedScore = 0.55d;
    private const double MinimumLowConfidenceScore = 0.40d;
    private const int StrictRecommendationLimit = 4;

    private static readonly string[] GenericReasonPrefixes =
    [
        "phu hop voi tinh trang cua ban",
        "dich vu nay co the ho tro ban",
        "goi y cham soc phu hop"
    ];

    public GeminiReasoningResult Validate(
        GeminiReasoningResult raw,
        List<ServiceSummaryForAi> validServices,
        bool allowServiceFallback = true,
        SymptomTagResult? tags = null)
    {
        var validServiceIds = validServices.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var limit = allowServiceFallback ? 6 : StrictRecommendationLimit;

        var serviceScores = raw.ServiceScores
            .Where(x => validServiceIds.Contains(x.ServiceId))
            .Select(x => new ServiceScore
            {
                ServiceId = x.ServiceId,
                Score = NormalizeScore(x.Score),
                Reason = string.IsNullOrWhiteSpace(x.Reason) ? "Phu hop voi giai doan hau san cua ban." : x.Reason.Trim(),
                MatchedNeeds = x.MatchedNeeds.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            })
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .ToList();

        if (allowServiceFallback && serviceScores.Count == 0)
        {
            serviceScores = validServices
                .OrderByDescending(x => x.IsPackage)
                .ThenBy(x => x.Price)
                .Take(3)
                .Select(x => new ServiceScore
                {
                    ServiceId = x.Id,
                    Score = 0.60d,
                    Reason = "Phu hop voi giai doan hau san cua ban."
                })
                .ToList();
        }

        var isRecommendationRejected = !allowServiceFallback && ShouldRejectRecommendation(serviceScores, tags);
        if (isRecommendationRejected)
        {
            serviceScores.Clear();
        }

        var planItems = raw.PlanItems
            .Take(10)
            .Select((item, index) => new PlanItemSuggestion
            {
                SessionNumber = index + 1,
                SuggestedDate = string.IsNullOrWhiteSpace(item.SuggestedDate) ? $"D+{index + 1}" : item.SuggestedDate.Trim(),
                Focus = string.IsNullOrWhiteSpace(item.Focus) ? $"Buoi {index + 1}" : item.Focus.Trim(),
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
            IsFromAi = raw.IsFromAi && !isRecommendationRejected,
            TokensUsed = raw.TokensUsed
        };
    }

    private static bool ShouldRejectRecommendation(List<ServiceScore> serviceScores, SymptomTagResult? tags)
    {
        if (serviceScores.Count == 0)
        {
            return true;
        }

        if (serviceScores.All(score => score.Score < MinimumLowConfidenceScore))
        {
            return true;
        }

        if (serviceScores.All(score => score.Score < MinimumAcceptedScore))
        {
            return true;
        }

        var tokens = BuildReasonValidationTokens(tags);
        return serviceScores.All(score => IsGenericReason(score.Reason, tokens));
    }

    private static HashSet<string> BuildReasonValidationTokens(SymptomTagResult? tags)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (tags is null)
        {
            return tokens;
        }

        foreach (var token in tags.RelevantContextTokens)
        {
            var normalized = Normalize(token);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                tokens.Add(normalized);
            }
        }

        var normalizedConcern = Normalize(tags.PrimaryConcern);
        if (!string.IsNullOrWhiteSpace(normalizedConcern))
        {
            tokens.Add(normalizedConcern.Replace('_', ' '));
        }

        return tokens;
    }

    private static bool IsGenericReason(string? reason, HashSet<string> tokens)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return true;
        }

        var normalized = Normalize(reason);
        if (normalized.Length < 45)
        {
            return true;
        }

        if (GenericReasonPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return true;
        }

        if (tokens.Count == 0)
        {
            return false;
        }

        return !tokens.Any(token => normalized.Contains(token, StringComparison.Ordinal));
    }

    private static double NormalizeScore(double value)
    {
        if (value > 1d && value <= 100d)
        {
            value /= 100d;
        }

        return Math.Clamp(value, 0d, 1d);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant()
            .Replace("-", " ")
            .Replace("_", " ");
        var formD = normalized.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
