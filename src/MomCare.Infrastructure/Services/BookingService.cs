using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;
using System.Data;
using System.Text.Json;
using NurseServiceModel = MomCare.Models.NurseService;

namespace MomCare.Services;

public class BookingService : IBookingService
{
    private readonly MomCareContext _context;
    private readonly INotificationService _notificationService;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private const int MaxRetryCount = 3;
    private const decimal PlatformFeeRate = 0.15m;
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public BookingService(
        MomCareContext context,
        INotificationService notificationService,
        IRealtimeNotifier realtimeNotifier)
    {
        _context = context;
        _notificationService = notificationService;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<ServiceResult<BookingDetailDto>> CreateBookingAsync(int customerId, CreateBookingDto dto)
    {
        // Validate nurse
        var nurseProfile = await _context.NurseProfiles
            .Include(np => np.NurseServices)
            .FirstOrDefaultAsync(np => np.UserId == dto.NurseId && np.IsActive && np.IsVerified == "verified");

        if (nurseProfile == null)
            return ServiceResult<BookingDetailDto>.Fail("Nurse không tồn tại hoặc chưa được xác minh.");

        // Validate service
        var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == dto.ServiceId && s.Status == "active");
        if (service == null)
            return ServiceResult<BookingDetailDto>.Fail("Dịch vụ không tồn tại hoặc đã ngừng cung cấp.");

        // Validate nurse offers this service
        var nurseService = nurseProfile.NurseServices.FirstOrDefault(ns => ns.ServiceId == dto.ServiceId && ns.Status == "enabled");
        if (nurseService == null)
            return ServiceResult<BookingDetailDto>.Fail("Nurse không cung cấp dịch vụ này.");

        // Branch by service kind
        if (service.ServiceKind == "package")
        {
            return await CreatePackageBookingAsync(customerId, dto, nurseProfile, service, nurseService);
        }
        else
        {
            return await CreateSingleBookingAsync(customerId, dto, nurseProfile, service, nurseService);
        }
    }

