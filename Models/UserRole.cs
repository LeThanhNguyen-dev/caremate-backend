using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("user_roles")]
public class UserRole
{
    [Column("user_id")]
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Column("role_id")]
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
