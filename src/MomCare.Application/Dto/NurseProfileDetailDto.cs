namespace MomCare.Dto;

public class NurseProfileDetailDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Avatar { get; set; }
    public string? BankBin { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountName { get; set; }
    public string? Bio { get; set; }
    public string? Specialization { get; set; }
    public int YearsExperience { get; set; }
    public int ServiceRadiusKm { get; set; }
    public string IsVerified { get; set; } = "unverified";
    public string? RejectionReason { get; set; }
    public string VerificationSubmissionStatus { get; set; } = "draft";

    // Rating aggregation
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new();

    // Related data
    public List<NurseDocumentDto> Documents { get; set; } = new();
    public List<ReviewDetailDto> Reviews { get; set; } = new();
}
