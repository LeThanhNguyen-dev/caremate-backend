namespace MomCare.Dto;

public class PayOSPaymentLinkDto
{
    public int BookingId { get; set; }
    public long OrderCode { get; set; }
    public string CheckoutUrl { get; set; } = string.Empty;
    public string? PaymentLinkId { get; set; }
}
