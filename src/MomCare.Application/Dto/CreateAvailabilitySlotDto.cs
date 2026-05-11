using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

/// <summary>
/// DTO for creating an availability slot.
/// Slots are now time-based only. Filtering by service is handled via nurse capabilities.
/// </summary>
public class CreateAvailabilitySlotDto
{
    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }
}
