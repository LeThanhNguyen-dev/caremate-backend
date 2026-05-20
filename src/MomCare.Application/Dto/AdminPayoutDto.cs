namespace MomCare.Dto;

public class AdminPayoutDto
{
    public int PayoutId { get; set; }
    public int BookingId { get; set; }
    public int NurseId { get; set; }
    public string NurseName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal PlatformFee { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? NurseBankBin { get; set; }
    public string? NurseBankAccountNumber { get; set; }
    public string? NurseBankAccountName { get; set; }
    public string? NurseQrUrl { get; set; }
}

public class CompletePayoutDto
{
    public string? AdminNote { get; set; }
}
