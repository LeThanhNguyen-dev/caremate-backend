using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("conversations")]
public class Conversation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("booking_id")]
    public int BookingId { get; set; }

    [Column("user1_id")]
    public int User1Id { get; set; }

    [Column("user2_id")]
    public int User2Id { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;

    [ForeignKey("User1Id")]
    public virtual User User1 { get; set; } = null!;

    [ForeignKey("User2Id")]
    public virtual User User2 { get; set; } = null!;

    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
