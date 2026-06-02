using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class AnalyzeHealthCheckInRequest
{
    [Range(0, 24)]
    public double SleepHours { get; set; }

    [Range(1, 10)]
    public int PainLevel { get; set; }

    [MaxLength(120)]
    public string? PainLocation { get; set; }

    [MaxLength(120)]
    public string? PainType { get; set; }

    [MaxLength(80)]
    public string? PainDuration { get; set; }

    [MaxLength(80)]
    public string? PainTrend { get; set; }

    public List<string> Symptoms { get; set; } = [];

    public List<string> MedicalHistory { get; set; } = [];

    [Range(0, 120)]
    public int? MotherAge { get; set; }

    [Range(0, 300)]
    public int? SystolicBloodPressure { get; set; }

    [Range(0, 220)]
    public int? DiastolicBloodPressure { get; set; }

    [Range(30, 45)]
    public double? TemperatureCelsius { get; set; }

    public bool TookMedicationToday { get; set; }

    [MaxLength(300)]
    public string? MedicationNote { get; set; }

    [Required]
    public string Mood { get; set; } = string.Empty;

    [Required]
    public string MilkStatus { get; set; } = string.Empty;

    [Required]
    public string BabyFeeding { get; set; } = string.Empty;

    [Required]
    public string BabySleep { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Note { get; set; }
}
