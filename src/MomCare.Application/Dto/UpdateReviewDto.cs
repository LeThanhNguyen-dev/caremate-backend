using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class UpdateReviewDto
{
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    public string? Comment { get; set; }
}
