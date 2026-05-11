namespace MomCare.Dto;

public class BookingDetailDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int NurseId { get; set; }
    public int ServiceId { get; set; }
    public int? AvailabilitySlotId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? NurseName { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
