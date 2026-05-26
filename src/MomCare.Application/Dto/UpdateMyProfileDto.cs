namespace MomCare.Dto;

public class UpdateMyProfileDto
{
    public required string FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? Avatar { get; set; }
    public string? BankBin { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountName { get; set; }
}
