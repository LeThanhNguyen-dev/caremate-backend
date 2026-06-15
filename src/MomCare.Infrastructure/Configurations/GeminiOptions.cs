namespace MomCare.Infrastructure.Configurations;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public string Model { get; set; } = "gemini-2.0-flash-stable";
    public int DefaultTimeoutSeconds { get; set; } = 30;
}
