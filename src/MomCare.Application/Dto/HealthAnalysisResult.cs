namespace MomCare.Dto;

public class HealthAnalysisResult
{
    public string Summary { get; set; } = string.Empty;
    public string WarningLevel { get; set; } = string.Empty;
    public string TriageColor { get; set; } = string.Empty;
    public string UrgencyAction { get; set; } = string.Empty;
    public string WeeklySummary { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public int ConfidenceScore { get; set; }
    public string TrendSummary { get; set; } = string.Empty;
    public List<RiskFactorDto> RiskFactors { get; set; } = [];
    public List<TrendSignalDto> TrendSignals { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public List<CarePlanItemDto> CarePlan { get; set; } = [];
    public List<SuggestedServiceDto> SuggestedServices { get; set; } = [];
    public string? RawAiResponse { get; set; }
}
