namespace MomCare.Dto;

public class AdminBookingSummaryDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int NurseId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
