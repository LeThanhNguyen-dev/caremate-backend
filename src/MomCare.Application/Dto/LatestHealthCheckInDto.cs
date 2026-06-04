namespace MomCare.Dto;

public class LatestHealthCheckInDto
{
    public Guid CheckInId { get; set; }
    public DateTime CreatedAt { get; set; }
    public double SleepHours { get; set; }
    public int PainLevel { get; set; }
    public string? PainLocation { get; set; }
    public string? PainType { get; set; }
    public string? PainDuration { get; set; }
    public string? PainTrend { get; set; }
    public List<string> Symptoms { get; set; } = [];
    public List<string> MedicalHistory { get; set; } = [];
    public Dictionary<string, string> ContextData { get; set; } = [];
    public int? MotherAge { get; set; }
    public int? SystolicBloodPressure { get; set; }
    public int? DiastolicBloodPressure { get; set; }
    public double? TemperatureCelsius { get; set; }
    public bool TookMedicationToday { get; set; }
    public string? MedicationNote { get; set; }
    public string Mood { get; set; } = string.Empty;
    public string MilkStatus { get; set; } = string.Empty;
    public string BabyFeeding { get; set; } = string.Empty;
    public string BabySleep { get; set; } = string.Empty;
    public string? Note { get; set; }
    public HealthAnalysisResponse? Analysis { get; set; }
}
