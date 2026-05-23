namespace MomCare.Dto;

/// <summary>
/// Review information displayed alongside a nurse profile.
/// </summary>
public class ReviewDetailDto
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerAvatar { get; set; }
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? ServiceCategory { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
