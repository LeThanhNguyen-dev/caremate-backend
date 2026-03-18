using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("booking_status_history")]
public class BookingStatusHistory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("booking_id")]
    public int BookingId { get; set; }

    [Column("status")]
    public required string Status { get; set; }

    [Column("changed_by")]
    public int? ChangedBy { get; set; } // UserId

    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;

    [ForeignKey("ChangedBy")]
    public virtual ApplicationUser? Changer { get; set; }
}
