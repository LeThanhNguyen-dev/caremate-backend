using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("community_post_likes")]
public class CommunityPostLike
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("post_id")]
    public int PostId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("PostId")]
    public virtual CommunityPost Post { get; set; } = null!;

    [ForeignKey("UserId")]
    public virtual ApplicationUser User { get; set; } = null!;
}
