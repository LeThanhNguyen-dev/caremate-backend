using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("nurse_profiles")]
public class NurseProfile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("bio")]
    public string? Bio { get; set; }

    [Column("specialization")]
    public string? Specialization { get; set; }

    [Column("certificates")]
    public string? Certificates { get; set; }

    [Column("years_experience")]
    public int YearsExperience { get; set; }

    [Column("service_radius_km")]
    public int ServiceRadiusKm { get; set; }

    [Column("average_rating")]
    public decimal AverageRating { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("is_verified")]
    public string IsVerified { get; set; } = "unverified";

    [Column("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }

    [Column("rejection_reason")]
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    [Column("verification_submission_status")]
    [MaxLength(30)]
    public string VerificationSubmissionStatus { get; set; } = "draft";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public virtual ApplicationUser User { get; set; } = null!;

    public virtual ICollection<NurseService> NurseServices { get; set; } = new List<NurseService>();
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    public virtual ICollection<AvailabilitySlot> AvailabilitySlots { get; set; } = new List<AvailabilitySlot>();
}
