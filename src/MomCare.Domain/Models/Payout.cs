using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("payouts")]
public class Payout
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("nurse_id")] // Identity user id of nurse
    public int NurseId { get; set; }

    [Column("booking_id")]
    public int BookingId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; } // Amount nurse receives

    [Column("platform_fee")]
    public decimal PlatformFee { get; set; }

    // Status: "unreleased", "on_hold", "released"
    [Column("status")]
    public string Status { get; set; } = "unreleased";

    [Column("released_at")]
    public DateTime? ReleasedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("NurseId")]
    public virtual ApplicationUser Nurse { get; set; } = null!;

    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;
}
