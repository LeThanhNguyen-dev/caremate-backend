namespace MomCare.Dto;

public class AdminRefundDto
{
    public int BookingId { get; set; }
    public string BookingStatus { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int NurseId { get; set; }
    public string NurseName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public decimal RefundAmount { get; set; }
    public bool HasPayment { get; set; }
    public string? RefundReason { get; set; }
    public string? RefundStatus { get; set; }
    public string? CustomerBankBin { get; set; }
    public string? CustomerBankAccountNumber { get; set; }
    public string? CustomerBankAccountName { get; set; }
    public string? CustomerQrUrl { get; set; }
}

public class CompleteRefundDto
{
    public string? AdminNote { get; set; }
}
