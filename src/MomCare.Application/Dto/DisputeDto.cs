namespace MomCare.Dto;

public class DisputeDto
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }
}
