using MomCare.Dto;
using MomCare.Models;

namespace MomCare.Interfaces;

public interface IPaymentService
{
    Task<Payment?> UpsertPaymentAsync(int actorUserId, bool isAdmin, int bookingId, UpdatePaymentStatusDto dto);
}
