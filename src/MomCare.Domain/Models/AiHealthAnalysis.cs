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

    [Column("risk_score")]
    public int RiskScore { get; set; }

    [Column("confidence_score")]
    public int ConfidenceScore { get; set; }

    [Column("trend_summary")]
    public string TrendSummary { get; set; } = string.Empty;

    [Column("risk_factors_json")]
    public string RiskFactorsJson { get; set; } = "[]";

    [Column("trend_signals_json")]
    public string TrendSignalsJson { get; set; } = "[]";

    [Column("recommendations_json")]
    public required string RecommendationsJson { get; set; }

    [Column("care_plan_json")]
    public string CarePlanJson { get; set; } = "[]";

    [Column("suggested_services_json")]
    public required string SuggestedServicesJson { get; set; }

    [Column("raw_ai_response")]
    public string? RawAiResponse { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(HealthCheckInId))]
    public virtual HealthCheckIn HealthCheckIn { get; set; } = null!;
}
