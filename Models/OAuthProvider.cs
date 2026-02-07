using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("oauth_providers")]
public class OAuthProvider
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("provider")]
    public required string Provider { get; set; } // "google", "facebook"

    [Column("provider_user_id")]
    public required string ProviderUserId { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
