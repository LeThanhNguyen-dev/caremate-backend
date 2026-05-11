using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IAdminService
{
    Task<IEnumerable<NurseProfileDetailDto>> GetPendingNursesAsync();
    Task<NurseProfileDetailDto?> GetNurseDetailsAsync(int userId);
    Task<bool> ReviewNurseAsync(int userId, ReviewNurseProfileDto reviewDto);
    Task<AdminDashboardDto> GetDashboardAsync();
    Task<IEnumerable<AdminBookingSummaryDto>> GetBookingsAsync(string? status);
    Task<IEnumerable<DisputeDto>> GetDisputesAsync(string? status);
}
