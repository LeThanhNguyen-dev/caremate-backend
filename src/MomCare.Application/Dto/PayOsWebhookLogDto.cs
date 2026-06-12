namespace MomCare.Dto;

public class PayOsWebhookLogDto
{
    public Guid Id { get; set; }
    public string? OrderCode { get; set; }
    public string? EventCode { get; set; }
    public string? EventDescription { get; set; }
    public bool IsVerified { get; set; }
    public bool IsProcessed { get; set; }
    public string? ProcessingError { get; set; }
    public int RetryCount { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
