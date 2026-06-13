namespace MomCare.Dto;

public class HealthCheckInRiskPreviewDto
{
    public string WarningLevel { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public int ConfidenceScore { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string UrgencyAction { get; set; } = string.Empty;
    public List<RiskFactorDto> RiskFactors { get; set; } = [];
}

public class HealthCheckInFollowUpPreviewResponse
{
    public int DataCoveragePercent { get; set; }
    public List<string> DataCoverageItems { get; set; } = [];
    public List<string> MissingDataItems { get; set; } = [];
    public List<FollowUpQuestionDto> FollowUpQuestions { get; set; } = [];
    public HealthCheckInRiskPreviewDto EstimatedRiskPreview { get; set; } = new();
    public string EngineVersion { get; set; } = string.Empty;
}
