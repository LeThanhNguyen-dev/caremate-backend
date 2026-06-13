namespace MomCare.Dto;

public class CarePlanRecommendRequest
{
    public Guid? HealthCheckInId { get; set; }
    public AnalyzeHealthCheckInRequest? CheckIn { get; set; }
    public GeoPointDto? UserLocation { get; set; }
}

public class GeoPointDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public class CarePlanResponse
{
    public Guid CarePlanId { get; set; }
    public string PlanType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SafetyLevel { get; set; } = "normal";
    public string? SafetyNotice { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<RecommendedCareServiceDto> RecommendedServices { get; set; } = [];
    public List<CarePlanTimelineItemDto> PlanItems { get; set; } = [];
    public List<NurseDiscoveryDto> RecommendedNurses { get; set; } = [];
    public string Disclaimer { get; set; } = string.Empty;
    public string? AiModel { get; set; }
    public bool FallbackMode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RecommendedCareServiceDto
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int? SessionCount { get; set; }
    public decimal EstimatedPrice { get; set; }
}

public class CarePlanTimelineItemDto
{
    public int SessionNumber { get; set; }
    public DateTime ScheduledDate { get; set; }
    public string Focus { get; set; } = string.Empty;
    public List<string> Activities { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
}

public class SafetyEvaluationDto
{
    public string SafetyLevel { get; set; } = "normal";
    public List<string> Triggers { get; set; } = [];
    public string? Notice { get; set; }
}
