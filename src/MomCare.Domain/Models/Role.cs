using Microsoft.AspNetCore.Identity;

namespace MomCare.Models;

public class ApplicationRole : IdentityRole<int>
{
    public string? DisplayName { get; set; }
}
