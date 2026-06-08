using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;
using System.Text.Json;

namespace MomCare.Services;

public class PackageSessionService : IPackageSessionService
{
    private readonly MomCareContext _context;
    private readonly INotificationService _notificationService;
    private const decimal PlatformFeeRate = 0.15m;

    public PackageSessionService(MomCareContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<ServiceResult<PackageProgressDto>> GetProgressAsync(int actorUserId, int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.Service)
            .Include(b => b.SessionLogs.OrderBy(s => s.SessionNumber))
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            return ServiceResult<PackageProgressDto>.Fail("Không tìm thấy lịch hẹn.");

        if (booking.CustomerId != actorUserId && booking.NurseId != actorUserId)
            return ServiceResult<PackageProgressDto>.Fail("Bạn không có quyền xem tiến độ này.");

        if (booking.Service.ServiceKind != "package")
            return ServiceResult<PackageProgressDto>.Fail("Lịch hẹn này không phải gói dịch vụ.");

        var sessions = booking.SessionLogs.Select(MapToDto).ToList();
        var completed = sessions.Count(s => s.Status == "completed");
        var reviewed = sessions.Where(s => s.CustomerRating.HasValue).ToList();
        var total = sessions.Count;

        var today = DateTime.UtcNow.Date;
        var todaySession = sessions.FirstOrDefault(s => s.SessionDate.Date == today);

        return ServiceResult<PackageProgressDto>.Ok(new PackageProgressDto
        {
            BookingId = bookingId,
            TotalSessions = total,
            CompletedSessions = completed,
            ProgressPercent = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0,
            ReviewedSessions = reviewed.Count,
            AverageCustomerRating = reviewed.Count > 0 ? Math.Round(reviewed.Average(s => s.CustomerRating!.Value), 1) : null,
            TodaySession = todaySession,
            Sessions = sessions
        });
    }

