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

    public async Task<Payment?> UpsertPaymentAsync(int actorUserId, bool isAdmin, int bookingId, UpdatePaymentStatusDto dto)
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

        await _notificationService.CreateAsync(booking.NurseId, "Payment updated", $"Payment for booking #{bookingId} is now '{payment.Status}'.", "payment");
        await _notificationService.CreateAsync(booking.CustomerId, "Payment updated", $"Your payment for booking #{bookingId} is now '{payment.Status}'.", "payment");

        return payment;
    }
}
