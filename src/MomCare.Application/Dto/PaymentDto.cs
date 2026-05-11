namespace MomCare.Dto;

public class PaymentDto
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public decimal? RefundAmount { get; set; }
    public string? RefundReason { get; set; }
    public string? RefundStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
}
