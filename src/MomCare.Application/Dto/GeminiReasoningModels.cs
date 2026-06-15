using System.Text.Json.Serialization;

namespace MomCare.Dto;

/// <summary>
/// Summarizes a care service for AI service matching.
/// </summary>
public class ServiceSummaryForAi
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public decimal Price { get; set; }
    public bool IsPackage { get; set; }
}

/// <summary>
/// Summarizes an existing booking for AI plan enrichment.
/// </summary>
public class BookingContextForAi
{
    public string ServiceName { get; set; } = string.Empty;
    public int RemainingSessionCount { get; set; }
    public DateTime? NextSessionDate { get; set; }
}

/// <summary>
/// Represents an AI-ranked service recommendation.
/// </summary>
public class ServiceScore
{
    public string ServiceId { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string> MatchedNeeds { get; set; } = [];
}

/// <summary>
/// Represents a structured care plan item suggested by AI.
/// </summary>
public class PlanItemSuggestion
{
    public int SessionNumber { get; set; }
    public string SuggestedDate { get; set; } = string.Empty;
    public string Focus { get; set; } = string.Empty;
    public List<string> Activities { get; set; } = [];
    public string Note { get; set; } = string.Empty;
    public int EstimatedDurationMinutes { get; set; }
}

/// <summary>
/// Contains parsed and validated Gemini reasoning output.
/// </summary>
public class GeminiReasoningResult
{
    public List<ServiceScore> ServiceScores { get; set; } = [];
    public List<PlanItemSuggestion> PlanItems { get; set; } = [];
    public string Reasoning { get; set; } = string.Empty;
    public bool IsFromAi { get; set; }
    public int TokensUsed { get; set; }
}

/// <summary>
/// Represents the raw JSON schema returned by Gemini before validation.
/// </summary>
public class GeminiReasoningOutput
{
    [JsonPropertyName("serviceScores")]
    public List<ServiceScore> ServiceScores { get; set; } = [];

    [JsonPropertyName("planItems")]
    public List<PlanItemSuggestion> PlanItems { get; set; } = [];

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = string.Empty;
}
