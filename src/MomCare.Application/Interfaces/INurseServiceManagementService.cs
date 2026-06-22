using MomCare.Dto;

namespace MomCare.Interfaces;

public interface INurseServiceManagementService
{
    Task<NurseServiceDto?> AddServiceAsync(int nurseUserId, CreateNurseServiceDto dto, string? language = null);
    Task<IEnumerable<NurseServiceDto>> GetMyServicesAsync(int nurseUserId, string? language = null);
    Task<NurseServiceDto?> UpdateServiceAsync(int nurseUserId, int serviceId, UpdateNurseServiceDto dto, string? language = null);
    Task<bool> RemoveServiceAsync(int nurseUserId, int serviceId);
}
