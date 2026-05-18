namespace MomCare.Dto;

public class UpsertServiceDto
{
    public required string Name { get; set; }
    public required string Category { get; set; }
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public string ServiceKind { get; set; } = "single";
    public int? PackageDays { get; set; }
    public string? IncludedServiceKeys { get; set; }
    public string? PackageScheduleJson { get; set; }
    public string Status { get; set; } = "active";
}
