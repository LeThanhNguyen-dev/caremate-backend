using MomCare.Dto;
using MomCare.Models;

namespace MomCare.Interfaces;

public interface IDisputeService
{
    Task<Dispute?> CreateAsync(int actorUserId, CreateDisputeDto dto);
    Task<IEnumerable<Dispute>> GetDisputesAsync(int actorUserId, bool isAdmin);
    Task<bool> UpdateStatusAsync(int disputeId, UpdateDisputeStatusDto dto);
}
