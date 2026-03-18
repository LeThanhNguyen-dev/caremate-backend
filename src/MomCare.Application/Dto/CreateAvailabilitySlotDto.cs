using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class CreateAvailabilitySlotDto
{
    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }
}
