namespace MomCare.Dto;

public class RiskFactorDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Points { get; set; }
    public string Category { get; set; } = string.Empty;
}
