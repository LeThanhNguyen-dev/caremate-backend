namespace MomCare.Dto;

public class CancelBookingDto
{
    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
}
