using Microsoft.AspNetCore.Identity;

namespace MomCare.Models;

public class ApplicationUser : IdentityUser<int>
{
    public required string FullName { get; set; }
    public string? Avatar { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
