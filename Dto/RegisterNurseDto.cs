using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class RegisterNurseDto
{
    [Required]
    public required string FullName { get; set; }

    [Phone]
    public string? Phone { get; set; }

    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    [MinLength(6)]
    public required string Password { get; set; }

    // Nurse specific fields
    public string? Bio { get; set; }
    public int YearsExperience { get; set; }
    public int ServiceRadiusKm { get; set; }
}
