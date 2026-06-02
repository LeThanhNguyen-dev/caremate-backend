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

    [Column("pain_location")]
    [MaxLength(120)]
    public string? PainLocation { get; set; }

    [Column("pain_type")]
    [MaxLength(120)]
    public string? PainType { get; set; }

    [Column("pain_duration")]
    [MaxLength(80)]
    public string? PainDuration { get; set; }

    [Column("pain_trend")]
    [MaxLength(80)]
    public string? PainTrend { get; set; }

    [Column("symptoms_json")]
    public string SymptomsJson { get; set; } = "[]";

    [Column("medical_history_json")]
    public string MedicalHistoryJson { get; set; } = "[]";

    [Column("mother_age")]
    public int? MotherAge { get; set; }

    [Column("systolic_blood_pressure")]
    public int? SystolicBloodPressure { get; set; }

    [Column("diastolic_blood_pressure")]
    public int? DiastolicBloodPressure { get; set; }

    [Column("temperature_celsius")]
    public double? TemperatureCelsius { get; set; }

    [Column("took_medication_today")]
    public bool TookMedicationToday { get; set; }

    [Column("medication_note")]
    [MaxLength(300)]
    public string? MedicationNote { get; set; }

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
