using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IAdminAiInsightService
{
    Task<ServiceResult<AdminAiInsightResponse>> GenerateAsync(AdminAiInsightRequest request, CancellationToken cancellationToken);
}
