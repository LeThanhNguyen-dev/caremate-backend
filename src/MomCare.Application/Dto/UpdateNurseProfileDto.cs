namespace MomCare.Dto;

public class UpdateNurseProfileDto
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Avatar { get; set; }
    public string? Bio { get; set; }
    public string? Specialization { get; set; }
    public int YearsExperience { get; set; }
    public int ServiceRadiusKm { get; set; }
    public string? BankBin { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountName { get; set; }
}
