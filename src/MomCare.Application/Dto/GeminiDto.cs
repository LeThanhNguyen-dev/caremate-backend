using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class GeminiGenerateRequest
{
    [Required]
    [MinLength(1)]
    public string Prompt { get; set; } = string.Empty;

    public string? SystemInstruction { get; set; }
    public double? Temperature { get; set; }
    public int? MaxOutputTokens { get; set; }
}

public class GeminiGenerateResponse
{
    public string Text { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? RawResponse { get; set; }
}
