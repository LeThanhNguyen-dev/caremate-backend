using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IDisputeService
{
    Task<DisputeDto?> CreateAsync(int actorUserId, CreateDisputeDto dto);
    Task<IEnumerable<DisputeDto>> GetDisputesAsync(int actorUserId, bool isAdmin);
    Task<bool> UpdateStatusAsync(int disputeId, UpdateDisputeStatusDto dto);
}
