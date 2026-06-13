using MomCare.Dto;

namespace MomCare.Interfaces;

public interface ICarePlanService
{
    Task<ServiceResult<CarePlanResponse>> RecommendAsync(int userId, CarePlanRecommendRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<CarePlanResponse>> GenerateForBookingAsync(int actorUserId, bool isAdmin, int bookingId, CancellationToken cancellationToken);
    Task<ServiceResult<CarePlanResponse>> GetForBookingAsync(int actorUserId, bool isAdmin, int bookingId, CancellationToken cancellationToken);
}
