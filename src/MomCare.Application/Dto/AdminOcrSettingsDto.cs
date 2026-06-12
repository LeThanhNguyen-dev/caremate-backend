namespace MomCare.Dto;

public class AdminOcrSettingsDto
{
    public string Provider { get; set; } = "FPT AI";
    public string Purpose { get; set; } = "CCCD OCR";
    public string IdCardEndpoint { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
    public string? MaskedApiKey { get; set; }
}
