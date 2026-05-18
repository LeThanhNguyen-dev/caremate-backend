namespace MomCare.Dto;

public class ServiceDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public string ServiceKind { get; set; } = "single";
    public int? PackageDays { get; set; }
    public string? IncludedServiceKeys { get; set; }
    public List<PackageScheduleEntryDto> PackageSchedule { get; set; } = new();
    public string Status { get; set; } = "active";
}

public class PackageScheduleEntryDto
{
    public int Day { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ServiceKeys { get; set; }
}
