using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;

namespace MomCare.Services;

public class PackageSessionService : IPackageSessionService
{
    private readonly MomCareContext _context;
    private readonly INotificationService _notificationService;

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
        var total = sessions.Count;

        var today = DateTime.UtcNow.Date;
        var todaySession = sessions.FirstOrDefault(s => s.SessionDate.Date == today);

        return ServiceResult<PackageProgressDto>.Ok(new PackageProgressDto
        {
            BookingId = bookingId,
            TotalSessions = total,
            CompletedSessions = completed,
            ProgressPercent = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0,
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

            await _notificationService.CreateAsync(booking.CustomerId, "Gói dịch vụ đã hoàn thành",
                $"Gói \"{booking.Service.Name}\" đã hoàn thành toàn bộ {booking.SessionLogs.Count} buổi. Cảm ơn bạn đã sử dụng dịch vụ!");
        }

        await _context.SaveChangesAsync();

        var total = booking.SessionLogs.Count;
        var completed = booking.SessionLogs.Count(s => s.Status == "completed");

        await _notificationService.CreateAsync(booking.CustomerId, "Buổi chăm sóc hoàn tất",
            $"Buổi {session.SessionNumber}/{total} đã hoàn tất. Tiến độ: {completed}/{total}.");

        return ServiceResult<PackageSessionDto>.Ok(MapToDto(session));
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
            NurseNote = session.NurseNote
        };
    }
}
