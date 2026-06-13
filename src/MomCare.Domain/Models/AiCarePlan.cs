using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("ai_care_plans")]
public class AiCarePlan
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("booking_id")]
    public int? BookingId { get; set; }

    [Column("health_checkin_id")]
    public Guid? HealthCheckInId { get; set; }

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    [Column("plan_type")]
    [MaxLength(30)]
    public string PlanType { get; set; } = string.Empty;

    [Column("safety_level")]
    [MaxLength(10)]
    public string SafetyLevel { get; set; } = "normal";

    [Column("safety_notice")]
    public string? SafetyNotice { get; set; }

    [Column("summary")]
    public string Summary { get; set; } = string.Empty;

    [Column("recommended_services_json")]
    public string RecommendedServicesJson { get; set; } = "[]";

    [Column("plan_items_json")]
    public string PlanItemsJson { get; set; } = "[]";

    [Column("recommended_nurses_json")]
    public string RecommendedNursesJson { get; set; } = "[]";

    [Column("disclaimer")]
    public string Disclaimer { get; set; } = string.Empty;

    [Column("ai_model")]
    [MaxLength(120)]
    public string? AiModel { get; set; }

    [Column("fallback_mode")]
    public bool FallbackMode { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser User { get; set; } = null!;

    [ForeignKey(nameof(BookingId))]
    public virtual Booking? Booking { get; set; }

    [ForeignKey(nameof(HealthCheckInId))]
    public virtual HealthCheckIn? HealthCheckIn { get; set; }
}
