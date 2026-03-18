using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class CreateReviewDto
{
    [Required]
    public int BookingId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    public string? Comment { get; set; }
}
