using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("bookings")]
public class Booking
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("nurse_id")]
    public int NurseId { get; set; }

    [Column("service_id")]
    public int ServiceId { get; set; }

    // Status: pending_confirm, confirmed, in_progress, completed, cancelled, rejected
    [Column("status")]
    public string Status { get; set; } = "pending_confirm";

    [Column("total_price")]
    public decimal TotalPrice { get; set; }

    // Store the address snapshot or address string
    [Column("address")]
    public string Address { get; set; } = string.Empty;

    [Column("start_time")]
    public DateTime StartTime { get; set; }

    [Column("end_time")]
    public DateTime EndTime { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("CustomerId")]
    public virtual User Customer { get; set; } = null!;

    // Using User for Nurse if NurseProfile isn't the direct foreign key, but NurseProfile is better. 
    // Usually Booking links to NurseProfile. Let's link to User (Nurse role) or NurseProfile.
    // The requirement says "Select nurse". NurseProfile is 1-to-1 with User. 
    // Let's use NurseProfileId for clarity or stick to User ID if authentication is primary. 
    // Given NurseProfile has the specific business logic, let's link Key to User but logical relationship to Profile?
    // Let's stick to User ID for "NurseId" to be consistent with "CustomerId" (UserId), 
    // but we can also add a nav prop to NurseProfile if needed. 
    // Plan said NurseId. Let's assume NurseId refers to the User.Id of the nurse.

    [ForeignKey("NurseId")]
    public virtual User Nurse { get; set; } = null!;

    [ForeignKey("ServiceId")]
    public virtual Service Service { get; set; } = null!; // Or NurseService? 
    // Implementation plan said "ServiceId". If it's the generic service, we also need to store the price snapshot.
    // TotalPrice handles the price. ServiceId tracks what kind of service it was.

    public virtual ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();
    
    // One-to-One relationships (to be configured in Context)
    public virtual Payment? Payment { get; set; }
    public virtual Review? Review { get; set; }
    public virtual Dispute? Dispute { get; set; }
    public virtual Conversation? Conversation { get; set; }
}
