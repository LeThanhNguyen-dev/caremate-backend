using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;
using System.Data;

namespace MomCare.Services;

public class BookingService : IBookingService
{
    private readonly MomCareContext _context;
    private readonly INotificationService _notificationService;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private const int MaxRetryCount = 3;

    public BookingService(
        MomCareContext context,
        INotificationService notificationService,
        IRealtimeNotifier realtimeNotifier)
    {
        _context = context;
        _notificationService = notificationService;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<BookingDetailDto?> CreateBookingAsync(int customerId, CreateBookingDto dto)
    {
        if (dto.EndTime <= dto.StartTime)
        {
            return null;
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var nurseProfile = await _context.NurseProfiles
                    .Include(np => np.NurseServices)
                    .FirstOrDefaultAsync(np => np.UserId == dto.NurseId && np.IsActive && np.IsVerified == "verified");

                if (nurseProfile == null) return null;

                var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == dto.ServiceId && s.Status == "active");
                if (service == null) return null;

                var hasRequiredService = nurseProfile.NurseServices.Any(ns => ns.ServiceId == dto.ServiceId && ns.Status == "enabled");
                if (!hasRequiredService) return null;

                // 1. Verify slot exists for this nurse
                var slot = await _context.AvailabilitySlots
                    .FirstOrDefaultAsync(a => a.Id == dto.AvailabilitySlotId && a.NurseProfileId == nurseProfile.Id);

                if (slot == null) return null;

                // 2. Verify requested time is WITHIN the slot
                if (dto.StartTime < slot.StartTime || dto.EndTime > slot.EndTime)
                {
                    return null;
                }

                // 3. Verify NO EXISTING BOOKING for this slot (one booking per slot)
                var slotIsTaken = await _context.Bookings.AnyAsync(b => 
                    b.AvailabilitySlotId == dto.AvailabilitySlotId && 
                    b.Status != BookingStatuses.Cancelled && 
                    b.Status != BookingStatuses.Rejected);

                if (slotIsTaken) return null;

                // 4. Overlap validation: Prevent booking if time overlaps with ANY existing booking for this nurse
                // (newStart < existingEnd && newEnd > existingStart)
                var overlap = await _context.Bookings.AnyAsync(b =>
                    b.NurseId == dto.NurseId &&
                    b.Status != BookingStatuses.Cancelled &&
                    b.Status != BookingStatuses.Rejected &&
                    dto.StartTime < b.EndTime &&
                    dto.EndTime > b.StartTime);

                if (overlap) return null;

                var nurseService = nurseProfile.NurseServices.First(ns => ns.ServiceId == dto.ServiceId);
                var totalPrice = nurseService.Unit == "hourly"
                    ? nurseService.Price * (decimal)(dto.EndTime - dto.StartTime).TotalHours
                    : nurseService.Price;

                var booking = new Booking
                {
                    CustomerId = customerId,
                    NurseId = dto.NurseId,
                    ServiceId = dto.ServiceId,
                    AvailabilitySlotId = dto.AvailabilitySlotId,
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

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var bookingDetail = new BookingDetailDto
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
                    Notes = booking.Notes,
                    AvailabilitySlotId = booking.AvailabilitySlotId
                };

                await _notificationService.CreateAsync(dto.NurseId, "Yêu cầu đặt lịch mới", $"Lịch hẹn #{booking.Id} đang chờ bạn xác nhận.");
                await _realtimeNotifier.NotifyBookingCreatedAsync(dto.NurseId, bookingDetail);

                return bookingDetail;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw; // Strategy will handle retry if it's a transient failure
            }
        });
    }

    public async Task<IEnumerable<BookingDetailDto>> GetCustomerBookingsAsync(int customerId)
    {
        return await _context.Bookings
            .Include(b => b.Service)
            .Include(b => b.Nurse)
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookingDetailDto
            {
                Id = b.Id,
                CustomerId = b.CustomerId,
                NurseId = b.NurseId,
                ServiceId = b.ServiceId,
                ServiceName = b.Service.Name,
                NurseName = b.Nurse.FullName,
                Status = b.Status,
                TotalPrice = b.TotalPrice,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                Address = b.Address,
                Notes = b.Notes,
                AvailabilitySlotId = b.AvailabilitySlotId
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<BookingDetailDto>> GetNurseBookingsAsync(int nurseId)
    {
        return await _context.Bookings
            .Include(b => b.Service)
            .Include(b => b.Nurse)
            .Where(b => b.NurseId == nurseId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookingDetailDto
            {
                Id = b.Id,
                CustomerId = b.CustomerId,
                NurseId = b.NurseId,
                ServiceId = b.ServiceId,
                ServiceName = b.Service.Name,
                NurseName = b.Nurse.FullName,
                Status = b.Status,
                TotalPrice = b.TotalPrice,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                Address = b.Address,
                Notes = b.Notes,
                AvailabilitySlotId = b.AvailabilitySlotId
            })
            .ToListAsync();
    }

    public async Task<BookingDetailDto?> GetBookingDetailAsync(int actorUserId, int bookingId, bool isAdmin)
    {
        var booking = await _context.Bookings
            .Include(b => b.Service)
            .Include(b => b.Nurse)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null) return null;

        if (!isAdmin && booking.CustomerId != actorUserId && booking.NurseId != actorUserId) return null;

        return new BookingDetailDto
        {
            Id = booking.Id,
            CustomerId = booking.CustomerId,
            NurseId = booking.NurseId,
            ServiceId = booking.ServiceId,
            ServiceName = booking.Service.Name,
            NurseName = booking.Nurse.FullName,
            Status = booking.Status,
            TotalPrice = booking.TotalPrice,
            StartTime = booking.StartTime,
            EndTime = booking.EndTime,
            Address = booking.Address,
            Notes = booking.Notes,
            AvailabilitySlotId = booking.AvailabilitySlotId
        };
    }

    public async Task<bool> UpdateBookingStatusAsync(int actorUserId, bool isAdmin, UpdateBookingStatusDto dto, int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.Service)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null) return false;

        if (!isAdmin && booking.CustomerId != actorUserId && booking.NurseId != actorUserId) return false;

        var nextStatus = dto.Status.Trim().ToLowerInvariant();
        if (!IsTransitionAllowed(booking, actorUserId, isAdmin, nextStatus)) return false;

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

        var targetUserId = booking.CustomerId == actorUserId ? booking.NurseId : booking.CustomerId;

        await _notificationService.CreateAsync(
            targetUserId,
            "Cập nhật trạng thái lịch hẹn",
            $"Lịch hẹn #{booking.Id} đã chuyển sang trạng thái {NotificationVietnameseText.BookingStatus(nextStatus)}.");

        var bookingDetail = new BookingDetailDto
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
            Notes = booking.Notes,
            AvailabilitySlotId = booking.AvailabilitySlotId
        };

        await _realtimeNotifier.NotifyBookingStatusChangedAsync(targetUserId, bookingDetail);

        return true;
    }

    private static bool IsTransitionAllowed(Booking booking, int actorUserId, bool isAdmin, string nextStatus)
    {
        if (isAdmin) return true;

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

    public async Task<bool> CancelBookingAsync(int actorUserId, bool isAdmin, int bookingId, CancelBookingDto dto)
    {
        var booking = await _context.Bookings
            .Include(b => b.Service)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null) return false;

        if (!isAdmin && booking.CustomerId != actorUserId) return false;

        if (booking.Status != BookingStatuses.PendingConfirm && booking.Status != BookingStatuses.Confirmed) return false;

        decimal refundAmount = CalculateRefundAmount(booking);

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

        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (payment != null)
        {
            payment.RefundAmount = refundAmount;
            payment.RefundReason = dto.Reason;
            payment.RefundStatus = "pending";
        }

        await _context.SaveChangesAsync();

        await _notificationService.CreateAsync(booking.NurseId, "Lịch hẹn đã bị hủy",
            $"Lịch hẹn #{booking.Id} đã bị hủy. Số tiền hoàn dự kiến: {refundAmount:N0}.");
        await _notificationService.CreateAsync(booking.CustomerId, "Lịch hẹn đã bị hủy",
            $"Lịch hẹn #{booking.Id} của bạn đã bị hủy. Số tiền hoàn dự kiến: {refundAmount:N0}.");

        var bookingDetail = new BookingDetailDto
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
            Notes = booking.Notes,
            AvailabilitySlotId = booking.AvailabilitySlotId
        };

        await _realtimeNotifier.NotifyBookingStatusChangedAsync(booking.NurseId, bookingDetail);
        await _realtimeNotifier.NotifyBookingStatusChangedAsync(booking.CustomerId, bookingDetail);
        await _realtimeNotifier.NotifyAvailabilityChangedAsync(booking.NurseId);

        return true;
    }

    private decimal CalculateRefundAmount(Booking booking)
    {
        var hoursUntilStart = (booking.StartTime - DateTime.UtcNow).TotalHours;

        if (hoursUntilStart >= 24) return booking.TotalPrice;
        if (hoursUntilStart >= 0) return booking.TotalPrice * 0.5m;
        return 0;
    }
}
