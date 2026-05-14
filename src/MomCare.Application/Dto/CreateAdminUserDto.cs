using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class CreateAdminUserDto
{
    [Required]
    public required string FullName { get; set; }

    [EmailAddress]
    [Required]
    public required string Email { get; set; }

    [Phone]
    public string? Phone { get; set; }

    [Required]
    [MinLength(6)]
    public required string Password { get; set; }

    public string Role { get; set; } = "customer";
}
