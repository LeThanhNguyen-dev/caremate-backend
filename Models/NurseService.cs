using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("nurse_services")]
public class NurseService
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("nurse_profile_id")]
    public int NurseProfileId { get; set; }

    [Column("service_id")]
    public int ServiceId { get; set; }

    [Column("price")]
    public decimal Price { get; set; }

    // Unit: "fixed", "hourly"
    [Column("unit")]
    public string Unit { get; set; } = "fixed";

    // Status: "enabled", "disabled" (nurse can toggle)
    [Column("status")]
    public string Status { get; set; } = "enabled";

    [ForeignKey("NurseProfileId")]
    public virtual NurseProfile NurseProfile { get; set; } = null!;

    [ForeignKey("ServiceId")]
    public virtual Service Service { get; set; } = null!;
}
