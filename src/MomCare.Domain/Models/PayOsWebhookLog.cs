using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("payos_webhook_logs")]
public class PayOsWebhookLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_code")]
    public string? OrderCode { get; set; }

    [Column("event_code")]
    public string? EventCode { get; set; }

    [Column("event_description")]
    public string? EventDescription { get; set; }

    [Column("raw_payload")]
    public string RawPayload { get; set; } = string.Empty;

    [Column("is_verified")]
    public bool IsVerified { get; set; }

    [Column("is_processed")]
    public bool IsProcessed { get; set; }

    [Column("processing_error")]
    public string? ProcessingError { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; }

    [Column("received_at")]
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }
}
