using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class CreateDisputeDto
{
    [Required]
    public int BookingId { get; set; }

    [Required]
    public string Reason { get; set; } = string.Empty;
}
