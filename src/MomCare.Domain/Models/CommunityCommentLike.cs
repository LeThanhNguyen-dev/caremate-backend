using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("community_comment_likes")]
public class CommunityCommentLike
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("comment_id")]
    public int CommentId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("CommentId")]
    public virtual CommunityComment Comment { get; set; } = null!;

    [ForeignKey("UserId")]
    public virtual ApplicationUser User { get; set; } = null!;
}
