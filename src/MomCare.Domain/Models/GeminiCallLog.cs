using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

/// <summary>
/// Stores telemetry for Gemini API calls made by CareMate AI features.
/// </summary>
[Table("gemini_call_logs")]
public class GeminiCallLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("call_type")]
    [MaxLength(40)]
    public string CallType { get; set; } = string.Empty;

    [Column("input_tokens")]
    public int InputTokens { get; set; }

    [Column("output_tokens")]
    public int OutputTokens { get; set; }

    [Column("latency_ms")]
    public long LatencyMs { get; set; }

    [Column("success")]
    public bool Success { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("fallback_used")]
    public bool FallbackUsed { get; set; }

    [Column("prompt_version")]
    [MaxLength(80)]
    public string? PromptVersion { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
