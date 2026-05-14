namespace MomCare.Dto;

public class AdminUserDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? Status { get; set; }
    public decimal? AverageRating { get; set; }
    public int? YearsExperience { get; set; }
    public string? IsVerified { get; set; }
    public int BookingCount { get; set; }
    public string? Bio { get; set; }
}
