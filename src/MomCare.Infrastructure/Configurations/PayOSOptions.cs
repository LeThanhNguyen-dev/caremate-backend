namespace MomCare.Infrastructure.Configurations;

public class PayOSOptions
{
    public const string SectionName = "PayOS";

    public string? ClientId { get; set; }
    public string? ApiKey { get; set; }
    public string? ChecksumKey { get; set; }
    public string? ReturnUrl { get; set; }
    public string? CancelUrl { get; set; }
    public string? WebhookUrl { get; set; }
}
