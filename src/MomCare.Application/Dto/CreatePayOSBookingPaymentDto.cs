using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class CreatePayOSBookingPaymentDto
{
    [Required]
    public int NurseId { get; set; }

    [Required]
    public int ServiceId { get; set; }

    public int? AvailabilitySlotId { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public List<DateTime>? PackageSessionStartTimes { get; set; }

    [Required]
    public string Address { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public string? ReturnUrl { get; set; }
    public string? CancelUrl { get; set; }
}
