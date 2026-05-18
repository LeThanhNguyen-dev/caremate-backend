namespace MomCare.Dto;

public class PackageSessionDto
{
    public int Id { get; set; }
    public int SessionNumber { get; set; }
    public DateTime SessionDate { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? PlannedServiceKeys { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? NurseNote { get; set; }
}

public class PackageProgressDto
{
    public int BookingId { get; set; }
    public int TotalSessions { get; set; }
    public int CompletedSessions { get; set; }
    public double ProgressPercent { get; set; }
    public PackageSessionDto? TodaySession { get; set; }
    public List<PackageSessionDto> Sessions { get; set; } = new();
}

public class CheckInSessionDto
{
    public string? NurseNote { get; set; }
}

public class CheckOutSessionDto
{
    public string? NurseNote { get; set; }
}
