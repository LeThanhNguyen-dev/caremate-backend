using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("health_checkins")]
public class HealthCheckIn
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("sleep_hours")]
    public double SleepHours { get; set; }

    [Column("pain_level")]
    public int PainLevel { get; set; }

    [Column("mood")]
    public required string Mood { get; set; }

    [Column("milk_status")]
    public required string MilkStatus { get; set; }

    [Column("baby_feeding")]
    public required string BabyFeeding { get; set; }

    [Column("baby_sleep")]
    public required string BabySleep { get; set; }

    [Column("note")]
    [MaxLength(1000)]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser User { get; set; } = null!;

    public virtual AiHealthAnalysis? Analysis { get; set; }
}
