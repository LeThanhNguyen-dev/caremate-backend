using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class CreateBookingDto
{
    [Required]
    public int NurseId { get; set; }

    [Required]
    public int ServiceId { get; set; }

    [Required]
    public int AvailabilitySlotId { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    [Required]
    public string Address { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
