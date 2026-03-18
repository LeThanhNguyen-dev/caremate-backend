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
}
