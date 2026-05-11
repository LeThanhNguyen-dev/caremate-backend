using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IPaymentService
{
    Task<PaymentDto?> UpsertPaymentAsync(int actorUserId, bool isAdmin, int bookingId, UpdatePaymentStatusDto dto);
}
