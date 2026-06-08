namespace MomCare.Infrastructure.Configurations;

public class FptAiOptions
{
    public const string SectionName = "FptAi";

    public string? ApiKey { get; set; }
    public string IdCardEndpoint { get; set; } = "https://api.fpt.ai/vision/idr/vnm";
}
