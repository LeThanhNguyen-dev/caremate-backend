namespace MomCare.Infrastructure.Configurations;

public class GroqOptions
{
    public const string SectionName = "Groq";

    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";
    public string Model { get; set; } = "openai/gpt-oss-20b";
    public int DefaultTimeoutSeconds { get; set; } = 30;
}
