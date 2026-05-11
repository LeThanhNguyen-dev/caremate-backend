using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("documents")]
public class Document
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("nurse_profile_id")]
    public int NurseProfileId { get; set; }

    // Type: id_card_front, id_card_back, certificate
    [Column("type")]
    public required string Type { get; set; }

    // Removed FileUrl to store only PublicId for private storage
    [Column("public_id")]
    public required string PublicId { get; set; }

    // Status: pending_review, approved, rejected
    [Column("status")]
    public string Status { get; set; } = "pending_review";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("NurseProfileId")]
    public virtual NurseProfile NurseProfile { get; set; } = null!;
}
