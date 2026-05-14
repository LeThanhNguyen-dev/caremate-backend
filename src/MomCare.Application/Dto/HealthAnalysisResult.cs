namespace MomCare.Dto;

public class HealthAnalysisResult
{
    public string Summary { get; set; } = string.Empty;
    public string WarningLevel { get; set; } = string.Empty;
    public List<string> Recommendations { get; set; } = [];
    public List<SuggestedServiceDto> SuggestedServices { get; set; } = [];
    public string? RawAiResponse { get; set; }
}
