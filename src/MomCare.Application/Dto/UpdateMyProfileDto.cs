namespace MomCare.Dto;

public class UpdateMyProfileDto
{
    public required string FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Avatar { get; set; }
}
