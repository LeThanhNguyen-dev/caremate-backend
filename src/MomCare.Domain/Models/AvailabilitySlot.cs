using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("availability_slots")]
public class AvailabilitySlot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("nurse_profile_id")]
    public int NurseProfileId { get; set; }

    [Column("start_time")]
    public DateTime StartTime { get; set; }

    [Column("end_time")]
    public DateTime EndTime { get; set; }

    [ForeignKey("NurseProfileId")]
    public virtual NurseProfile NurseProfile { get; set; } = null!;
}
