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

    [Column("triage_color")]
    public string TriageColor { get; set; } = string.Empty;

    [Column("urgency_action")]
    public string UrgencyAction { get; set; } = string.Empty;

    [Column("weekly_summary")]
    public string WeeklySummary { get; set; } = string.Empty;

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

    [Column("ai_model")]
    [MaxLength(120)]
    public string? AiModel { get; set; }

    [Column("ai_latency_ms")]
    public long? AiLatencyMs { get; set; }

    [Column("ai_fallback_mode")]
    [MaxLength(80)]
    public string? AiFallbackMode { get; set; }

    [Column("engine_version")]
    [MaxLength(80)]
    public string EngineVersion { get; set; } = string.Empty;

    // v3.0 — PPD Screening
    [Column("ppd_screening_score")]
    public int PpdScreeningScore { get; set; }

    [Column("ppd_screening_level")]
    public string PpdScreeningLevel { get; set; } = string.Empty;

    [Column("ppd_screening_note")]
    public string PpdScreeningNote { get; set; } = string.Empty;

    // v3.0 — AI Narrative Summary (local NLG)
    [Column("narrative_summary")]
    public string NarrativeSummary { get; set; } = string.Empty;

    // v3.0 — Nutrition Guidance
    [Column("nutrition_guidance_json")]
    public string NutritionGuidanceJson { get; set; } = "[]";

    // v3.0 — Data Coverage
    [Column("data_coverage_percent")]
    public int DataCoveragePercent { get; set; }

    [Column("data_coverage_items_json")]
    public string DataCoverageItemsJson { get; set; } = "[]";

    [Column("missing_data_items_json")]
    public string MissingDataItemsJson { get; set; } = "[]";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(HealthCheckInId))]
    public virtual HealthCheckIn HealthCheckIn { get; set; } = null!;
}
