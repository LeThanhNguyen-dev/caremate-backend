using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class AnalyzeHealthCheckInRequest
{
    [Range(0, 24)]
    public double SleepHours { get; set; }

    [Range(1, 10)]
    public int PainLevel { get; set; }

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
