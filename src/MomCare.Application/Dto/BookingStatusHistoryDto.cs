namespace MomCare.Dto;

public class BookingStatusHistoryDto
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ChangedBy { get; set; }
    public string? ChangedByName { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
