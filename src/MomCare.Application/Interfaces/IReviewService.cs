using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IReviewService
{
    Task<bool> CreateReviewAsync(int customerId, CreateReviewDto dto);
    Task<bool> UpdateReviewAsync(int customerId, int reviewId, UpdateReviewDto dto);
    Task<bool> DeleteReviewAsync(int customerId, int reviewId);
    Task<IEnumerable<ReviewDetailDto>> GetNurseReviewsAsync(int nurseUserId, int page = 1, int pageSize = 10);
    Task<NurseRatingDto> GetNurseRatingAsync(int nurseUserId);
}
