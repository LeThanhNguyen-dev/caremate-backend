using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IAvailabilityService
{
    Task<IEnumerable<AvailabilitySlotDto>> GetNurseSlotsAsync(int nurseUserId, DateTime? from, DateTime? to);
    Task<IEnumerable<AvailabilitySlotDto>> GetMySlotsAsync(int nurseUserId, DateTime? from, DateTime? to);
    Task<AvailabilitySlotDto?> CreateSlotAsync(int nurseUserId, CreateAvailabilitySlotDto dto);
    Task<bool> DeleteSlotAsync(int nurseUserId, int slotId);
}
