using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class BookingService : IBookingService
{
    private readonly MomCareContext _context;
    private readonly INotificationService _notificationService;

    public BookingService(MomCareContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<BookingDetailDto?> CreateBookingAsync(int customerId, CreateBookingDto dto)
    {
        if (dto.EndTime <= dto.StartTime)
        {
            return null;
        }

        var nurseProfile = await _context.NurseProfiles
            .Include(np => np.NurseServices)
            .FirstOrDefaultAsync(np => np.UserId == dto.NurseId && np.IsActive && np.IsVerified == "verified");

        if (nurseProfile == null)
        {
            return null;
        }

        var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == dto.ServiceId && s.Status == "active");
        if (service == null)
        {
            return null;
        }

        var hasRequiredService = nurseProfile.NurseServices.Any(ns => ns.ServiceId == dto.ServiceId && ns.Status == "enabled");
        if (!hasRequiredService)
        {
            return null;
        }

        var hasAvailability = await _context.AvailabilitySlots.AnyAsync(a =>
            a.NurseProfileId == nurseProfile.Id &&
            !a.IsBooked &&
            a.StartTime <= dto.StartTime &&
            a.EndTime >= dto.EndTime);

        if (!hasAvailability)
        {
            return null;
        }

        var overlap = await _context.Bookings.AnyAsync(b =>
            b.NurseId == dto.NurseId &&
            b.Status != BookingStatuses.Cancelled &&
            b.Status != BookingStatuses.Rejected &&
            dto.StartTime < b.EndTime &&
            dto.EndTime > b.StartTime);

        if (overlap)
        {
            return null;
        }

        var nurseService = nurseProfile.NurseServices.First(ns => ns.ServiceId == dto.ServiceId);
        var totalPrice = nurseService.Unit == "hourly"
            ? nurseService.Price * (decimal)(dto.EndTime - dto.StartTime).TotalHours
            : nurseService.Price;

        var booking = new Booking
        {
            CustomerId = customerId,
            NurseId = dto.NurseId,
            ServiceId = dto.ServiceId,
            Status = BookingStatuses.PendingConfirm,
            TotalPrice = totalPrice,
            Address = dto.Address,
            Notes = dto.Notes,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        _context.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            Status = booking.Status,
            ChangedBy = customerId,
            Note = "Booking created",
            CreatedAt = DateTime.UtcNow
        });

        var coveringSlots = await _context.AvailabilitySlots
            .Where(a => a.NurseProfileId == nurseProfile.Id && !a.IsBooked && a.StartTime <= dto.StartTime && a.EndTime >= dto.EndTime)
            .ToListAsync();

        foreach (var slot in coveringSlots)
        {
            slot.IsBooked = true;
        }

        await _context.SaveChangesAsync();

        await _notificationService.CreateAsync(dto.NurseId, "New booking request", $"Booking #{booking.Id} is waiting for your confirmation.");

        return new BookingDetailDto
        {
            Id = booking.Id,
            CustomerId = booking.CustomerId,
            NurseId = booking.NurseId,
            ServiceId = booking.ServiceId,
            ServiceName = service.Name,
            Status = booking.Status,
            TotalPrice = booking.TotalPrice,
            StartTime = booking.StartTime,
            EndTime = booking.EndTime,
            Address = booking.Address,
            Notes = booking.Notes
        };
    }

    public async Task<IEnumerable<BookingDetailDto>> GetCustomerBookingsAsync(int customerId)
    {
        return await _context.Bookings
            .Include(b => b.Service)
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookingDetailDto
            {
                Id = b.Id,
                CustomerId = b.CustomerId,
                NurseId = b.NurseId,
                ServiceId = b.ServiceId,
                ServiceName = b.Service.Name,
                Status = b.Status,
                TotalPrice = b.TotalPrice,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                Address = b.Address,
                Notes = b.Notes
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<BookingDetailDto>> GetNurseBookingsAsync(int nurseId)
    {
        return await _context.Bookings
            .Include(b => b.Service)
            .Where(b => b.NurseId == nurseId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookingDetailDto
            {
                Id = b.Id,
                CustomerId = b.CustomerId,
                NurseId = b.NurseId,
                ServiceId = b.ServiceId,
                ServiceName = b.Service.Name,
                Status = b.Status,
                TotalPrice = b.TotalPrice,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                Address = b.Address,
                Notes = b.Notes
            })
            .ToListAsync();
    }

    public async Task<BookingDetailDto?> GetBookingDetailAsync(int actorUserId, int bookingId, bool isAdmin)
    {
        var booking = await _context.Bookings
            .Include(b => b.Service)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            return null;
        }

        if (!isAdmin && booking.CustomerId != actorUserId && booking.NurseId != actorUserId)
        {
            return null;
        }

        return new BookingDetailDto
        {
            Id = booking.Id,
            CustomerId = booking.CustomerId,
            NurseId = booking.NurseId,
            ServiceId = booking.ServiceId,
            ServiceName = booking.Service.Name,
            Status = booking.Status,
            TotalPrice = booking.TotalPrice,
            StartTime = booking.StartTime,
            EndTime = booking.EndTime,
            Address = booking.Address,
            Notes = booking.Notes
        };
    }

    public async Task<bool> UpdateBookingStatusAsync(int actorUserId, bool isAdmin, UpdateBookingStatusDto dto, int bookingId)
    {
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null)
        {
            return false;
        }

        if (!isAdmin && booking.CustomerId != actorUserId && booking.NurseId != actorUserId)
        {
            return false;
        }

        var nextStatus = dto.Status.Trim().ToLowerInvariant();
        if (!IsTransitionAllowed(booking, actorUserId, isAdmin, nextStatus))
        {
            return false;
        }

        booking.Status = nextStatus;
        booking.UpdatedAt = DateTime.UtcNow;

        _context.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            Status = nextStatus,
            ChangedBy = actorUserId,
            Note = dto.Note,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        await _notificationService.CreateAsync(
            booking.CustomerId == actorUserId ? booking.NurseId : booking.CustomerId,
            "Booking status updated",
            $"Booking #{booking.Id} changed to '{nextStatus}'.");

        return true;
    }

    private static bool IsTransitionAllowed(Booking booking, int actorUserId, bool isAdmin, string nextStatus)
    {
        if (isAdmin)
        {
            return true;
        }

        var isCustomer = booking.CustomerId == actorUserId;
        var isNurse = booking.NurseId == actorUserId;

        if (isCustomer)
        {
            return nextStatus == BookingStatuses.Cancelled &&
                   (booking.Status == BookingStatuses.PendingConfirm || booking.Status == BookingStatuses.Confirmed);
        }

        if (isNurse)
        {
            if (booking.Status == BookingStatuses.PendingConfirm)
            {
                return nextStatus is BookingStatuses.Confirmed or BookingStatuses.Rejected;
            }

            if (booking.Status == BookingStatuses.Confirmed)
            {
                return nextStatus == BookingStatuses.InProgress;
            }

            if (booking.Status == BookingStatuses.InProgress)
            {
                return nextStatus == BookingStatuses.Completed;
            }
        }

        return false;
    }

    public async Task<bool> CancelBookingAsync(int actorUserId, int bookingId, CancelBookingDto dto)
    {
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null)
        {
            return false;
        }

        // Only customer or admin can cancel - for now just check if customer
        if (booking.CustomerId != actorUserId)
        {
            return false;
        }

        // Can only cancel if pending or confirmed
        if (booking.Status != BookingStatuses.PendingConfirm && booking.Status != BookingStatuses.Confirmed)
        {
            return false;
        }

        // Calculate refund based on cancellation window
        decimal refundAmount = CalculateRefundAmount(booking);

        // Mark booking as cancelled
        booking.Status = BookingStatuses.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;

        _context.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            Status = BookingStatuses.Cancelled,
            ChangedBy = actorUserId,
            Note = dto.Note ?? dto.Reason,
            CreatedAt = DateTime.UtcNow
        });

        // Update or create payment with refund info
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (payment != null)
        {
            payment.RefundAmount = refundAmount;
            payment.RefundReason = dto.Reason;
            payment.RefundStatus = "pending";
        }

        // Release availability slots
        var nurseProfile = await _context.NurseProfiles
            .FirstOrDefaultAsync(np => np.UserId == booking.NurseId);

        if (nurseProfile != null)
        {
            var slots = await _context.AvailabilitySlots
                .Where(s => s.NurseProfileId == nurseProfile.Id &&
                            s.IsBooked &&
                            s.StartTime >= booking.StartTime &&
                            s.EndTime <= booking.EndTime)
                .ToListAsync();

            foreach (var slot in slots)
            {
                slot.IsBooked = false;
            }
        }

        await _context.SaveChangesAsync();

        // Send notifications
        await _notificationService.CreateAsync(booking.NurseId, "Booking cancelled",
            $"Booking #{booking.Id} has been cancelled. Refund: {refundAmount}");
        await _notificationService.CreateAsync(booking.CustomerId, "Booking cancelled",
            $"Your booking #{booking.Id} has been cancelled. Refund: {refundAmount}");

        return true;
    }

    private decimal CalculateRefundAmount(Booking booking)
    {
        var hoursUntilStart = (booking.StartTime - DateTime.UtcNow).TotalHours;

        // If cancelled more than 24 hours before: full refund
        if (hoursUntilStart >= 24)
        {
            return booking.TotalPrice;
        }

        // If cancelled less than 24 hours before: 50% refund
        if (hoursUntilStart >= 0)
        {
            return booking.TotalPrice * 0.5m;
        }

        // If cancelled after service time: no refund
        return 0;
    }
}
