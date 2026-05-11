namespace MomCare.Dto;

/// <summary>
/// Aggregated nurse rating info for profile display.
/// </summary>
public class NurseRatingDto
{
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new(); // rating → count (1-5)
}
