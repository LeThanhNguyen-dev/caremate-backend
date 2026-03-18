using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class ExternalLoginDto
{
    [Required]
    public required string Provider { get; set; } // "google" or "facebook"

    [Required]
    public required string IdToken { get; set; } // The token from the provider
}
