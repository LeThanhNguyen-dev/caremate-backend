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

    [Column("years_experience")]
    public int YearsExperience { get; set; }

    [Column("service_radius_km")]
    public int ServiceRadiusKm { get; set; }

    [Column("is_verified")]
    public string IsVerified { get; set; } = "unverified";

    [Column("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    public virtual ICollection<NurseService> NurseServices { get; set; } = new List<NurseService>();
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    public virtual ICollection<AvailabilitySlot> AvailabilitySlots { get; set; } = new List<AvailabilitySlot>();
}
