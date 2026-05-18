using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("package_session_logs")]
public class PackageSessionLog
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("booking_id")]
    public int BookingId { get; set; }

    [Column("session_number")]
    public int SessionNumber { get; set; }

    [Column("session_date")]
    public DateTime SessionDate { get; set; }

    // Copied from template
    [Column("title")]
    public string? Title { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("planned_service_keys")]
    public string? PlannedServiceKeys { get; set; }

    // Nurse records
    [Column("check_in_time")]
    public DateTime? CheckInTime { get; set; }

    [Column("check_out_time")]
    public DateTime? CheckOutTime { get; set; }

    // pending | checked_in | completed | skipped
    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("nurse_note")]
    public string? NurseNote { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;
}
