using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IAdminService
{
    Task<IEnumerable<NurseProfileDetailDto>> GetPendingNursesAsync();
    Task<NurseProfileDetailDto?> GetNurseDetailsAsync(int userId);
    Task<bool> ReviewNurseAsync(int userId, ReviewNurseProfileDto reviewDto);
}
