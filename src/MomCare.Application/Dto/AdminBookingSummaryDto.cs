namespace MomCare.Dto;

public class AdminBookingSummaryDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int NurseId { get; set; }
    public string NurseName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public decimal PlatformFee { get; set; }
    public decimal NursePayoutAmount { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
