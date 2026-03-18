using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IReviewService
{
    Task<bool> CreateReviewAsync(int customerId, CreateReviewDto dto);
}
