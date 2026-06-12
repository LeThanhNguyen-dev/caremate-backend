namespace MomCare.Dto;

public class HealthAnalysisResult
{
    public string Summary { get; set; } = string.Empty;
    public string WarningLevel { get; set; } = string.Empty;
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

    // v3.0 — PPD Screening
    public int PpdScreeningScore { get; set; }
    public string PpdScreeningLevel { get; set; } = string.Empty;
    public string PpdScreeningNote { get; set; } = string.Empty;

    // v3.0 — Nutrition Guidance
    public List<NutritionTipDto> NutritionGuidance { get; set; } = [];

    // v3.0 — AI Narrative Summary (local NLG)
    public string NarrativeSummary { get; set; } = string.Empty;

    // v3.0 — Data Coverage
    public int DataCoveragePercent { get; set; }
    public List<string> DataCoverageItems { get; set; } = [];
    public List<string> MissingDataItems { get; set; } = [];
    public List<FollowUpQuestionDto> FollowUpQuestions { get; set; } = [];
}
