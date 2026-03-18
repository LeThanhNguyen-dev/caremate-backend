using MomCare.Dto;

namespace MomCare.Interfaces;

public interface INurseServiceManagementService
{
    Task<NurseServiceDto?> AddServiceAsync(int nurseUserId, CreateNurseServiceDto dto);
    Task<IEnumerable<NurseServiceDto>> GetMyServicesAsync(int nurseUserId);
    Task<NurseServiceDto?> UpdateServiceAsync(int nurseUserId, int serviceId, UpdateNurseServiceDto dto);
    Task<bool> RemoveServiceAsync(int nurseUserId, int serviceId);
}
