using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IHealthCheckInService
{
    Task<HealthAnalysisResponse> AnalyzeAsync(int userId, AnalyzeHealthCheckInRequest request, CancellationToken cancellationToken);
    Task<LatestHealthCheckInDto?> GetLatestAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<HealthCheckInHistoryDto>> GetHistoryAsync(int userId, int page, int pageSize, CancellationToken cancellationToken);
}
