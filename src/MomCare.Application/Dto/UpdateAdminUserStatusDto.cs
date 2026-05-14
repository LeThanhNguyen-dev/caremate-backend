using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class UpdateAdminUserStatusDto
{
    [Required]
    public required string Status { get; set; }
}
