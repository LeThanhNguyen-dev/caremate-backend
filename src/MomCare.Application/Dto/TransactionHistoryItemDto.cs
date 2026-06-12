namespace MomCare.Dto;

public class TransactionHistoryItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int BookingId { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string? ServiceName { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Method { get; set; }
    public string? TransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
}
