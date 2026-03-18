namespace MomCare.Dto;

public class UpsertServiceDto
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public string Status { get; set; } = "active";
}
