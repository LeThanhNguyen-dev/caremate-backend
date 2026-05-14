using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("ai_health_analyses")]
public class AiHealthAnalysis
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("health_checkin_id")]
    public Guid HealthCheckInId { get; set; }

    [Column("summary")]
    public required string Summary { get; set; }

    [Column("warning_level")]
    public required string WarningLevel { get; set; }

    [Column("recommendations_json")]
    public required string RecommendationsJson { get; set; }

    [Column("suggested_services_json")]
    public required string SuggestedServicesJson { get; set; }

    [Column("raw_ai_response")]
    public string? RawAiResponse { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(HealthCheckInId))]
    public virtual HealthCheckIn HealthCheckIn { get; set; } = null!;
}
