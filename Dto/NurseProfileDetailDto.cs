namespace MomCare.Dto;

public class NurseProfileDetailDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Bio { get; set; }
    public int YearsExperience { get; set; }
    public int ServiceRadiusKm { get; set; }
    public string IsVerified { get; set; } = null!;
    public List<NurseDocumentDto> Documents { get; set; } = new();
}