    public async Task<ServiceResult<PackageSessionDto>> CheckInAsync(int nurseUserId, int bookingId, CheckInSessionDto dto)
    {
        var booking = await _context.Bookings
            .Include(b => b.Service)
            .Include(b => b.SessionLogs)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            return ServiceResult<PackageSessionDto>.Fail("Không tìm thấy lịch hẹn.");

        if (booking.NurseId != nurseUserId)
            return ServiceResult<PackageSessionDto>.Fail("Bạn không phải y tá của lịch hẹn này.");

        if (booking.Service.ServiceKind != "package")
            return ServiceResult<PackageSessionDto>.Fail("Lịch hẹn này không phải gói dịch vụ.");

        if (booking.Status != BookingStatuses.InProgress && booking.Status != BookingStatuses.Confirmed)
            return ServiceResult<PackageSessionDto>.Fail("Gói dịch vụ chưa ở trạng thái đang thực hiện.");

        var today = DateTime.UtcNow.Date;
        var session = booking.SessionLogs.FirstOrDefault(s => s.SessionDate.Date == today);

        if (session == null)
            return ServiceResult<PackageSessionDto>.Fail("Hôm nay không có buổi nào trong lịch trình gói.");

        if (session.Status == "checked_in")
            return ServiceResult<PackageSessionDto>.Fail("Bạn đã check-in buổi hôm nay rồi.");

        if (session.Status == "completed")
            return ServiceResult<PackageSessionDto>.Fail("Buổi hôm nay đã hoàn thành.");

        session.CheckInTime = DateTime.UtcNow;
        session.Status = "checked_in";
        session.NurseNote = dto.NurseNote;
        session.UpdatedAt = DateTime.UtcNow;

        // Auto-transition booking to in_progress if confirmed
        if (booking.Status == BookingStatuses.Confirmed)
        {
            booking.Status = BookingStatuses.InProgress;
            booking.UpdatedAt = DateTime.UtcNow;

            _context.BookingStatusHistories.Add(new Models.BookingStatusHistory
            {
                BookingId = booking.Id,
                Status = BookingStatuses.InProgress,
                ChangedBy = nurseUserId,
                Note = "Tự động chuyển sang đang thực hiện khi y tá check-in.",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        await _notificationService.CreateAsync(booking.CustomerId, "Y tá đã check-in",
            $"Y tá đã bắt đầu buổi {session.SessionNumber}/{booking.SessionLogs.Count}: {session.Title ?? "Chăm sóc"}.");

        return ServiceResult<PackageSessionDto>.Ok(MapToDto(session));
    }

    public async Task<ServiceResult<PackageSessionDto>> SubmitPackageSessionFeedbackAsync(int customerUserId, int bookingId, int sessionId, CustomerSessionFeedbackDto dto)
    {
        var validationError = ValidateFeedback(dto);
        if (validationError != null)
            return ServiceResult<PackageSessionDto>.Fail(validationError);

        var booking = await _context.Bookings
            .Include(b => b.Service)
            .Include(b => b.SessionLogs)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            return ServiceResult<PackageSessionDto>.Fail("Không tìm thấy lịch hẹn.");

        if (booking.CustomerId != customerUserId)
            return ServiceResult<PackageSessionDto>.Fail("Bạn không có quyền đánh giá buổi này.");

        if (booking.Service.ServiceKind != "package")
            return ServiceResult<PackageSessionDto>.Fail("Lịch hẹn này không phải gói dịch vụ.");

        var session = booking.SessionLogs.FirstOrDefault(s => s.Id == sessionId);
        if (session == null)
            return ServiceResult<PackageSessionDto>.Fail("Không tìm thấy buổi chăm sóc.");

        if (session.Status != "completed")
            return ServiceResult<PackageSessionDto>.Fail("Chỉ có thể đánh giá sau khi buổi chăm sóc hoàn thành.");

        session.CustomerRating = dto.Rating;
        session.CustomerNote = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        session.CustomerTagsJson = SerializeTags(dto.Tags);
        session.CustomerReviewedAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _notificationService.CreateAsync(booking.NurseId, "Khách hàng đánh giá buổi chăm sóc",
            $"Buổi {session.SessionNumber}/{booking.SessionLogs.Count} vừa được đánh giá {session.CustomerRating}/5 sao.");

        return ServiceResult<PackageSessionDto>.Ok(MapToDto(session));
    }

    public async Task<ServiceResult<BookingDetailDto>> SubmitSingleSessionFeedbackAsync(int customerUserId, int bookingId, CustomerSessionFeedbackDto dto)
    {
        var validationError = ValidateFeedback(dto);
        if (validationError != null)
            return ServiceResult<BookingDetailDto>.Fail(validationError);

        var booking = await _context.Bookings
            .Include(b => b.Service)
            .Include(b => b.Nurse)
            .Include(b => b.SessionLogs)
            .Include(b => b.StatusHistory)
            .Include(b => b.Payment)
            .Include(b => b.Review)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            return ServiceResult<BookingDetailDto>.Fail("Không tìm thấy lịch hẹn.");

        if (booking.CustomerId != customerUserId)
            return ServiceResult<BookingDetailDto>.Fail("Bạn không có quyền đánh giá buổi này.");

        if (booking.Service.ServiceKind == "package")
            return ServiceResult<BookingDetailDto>.Fail("Gói dịch vụ cần đánh giá theo từng buổi.");

        if (booking.Status != BookingStatuses.Completed)
            return ServiceResult<BookingDetailDto>.Fail("Chỉ có thể đánh giá sau khi buổi chăm sóc hoàn thành.");

        booking.CustomerSessionRating = dto.Rating;
        booking.CustomerSessionNote = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        booking.CustomerSessionTagsJson = SerializeTags(dto.Tags);
        booking.CustomerSessionReviewedAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _notificationService.CreateAsync(booking.NurseId, "Khách hàng đánh giá buổi chăm sóc",
            $"Lịch hẹn #{booking.Id} vừa được đánh giá {booking.CustomerSessionRating}/5 sao.");

        return ServiceResult<BookingDetailDto>.Ok(MapBookingToDetailDto(booking));
    }

    public async Task<ServiceResult<PackageSessionDto>> CheckOutAsync(int nurseUserId, int bookingId, CheckOutSessionDto dto)
    {
        var booking = await _context.Bookings
            .Include(b => b.Service)
            .Include(b => b.SessionLogs)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            return ServiceResult<PackageSessionDto>.Fail("Không tìm thấy lịch hẹn.");

        if (booking.NurseId != nurseUserId)
            return ServiceResult<PackageSessionDto>.Fail("Bạn không phải y tá của lịch hẹn này.");

        var today = DateTime.UtcNow.Date;
        var session = booking.SessionLogs.FirstOrDefault(s => s.SessionDate.Date == today);

        if (session == null)
            return ServiceResult<PackageSessionDto>.Fail("Hôm nay không có buổi nào trong lịch trình gói.");

        if (session.Status != "checked_in")
            return ServiceResult<PackageSessionDto>.Fail("Bạn cần check-in trước khi check-out.");

        session.CheckOutTime = DateTime.UtcNow;
        session.Status = "completed";
        if (!string.IsNullOrWhiteSpace(dto.NurseNote))
            session.NurseNote = dto.NurseNote;
        session.UpdatedAt = DateTime.UtcNow;

        // Check if ALL sessions are completed → auto-complete booking
        var allCompleted = booking.SessionLogs.All(s => s.Status == "completed");
        if (allCompleted)
        {
            booking.Status = BookingStatuses.Completed;
            booking.UpdatedAt = DateTime.UtcNow;

            _context.BookingStatusHistories.Add(new Models.BookingStatusHistory
            {
                BookingId = booking.Id,
                Status = BookingStatuses.Completed,
                ChangedBy = nurseUserId,
                Note = "Tự động hoàn thành khi tất cả buổi trong gói đã xong.",
                CreatedAt = DateTime.UtcNow
            });

            await EnsurePayoutForCompletedBookingAsync(booking);

            await _notificationService.CreateAsync(booking.CustomerId, "Gói dịch vụ đã hoàn thành",
                $"Gói \"{booking.Service.Name}\" đã hoàn thành toàn bộ {booking.SessionLogs.Count} buổi. Cảm ơn bạn đã sử dụng dịch vụ!");
        }

        await _context.SaveChangesAsync();

        var total = booking.SessionLogs.Count;
        var completed = booking.SessionLogs.Count(s => s.Status == "completed");

        await _notificationService.CreateAsync(booking.CustomerId, "Buổi chăm sóc đã hoàn tất",
            $"Buổi {session.SessionNumber}/{total} đã hoàn tất. Hãy đánh giá nhanh buổi này để CareMate tổng hợp chất lượng gói. Tiến độ: {completed}/{total}.",
            "review");

        return ServiceResult<PackageSessionDto>.Ok(MapToDto(session));
    }

    private async Task EnsurePayoutForCompletedBookingAsync(Booking booking)
    {
        var exists = await _context.Payouts.AnyAsync(p => p.BookingId == booking.Id);
        if (exists) return;

        var platformFee = decimal.Round(booking.TotalPrice * PlatformFeeRate, 0, MidpointRounding.AwayFromZero);
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

    private static PackageSessionDto MapToDto(Models.PackageSessionLog session)
    {
        return new PackageSessionDto
        {
            Id = session.Id,
            SessionNumber = session.SessionNumber,
            SessionDate = session.SessionDate,
            Title = session.Title,
            Description = session.Description,
            PlannedServiceKeys = session.PlannedServiceKeys,
            Status = session.Status,
            CheckInTime = session.CheckInTime,
            CheckOutTime = session.CheckOutTime,
            NurseNote = session.NurseNote,
            CustomerRating = session.CustomerRating,
            CustomerNote = session.CustomerNote,
            CustomerTags = DeserializeTags(session.CustomerTagsJson),
            CustomerReviewedAt = session.CustomerReviewedAt
        };
    }

    private static string? ValidateFeedback(CustomerSessionFeedbackDto dto)
    {
        if (dto.Rating is < 1 or > 5)
            return "Số sao đánh giá phải từ 1 đến 5.";

        if (dto.Note?.Length > 1000)
            return "Ghi chú đánh giá không được vượt quá 1000 ký tự.";

        if (NormalizeTags(dto.Tags).Count > 8)
            return "Chỉ có thể chọn tối đa 8 nhãn đánh giá.";

        return null;
    }

    private static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags == null) return new List<string>();

        return tags
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Length > 80 ? tag[..80] : tag)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    private static string? SerializeTags(IEnumerable<string>? tags)
    {
        var normalized = NormalizeTags(tags);
        return normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized);
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

    private static BookingDetailDto MapBookingToDetailDto(Booking booking)
    {
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
}
