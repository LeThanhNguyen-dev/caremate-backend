namespace MomCare.Dto;

public class RegisterDto
{
    public required string FullName { get; set; }
    public string? Phone { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public string Role { get; set; } = "customer";
}
