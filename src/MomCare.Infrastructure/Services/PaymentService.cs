using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class PaymentService : IPaymentService
{
    private readonly MomCareContext _context;
    private readonly INotificationService _notificationService;

    public PaymentService(MomCareContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<PaymentDto?> UpsertPaymentAsync(int actorUserId, bool isAdmin, int bookingId, UpdatePaymentStatusDto dto)
    {
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null)
        {
            return null;
        }

        if (!isAdmin && booking.CustomerId != actorUserId)
        {
            return null;
        }

        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (payment == null)
        {
            payment = new Payment
            {
                BookingId = bookingId,
                Amount = booking.TotalPrice,
                Method = dto.Method,
                Status = dto.Status,
                TransactionId = dto.TransactionId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
        }
        else
        {
            payment.Method = dto.Method;
            payment.Status = dto.Status;
            payment.TransactionId = dto.TransactionId;
        }

        await _context.SaveChangesAsync();

        var statusText = NotificationVietnameseText.PaymentStatus(payment.Status);
        await _notificationService.CreateAsync(booking.NurseId, "Cập nhật thanh toán", $"Thanh toán cho lịch hẹn #{bookingId} hiện {statusText}.", "payment");
        await _notificationService.CreateAsync(booking.CustomerId, "Cập nhật thanh toán", $"Thanh toán của bạn cho lịch hẹn #{bookingId} hiện {statusText}.", "payment");

        return MapPayment(payment);
    }

    private static PaymentDto MapPayment(Payment p) => new()
    {
        Id = p.Id,
        BookingId = p.BookingId,
        Amount = p.Amount,
        Method = p.Method,
        Status = p.Status,
        TransactionId = p.TransactionId,
        RefundAmount = p.RefundAmount,
        RefundReason = p.RefundReason,
        RefundStatus = p.RefundStatus,
        CreatedAt = p.CreatedAt,
        RefundedAt = p.RefundedAt
    };
}
