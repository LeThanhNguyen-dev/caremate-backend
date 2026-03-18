namespace MomCare.Dto;

public class UpdateBookingStatusDto
{
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
}
