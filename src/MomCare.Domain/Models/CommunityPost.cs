using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("community_posts")]
public class CommunityPost
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("author_id")]
    public int AuthorId { get; set; }

    [Column("title")]
    [MaxLength(180)]
    public required string Title { get; set; }

    [Column("content")]
    [MaxLength(4000)]
    public required string Content { get; set; }

    [Column("tags")]
    [MaxLength(500)]
    public string? Tags { get; set; }

    [Column("image_url")]
    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    [Column("image_public_id")]
    [MaxLength(255)]
    public string? ImagePublicId { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("AuthorId")]
    public virtual ApplicationUser Author { get; set; } = null!;

    public virtual ICollection<CommunityComment> Comments { get; set; } = new List<CommunityComment>();
    public virtual ICollection<CommunityPostLike> Likes { get; set; } = new List<CommunityPostLike>();
}
