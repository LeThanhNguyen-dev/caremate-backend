using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("payments")]
public class Payment
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("booking_id")]
    public int BookingId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    // Method: "bank_transfer"
    [Column("method")]
    public string Method { get; set; } = "bank_transfer";

    // Status: "initiated", "paid", "refunded"
    [Column("status")]
    public string Status { get; set; } = "initiated";

    [Column("transaction_id")]
    public string? TransactionId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;
}
