namespace MomCare.Dto;

public class HealthAnalysisResponse
{
    public Guid CheckInId { get; set; }
    public Guid AnalysisId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string WarningLevel { get; set; } = string.Empty;
    public List<string> Recommendations { get; set; } = [];
    public List<SuggestedServiceDto> SuggestedServices { get; set; } = [];
    public string Disclaimer { get; set; } = string.Empty;
}