    /// <summary>
    /// Creates a booking for a single (one-time) service.
    /// Requires an AvailabilitySlot and explicit EndTime.
    /// </summary>
    private async Task<ServiceResult<BookingDetailDto>> CreateSingleBookingAsync(
        int customerId,
        CreateBookingDto dto,
        NurseProfile nurseProfile,
        Service service,
        NurseServiceModel nurseService)
    {
        if (!dto.AvailabilitySlotId.HasValue)
            return ServiceResult<BookingDetailDto>.Fail("Dịch vụ lẻ yêu cầu chọn khung giờ (AvailabilitySlotId).");

        var requestedStartTime = NormalizeDateTime(dto.StartTime);
        var requestedEndTime = requestedStartTime.AddMinutes(Math.Max(service.EstimatedDurationMinutes, 1));

        if (requestedEndTime <= requestedStartTime)
            return ServiceResult<BookingDetailDto>.Fail("Thời gian kết thúc phải sau thời gian bắt đầu.");

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // 1. Verify slot exists for this nurse
                var slot = await _context.AvailabilitySlots
                    .FirstOrDefaultAsync(a => a.Id == dto.AvailabilitySlotId.Value && a.NurseProfileId == nurseProfile.Id);

                if (slot == null)
                    return ServiceResult<BookingDetailDto>.Fail("Khung giờ không tồn tại cho nurse này.");

                // 2. Verify requested time is WITHIN the slot
                if (requestedStartTime < slot.StartTime || requestedEndTime > slot.EndTime)
                    return ServiceResult<BookingDetailDto>.Fail("Thời gian đặt lịch nằm ngoài khung giờ trống của nurse.");

                // 3. Overlap validation
                var overlap = await _context.Bookings.AnyAsync(b =>
                    b.NurseId == dto.NurseId &&
                    b.Status != BookingStatuses.Cancelled &&
                    b.Status != BookingStatuses.Rejected &&
                    b.Service.ServiceKind != "package" &&
                    requestedStartTime < b.EndTime &&
                    requestedEndTime > b.StartTime);

                if (overlap)
                    return ServiceResult<BookingDetailDto>.Fail("Thời gian bị trùng với lịch hẹn khác của nurse.");

                if (await HasPackageSessionOverlapAsync(dto.NurseId, requestedStartTime, requestedEndTime))
                    return ServiceResult<BookingDetailDto>.Fail("Thời gian bị trùng với buổi chăm sóc trong gói dịch vụ khác của nurse.");

                // Calculate price
                var totalPrice = nurseService.Unit == "hourly"
                    ? nurseService.Price * (decimal)(requestedEndTime - requestedStartTime).TotalHours
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
                    StartTime = requestedStartTime,
                    EndTime = requestedEndTime,
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

                var bookingDetail = MapToDetailDto(booking, service);

                await _notificationService.CreateAsync(dto.NurseId, "Yêu cầu đặt lịch mới", $"Lịch hẹn #{booking.Id} đang chờ bạn xác nhận.");
                await _realtimeNotifier.NotifyBookingCreatedAsync(dto.NurseId, bookingDetail);

                return ServiceResult<BookingDetailDto>.Ok(bookingDetail);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw; // Strategy will handle retry if it's a transient failure
            }
        });
    }

    /// <summary>
    /// Creates a booking for a multi-day package service.
    /// AvailabilitySlot is optional; EndTime is auto-calculated from PackageDays.
    /// </summary>
    private async Task<ServiceResult<BookingDetailDto>> CreatePackageBookingAsync(
        int customerId,
        CreateBookingDto dto,
        NurseProfile nurseProfile,
        Service service,
        NurseServiceModel nurseService)
    {
        if (!service.PackageDays.HasValue || service.PackageDays.Value <= 0)
            return ServiceResult<BookingDetailDto>.Fail("Gói dịch vụ không hợp lệ (thiếu số ngày).");

        var sessionDurationMinutes = Math.Max(service.EstimatedDurationMinutes, 1);
        var sessionStarts = dto.PackageSessionStartTimes?.Count > 0
            ? dto.PackageSessionStartTimes
            : BuildPackageSessionTimes(dto.StartTime, service.PackageDays.Value, sessionDurationMinutes).Select(session => session.Start).ToList();

        if (sessionStarts.Count != service.PackageDays.Value)
            return ServiceResult<BookingDetailDto>.Fail($"Gói dịch vụ yêu cầu chọn đủ {service.PackageDays.Value} buổi chăm sóc.");

        if (sessionStarts.Distinct().Count() != sessionStarts.Count)
            return ServiceResult<BookingDetailDto>.Fail("Danh sách buổi chăm sóc trong gói bị trùng thời gian.");

        sessionStarts = sessionStarts.OrderBy(value => value).ToList();
        var candidateSessions = sessionStarts
            .Select(start => new SessionTimeRange(start, start.AddMinutes(sessionDurationMinutes)))
            .ToList();
        var endTime = candidateSessions.Last().End;

        if (dto.StartTime < DateTime.UtcNow.AddHours(-1))
            return ServiceResult<BookingDetailDto>.Fail("Ngày bắt đầu gói dịch vụ phải từ hôm nay trở đi.");

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // Check: nurse doesn't have an active package booking overlapping this period
                var packageOverlap = await _context.Bookings.AnyAsync(b =>
                    b.NurseId == dto.NurseId &&
                    b.Status != BookingStatuses.Cancelled &&
                    b.Status != BookingStatuses.Rejected &&
                    dto.StartTime < b.EndTime &&
                    endTime > b.StartTime &&
                    _context.Services.Any(s => s.Id == b.ServiceId && s.ServiceKind == "package"));

                if (false && packageOverlap)
                    return ServiceResult<BookingDetailDto>.Fail("Nurse đã có gói dịch vụ khác trùng trong khoảng thời gian này.");

                foreach (var candidate in candidateSessions)
                {
                    var hasAvailability = await _context.AvailabilitySlots.AnyAsync(slot =>
                        slot.NurseProfileId == nurseProfile.Id &&
                        slot.StartTime <= candidate.Start &&
                        slot.EndTime >= candidate.End);

                    if (!hasAvailability)
                        return ServiceResult<BookingDetailDto>.Fail($"Buổi ngày {candidate.Start:dd/MM/yyyy HH:mm} không nằm trong khung giờ trống của nurse.");

                    var singleOverlap = await _context.Bookings.AnyAsync(b =>
                        b.NurseId == dto.NurseId &&
                        b.Status != BookingStatuses.Cancelled &&
                        b.Status != BookingStatuses.Rejected &&
                        b.Service.ServiceKind != "package" &&
                        candidate.Start < b.EndTime &&
                        candidate.End > b.StartTime);

                    if (singleOverlap)
                        return ServiceResult<BookingDetailDto>.Fail($"Buổi ngày {candidate.Start:dd/MM/yyyy HH:mm} bị trùng với lịch hẹn khác của nurse.");

                    if (await HasPackageSessionOverlapAsync(dto.NurseId, candidate.Start, candidate.End))
                        return ServiceResult<BookingDetailDto>.Fail($"Buổi ngày {candidate.Start:dd/MM/yyyy HH:mm} bị trùng với gói dịch vụ khác của nurse.");
                }

                // Price: use nurse's package price (fixed)
                var totalPrice = nurseService.Price;

                var booking = new Booking
                {
                    CustomerId = customerId,
                    NurseId = dto.NurseId,
                    ServiceId = dto.ServiceId,
                    AvailabilitySlotId = null, // Packages don't require a specific slot
                    Status = BookingStatuses.PendingConfirm,
                    TotalPrice = totalPrice,
                    Address = dto.Address,
                    Notes = dto.Notes,
                    StartTime = dto.StartTime,
                    EndTime = endTime,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                // Pre-generate session logs from schedule template at selected session times.
                GenerateSessionLogs(booking, service, sessionStarts);

                _context.BookingStatusHistories.Add(new BookingStatusHistory
                {
                    BookingId = booking.Id,
                    Status = booking.Status,
                    ChangedBy = customerId,
                    Note = $"Package booking created ({service.PackageDays} days)",
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var bookingDetail = MapToDetailDto(booking, service);

                await _notificationService.CreateAsync(dto.NurseId, "Yêu cầu đặt gói dịch vụ", $"Gói \"{service.Name}\" ({service.PackageDays} ngày) - Lịch hẹn #{booking.Id} đang chờ bạn xác nhận.");
                await _realtimeNotifier.NotifyBookingCreatedAsync(dto.NurseId, bookingDetail);

                return ServiceResult<BookingDetailDto>.Ok(bookingDetail);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<IEnumerable<BookingDetailDto>> GetCustomerBookingsAsync(int customerId)
    {
        return await _context.Bookings
            .Include(b => b.Service)
            .Include(b => b.Nurse)
            .Include(b => b.Payment)
            .Include(b => b.Review)
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookingDetailDto
            {
                Id = b.Id,
                CustomerId = b.CustomerId,
                NurseId = b.NurseId,
                ServiceId = b.ServiceId,
                ServiceName = b.Service.Name,
                ServiceKind = b.Service.ServiceKind,
                NurseName = b.Nurse.FullName,
                Status = b.Status,
                TotalPrice = b.TotalPrice,
                PlatformFee = CalculatePlatformFee(b.TotalPrice),
                NursePayoutAmount = CalculateNursePayoutAmount(b.TotalPrice),
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                Address = b.Address,
                Notes = b.Notes,
                CustomerSessionRating = b.CustomerSessionRating,
                CustomerSessionNote = b.CustomerSessionNote,
                CustomerSessionReviewedAt = b.CustomerSessionReviewedAt,
                FinalReviewId = b.Review != null && !b.Review.IsDeleted ? b.Review.Id : null,
                FinalReviewRating = b.Review != null && !b.Review.IsDeleted ? b.Review.Rating : null,
                FinalReviewComment = b.Review != null && !b.Review.IsDeleted ? b.Review.Comment : null,
                FinalReviewCreatedAt = b.Review != null && !b.Review.IsDeleted ? b.Review.CreatedAt : null,
                PaymentStatus = b.Payment != null ? b.Payment.Status : null,
                RefundAmount = b.Payment != null ? b.Payment.RefundAmount : null,
                RefundReason = b.Payment != null ? b.Payment.RefundReason : null,
                RefundStatus = b.Payment != null ? b.Payment.RefundStatus : null,
                RefundedAt = b.Payment != null ? b.Payment.RefundedAt : null,
                AvailabilitySlotId = b.AvailabilitySlotId,
                PackageDays = b.Service.PackageDays,
                CompletedSessions = b.SessionLogs.Count(s => s.Status == "completed")
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<BookingDetailDto>> GetNurseBookingsAsync(int nurseId)
    {
        return await _context.Bookings
            .Include(b => b.Service)
            .Include(b => b.Nurse)
            .Include(b => b.Payment)
            .Include(b => b.Review)
            .Where(b => b.NurseId == nurseId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookingDetailDto
            {
                Id = b.Id,
                CustomerId = b.CustomerId,
                NurseId = b.NurseId,
                ServiceId = b.ServiceId,
                ServiceName = b.Service.Name,
                ServiceKind = b.Service.ServiceKind,
                NurseName = b.Nurse.FullName,
                Status = b.Status,
                TotalPrice = b.TotalPrice,
                PlatformFee = CalculatePlatformFee(b.TotalPrice),
                NursePayoutAmount = CalculateNursePayoutAmount(b.TotalPrice),
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                Address = b.Address,
                Notes = b.Notes,
                CustomerSessionRating = b.CustomerSessionRating,
                CustomerSessionNote = b.CustomerSessionNote,
                CustomerSessionReviewedAt = b.CustomerSessionReviewedAt,
                FinalReviewId = b.Review != null && !b.Review.IsDeleted ? b.Review.Id : null,
                FinalReviewRating = b.Review != null && !b.Review.IsDeleted ? b.Review.Rating : null,
                FinalReviewComment = b.Review != null && !b.Review.IsDeleted ? b.Review.Comment : null,
                FinalReviewCreatedAt = b.Review != null && !b.Review.IsDeleted ? b.Review.CreatedAt : null,
                PaymentStatus = b.Payment != null ? b.Payment.Status : null,
                RefundAmount = b.Payment != null ? b.Payment.RefundAmount : null,
                RefundReason = b.Payment != null ? b.Payment.RefundReason : null,
                RefundStatus = b.Payment != null ? b.Payment.RefundStatus : null,
                RefundedAt = b.Payment != null ? b.Payment.RefundedAt : null,
                AvailabilitySlotId = b.AvailabilitySlotId,
                PackageDays = b.Service.PackageDays,
                CompletedSessions = b.SessionLogs.Count(s => s.Status == "completed")
            })
            .ToListAsync();
    }

    public async Task<BookingDetailDto?> GetBookingDetailAsync(int actorUserId, int bookingId, bool isAdmin)
    {
        var booking = await _context.Bookings
            .Include(b => b.Service)
            .Include(b => b.Nurse)
            .Include(b => b.SessionLogs)
            .Include(b => b.StatusHistory)
            .Include(b => b.Payment)
            .Include(b => b.Review)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null) return null;

        if (!isAdmin && booking.CustomerId != actorUserId && booking.NurseId != actorUserId) return null;

        var checkInTime = booking.StatusHistory
            .Where(h => h.Status == BookingStatuses.InProgress)
            .OrderBy(h => h.CreatedAt)
            .Select(h => (DateTime?)h.CreatedAt)
            .FirstOrDefault();
        var checkOutTime = booking.StatusHistory
            .Where(h => h.Status == BookingStatuses.Completed)
            .OrderBy(h => h.CreatedAt)
            .Select(h => (DateTime?)h.CreatedAt)
            .FirstOrDefault();
        var nurseNote = booking.StatusHistory
            .Where(h => (h.Status == BookingStatuses.Completed || h.Status == BookingStatuses.InProgress)
                && !string.IsNullOrWhiteSpace(h.Note))
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => h.Note)
            .FirstOrDefault();

        return new BookingDetailDto
        {
            Id = booking.Id,
            CustomerId = booking.CustomerId,
            NurseId = booking.NurseId,
            ServiceId = booking.ServiceId,
            ServiceName = booking.Service.Name,
            ServiceKind = booking.Service.ServiceKind,
            NurseName = booking.Nurse.FullName,
            Status = booking.Status,
            TotalPrice = booking.TotalPrice,
            StartTime = booking.StartTime,
            EndTime = booking.EndTime,
            CheckInTime = checkInTime,
            CheckOutTime = checkOutTime,
            ActualDurationMinutes = checkInTime.HasValue && checkOutTime.HasValue
                ? (int)Math.Max(0, Math.Round((checkOutTime.Value - checkInTime.Value).TotalMinutes))
                : null,
            Address = booking.Address,
            Notes = booking.Notes,
            NurseNote = nurseNote,
            CustomerSessionRating = booking.CustomerSessionRating,
            CustomerSessionNote = booking.CustomerSessionNote,
            CustomerSessionTags = DeserializeTags(booking.CustomerSessionTagsJson),
            CustomerSessionReviewedAt = booking.CustomerSessionReviewedAt,
            FinalReviewId = booking.Review != null && !booking.Review.IsDeleted ? booking.Review.Id : null,
            FinalReviewRating = booking.Review != null && !booking.Review.IsDeleted ? booking.Review.Rating : null,
            FinalReviewComment = booking.Review != null && !booking.Review.IsDeleted ? booking.Review.Comment : null,
            FinalReviewCreatedAt = booking.Review != null && !booking.Review.IsDeleted ? booking.Review.CreatedAt : null,
            PaymentStatus = booking.Payment?.Status,
            RefundAmount = booking.Payment?.RefundAmount,
            RefundReason = booking.Payment?.RefundReason,
            RefundStatus = booking.Payment?.RefundStatus,
            RefundedAt = booking.Payment?.RefundedAt,
            AvailabilitySlotId = booking.AvailabilitySlotId,
            PackageDays = booking.Service.PackageDays,
            CompletedSessions = booking.SessionLogs.Count(s => s.Status == "completed")
        };
    }

    public async Task<IEnumerable<BookingStatusHistoryDto>?> GetBookingHistoryAsync(int actorUserId, int bookingId, bool isAdmin)
    {
        var booking = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            return null;
        }

        if (!isAdmin && booking.CustomerId != actorUserId && booking.NurseId != actorUserId)
        {
            return null;
        }

        return await _context.BookingStatusHistories
            .AsNoTracking()
            .Include(h => h.Changer)
            .Where(h => h.BookingId == bookingId)
            .OrderBy(h => h.CreatedAt)
            .Select(h => new BookingStatusHistoryDto
            {
                Id = h.Id,
                BookingId = h.BookingId,
                Status = h.Status,
                ChangedBy = h.ChangedBy,
                ChangedByName = h.Changer != null ? h.Changer.FullName : null,
                Note = h.Note,
                CreatedAt = h.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<ServiceResult<bool>> UpdateBookingStatusAsync(int actorUserId, bool isAdmin, UpdateBookingStatusDto dto, int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.Service)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            return ServiceResult<bool>.Fail("Lịch hẹn không tồn tại.");

        if (!isAdmin && booking.CustomerId != actorUserId && booking.NurseId != actorUserId)
            return ServiceResult<bool>.Fail("Bạn không có quyền cập nhật lịch hẹn này.");

        var nextStatus = dto.Status.Trim().ToLowerInvariant();
        var transitionError = GetTransitionError(booking, actorUserId, isAdmin, nextStatus);
        if (transitionError != null)
            return ServiceResult<bool>.Fail(transitionError);

        booking.Status = nextStatus;
        booking.UpdatedAt = DateTime.UtcNow;

        if (nextStatus == BookingStatuses.Completed)
        {
            await EnsurePayoutForCompletedBookingAsync(booking);
        }

        if (nextStatus == BookingStatuses.Rejected && booking.Payment?.Status == PaymentStatuses.Paid)
        {
            booking.Payment.RefundAmount = booking.TotalPrice;
            booking.Payment.RefundReason ??= "Nurse rejected a paid booking.";
            booking.Payment.RefundStatus = "pending";
        }

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

        if (nextStatus == BookingStatuses.Completed && targetUserId == booking.CustomerId && booking.Service.ServiceKind != "package")
        {
            await _notificationService.CreateAsync(
                booking.CustomerId,
                "Buổi chăm sóc đã hoàn tất",
                $"Lịch hẹn #{booking.Id} đã hoàn tất. Hãy đánh giá nhanh buổi chăm sóc để CareMate theo dõi chất lượng dịch vụ.",
                "review");
        }

        var bookingDetail = MapToDetailDto(booking, booking.Service);

        await _realtimeNotifier.NotifyBookingStatusChangedAsync(targetUserId, bookingDetail);

        return ServiceResult<bool>.Ok(true);
    }

    private static string? GetTransitionError(Booking booking, int actorUserId, bool isAdmin, string nextStatus)
    {
        if (isAdmin) return null;

        var isCustomer = booking.CustomerId == actorUserId;
        var isNurse = booking.NurseId == actorUserId;

        if (isCustomer)
        {
            if (nextStatus == BookingStatuses.Cancelled &&
                (booking.Status == BookingStatuses.PendingConfirm || booking.Status == BookingStatuses.Confirmed))
                return null;

            return $"Khách hàng chỉ có thể hủy lịch hẹn khi đang ở trạng thái chờ xác nhận hoặc đã xác nhận. Trạng thái hiện tại: {booking.Status}.";
        }

        if (isNurse)
        {
            if (booking.Status == BookingStatuses.PendingConfirm &&
                nextStatus is BookingStatuses.Confirmed or BookingStatuses.Rejected)
                return null;

            if (booking.Status == BookingStatuses.Confirmed &&
                nextStatus == BookingStatuses.InProgress)
                return null;

            if (booking.Status == BookingStatuses.InProgress &&
                nextStatus == BookingStatuses.Completed)
                return null;

            return $"Không thể chuyển trạng thái từ \"{booking.Status}\" sang \"{nextStatus}\".";
        }

        return "Bạn không có quyền thực hiện thao tác này.";
    }

    private async Task EnsurePayoutForCompletedBookingAsync(Booking booking)
    {
        var exists = await _context.Payouts.AnyAsync(p => p.BookingId == booking.Id);
        if (exists) return;

        var platformFee = CalculatePlatformFee(booking.TotalPrice);
        _context.Payouts.Add(new Payout
        {
            BookingId = booking.Id,
            NurseId = booking.NurseId,
            Amount = booking.TotalPrice - platformFee,
            PlatformFee = platformFee,
            Status = "on_hold",
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<ServiceResult<bool>> CancelBookingAsync(int actorUserId, bool isAdmin, int bookingId, CancelBookingDto dto)
    {
        var booking = await _context.Bookings
            .Include(b => b.Service)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            return ServiceResult<bool>.Fail("Lịch hẹn không tồn tại.");

        if (!isAdmin && booking.CustomerId != actorUserId)
            return ServiceResult<bool>.Fail("Bạn không có quyền hủy lịch hẹn này.");

        if (booking.Status != BookingStatuses.PendingConfirm && booking.Status != BookingStatuses.Confirmed)
            return ServiceResult<bool>.Fail($"Không thể hủy lịch hẹn ở trạng thái \"{NotificationVietnameseText.BookingStatus(booking.Status)}\".");

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
        if (payment != null && payment.Status == PaymentStatuses.Paid)
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

        var bookingDetail = MapToDetailDto(booking, booking.Service);

        await _realtimeNotifier.NotifyBookingStatusChangedAsync(booking.NurseId, bookingDetail);
        await _realtimeNotifier.NotifyBookingStatusChangedAsync(booking.CustomerId, bookingDetail);
        await _realtimeNotifier.NotifyAvailabilityChangedAsync(booking.NurseId);

        return ServiceResult<bool>.Ok(true);
    }

    private decimal CalculateRefundAmount(Booking booking)
    {
        var hoursUntilStart = (booking.StartTime - DateTime.UtcNow).TotalHours;

        if (hoursUntilStart > 48) return booking.TotalPrice;
        if (hoursUntilStart > 24) return booking.TotalPrice * 0.5m;
        return 0;
    }

    private static List<SessionTimeRange> BuildPackageSessionTimes(DateTime firstStart, int days, int durationMinutes)
    {
        return Enumerable.Range(0, days)
            .Select(offset =>
            {
                var start = firstStart.AddDays(offset);
                return new SessionTimeRange(start, start.AddMinutes(durationMinutes));
            })
            .ToList();
    }

    private static DateTime NormalizeDateTime(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => TimeZoneInfo.ConvertTimeToUtc(value, VietnamTimeZone)
        };
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    private async Task<bool> HasPackageSessionOverlapAsync(int nurseId, DateTime start, DateTime end)
    {
        var sessions = await _context.PackageSessionLogs
            .Include(session => session.Booking)
            .ThenInclude(booking => booking.Service)
            .Where(session =>
                session.Booking.NurseId == nurseId &&
                session.Booking.Status != BookingStatuses.Cancelled &&
                session.Booking.Status != BookingStatuses.Rejected &&
                session.Status != "skipped" &&
                session.SessionDate.Date <= end.Date &&
                session.SessionDate.Date >= start.AddDays(-1).Date)
            .ToListAsync();

        return sessions.Any(session =>
        {
            var durationMinutes = Math.Max(session.Booking.Service.EstimatedDurationMinutes, 1);
            var sessionEnd = session.SessionDate.AddMinutes(durationMinutes);
            return start < sessionEnd && end > session.SessionDate;
        });
    }

    private readonly record struct SessionTimeRange(DateTime Start, DateTime End);

    private static decimal CalculatePlatformFee(decimal totalPrice)
    {
        return decimal.Round(totalPrice * PlatformFeeRate, 0, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateNursePayoutAmount(decimal totalPrice)
    {
        return totalPrice - CalculatePlatformFee(totalPrice);
    }

    private static BookingDetailDto MapToDetailDto(Booking booking, Service service)
    {
        return new BookingDetailDto
        {
            Id = booking.Id,
            CustomerId = booking.CustomerId,
            NurseId = booking.NurseId,
            ServiceId = booking.ServiceId,
            ServiceName = service.Name,
            ServiceKind = service.ServiceKind,
            Status = booking.Status,
            TotalPrice = booking.TotalPrice,
            PlatformFee = CalculatePlatformFee(booking.TotalPrice),
            NursePayoutAmount = CalculateNursePayoutAmount(booking.TotalPrice),
            StartTime = booking.StartTime,
            EndTime = booking.EndTime,
            Address = booking.Address,
            Notes = booking.Notes,
            CustomerSessionRating = booking.CustomerSessionRating,
            CustomerSessionNote = booking.CustomerSessionNote,
            CustomerSessionTags = DeserializeTags(booking.CustomerSessionTagsJson),
            CustomerSessionReviewedAt = booking.CustomerSessionReviewedAt,
            FinalReviewId = booking.Review != null && !booking.Review.IsDeleted ? booking.Review.Id : null,
            FinalReviewRating = booking.Review != null && !booking.Review.IsDeleted ? booking.Review.Rating : null,
            FinalReviewComment = booking.Review != null && !booking.Review.IsDeleted ? booking.Review.Comment : null,
            FinalReviewCreatedAt = booking.Review != null && !booking.Review.IsDeleted ? booking.Review.CreatedAt : null,
            PaymentStatus = booking.Payment?.Status,
            RefundAmount = booking.Payment?.RefundAmount,
            RefundReason = booking.Payment?.RefundReason,
            RefundStatus = booking.Payment?.RefundStatus,
            RefundedAt = booking.Payment?.RefundedAt,
            AvailabilitySlotId = booking.AvailabilitySlotId,
            PackageDays = service.PackageDays,
            CompletedSessions = booking.SessionLogs?.Count(s => s.Status == "completed") ?? 0
        };
    }

    private void GenerateSessionLogs(Booking booking, Service service, IReadOnlyList<DateTime>? sessionStartTimes = null)
    {
        var days = service.PackageDays ?? 0;
        if (days <= 0) return;

        List<ScheduleEntry>? schedule = null;
        if (!string.IsNullOrWhiteSpace(service.PackageScheduleJson))
        {
            try
            {
                schedule = JsonSerializer.Deserialize<List<ScheduleEntry>>(service.PackageScheduleJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* fallback to empty sessions */ }
        }

        for (int i = 1; i <= days; i++)
        {
            var entry = schedule?.FirstOrDefault(e => e.Day == i);
            _context.PackageSessionLogs.Add(new PackageSessionLog
            {
                BookingId = booking.Id,
                SessionNumber = i,
                SessionDate = sessionStartTimes != null && sessionStartTimes.Count >= i
                    ? sessionStartTimes[i - 1]
                    : booking.StartTime.AddDays(i - 1),
                Title = entry?.Title ?? $"Buổi {i}",
                Description = entry?.Description,
                PlannedServiceKeys = entry?.ServiceKeys,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }

    private class ScheduleEntry
    {
        public int Day { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? ServiceKeys { get; set; }
    }

    private static List<string> DeserializeTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson)) return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(tagsJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
