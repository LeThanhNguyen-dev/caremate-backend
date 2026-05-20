using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IPaymentService
{
    Task<PaymentDto?> UpsertPaymentAsync(int actorUserId, bool isAdmin, int bookingId, UpdatePaymentStatusDto dto);
    Task<PayOSPaymentLinkDto?> CreatePayOSPaymentLinkAsync(int actorUserId, bool isAdmin, int bookingId, CreatePayOSPaymentLinkDto dto);
    Task<PayOSPaymentLinkDto> CreatePayOSBookingPaymentLinkAsync(int actorUserId, CreatePayOSBookingPaymentDto dto);
    Task<bool> HandlePayOSWebhookAsync(PayOSWebhookDto webhook);
}
