namespace MomCare.Dto;

public class UpdatePaymentStatusDto
{
    public string Method { get; set; } = "bank_transfer";
    public string Status { get; set; } = "initiated";
    public string? TransactionId { get; set; }
}
