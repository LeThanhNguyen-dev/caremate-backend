using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("community_comments")]
public class CommunityComment
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("post_id")]
    public int PostId { get; set; }

    [Column("author_id")]
    public int AuthorId { get; set; }

    [Column("parent_comment_id")]
    public int? ParentCommentId { get; set; }

    [Column("content")]
    [MaxLength(1200)]
    public required string Content { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("PostId")]
    public virtual CommunityPost Post { get; set; } = null!;

    [ForeignKey("AuthorId")]
    public virtual ApplicationUser Author { get; set; } = null!;

    [ForeignKey("ParentCommentId")]
    public virtual CommunityComment? ParentComment { get; set; }

    public virtual ICollection<CommunityComment> Replies { get; set; } = new List<CommunityComment>();

    public virtual ICollection<CommunityCommentLike> Likes { get; set; } = new List<CommunityCommentLike>();
}
