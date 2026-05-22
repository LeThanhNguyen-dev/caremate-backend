using MomCare.Dto;
using MomCare.Models;

namespace MomCare.Interfaces;

public interface IOpenAiHealthAnalysisService
{
    Task<HealthAnalysisResult> AnalyzeAsync(
        HealthCheckIn currentCheckIn,
        List<HealthCheckIn> recentHistory,
        IReadOnlyList<SuggestedServiceDto> availableServices,
        CancellationToken cancellationToken);
}
