using MomCare.Dto;

namespace MomCare.Interfaces;

public interface INurseDiscoveryService
{
    Task<IEnumerable<NurseDiscoveryDto>> SearchAsync(
        int? serviceId,
        decimal? minPrice,
        decimal? maxPrice,
        DateTime? startTime,
        DateTime? endTime);

    Task<NurseProfileDetailDto?> GetDetailAsync(int userId);
}
