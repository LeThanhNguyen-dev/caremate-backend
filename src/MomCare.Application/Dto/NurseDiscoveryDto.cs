namespace MomCare.Dto;

public class NurseDiscoveryDto
{
    public int UserId { get; set; }
    public int NurseProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string? Bio { get; set; }
    public string? Specialization { get; set; }
    public decimal AverageRating { get; set; }
    public int YearsExperience { get; set; }
    public int ServiceRadiusKm { get; set; }
    public decimal? ServicePrice { get; set; }
    public string? ServiceUnit { get; set; }
    public double? DistanceKm { get; set; }
    public string? DistanceSource { get; set; }
    public int MatchScore { get; set; }
    public List<string> MatchReasons { get; set; } = new();
    public string? AiMatchSummary { get; set; }
    public bool AiSummaryFallback { get; set; }
    public int CompletedBookings { get; set; }
    public int TotalReviews { get; set; }
    public DateTime? NextAvailableAt { get; set; }
    public string? District { get; set; }
}
