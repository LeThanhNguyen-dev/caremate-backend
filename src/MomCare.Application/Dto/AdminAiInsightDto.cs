namespace MomCare.Dto;

public static class AdminAiInsightUseCases
{
    public const string PersonalizedCarePlan = "personalized_care_plan";
    public const string HealthSummary = "health_summary";
    public const string ServiceOptimization = "service_optimization";

    public static readonly string[] All =
    [
        PersonalizedCarePlan,
        HealthSummary,
        ServiceOptimization
    ];
}

public class AdminAiInsightRequest
{
    public string UseCase { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public int? BookingId { get; set; }
    public Guid? HealthCheckInId { get; set; }
    public AdminAiInsightDateRangeDto? DateRange { get; set; }
}

public class AdminAiInsightDateRangeDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class AdminAiInsightResponse
{
    public string UseCase { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> Insights { get; set; } = [];
    public List<AdminAiInsightActionDto> RecommendedActions { get; set; } = [];
    public List<AdminAiInsightMetricDto> Metrics { get; set; } = [];
    public List<AdminAiInsightEntityDto> RelatedEntities { get; set; } = [];
    public string Disclaimer { get; set; } = string.Empty;
    public string? AiModel { get; set; }
    public bool FallbackMode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminAiInsightActionDto
{
    public string Label { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int Priority { get; set; }
}

public class AdminAiInsightMetricDto
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Trend { get; set; }
    public string? Note { get; set; }
}

public class AdminAiInsightEntityDto
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
