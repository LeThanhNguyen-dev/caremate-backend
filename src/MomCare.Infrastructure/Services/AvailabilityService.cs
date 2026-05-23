using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly MomCareContext _context;
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public AvailabilityService(MomCareContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<AvailabilitySlotDto>> GetNurseSlotsAsync(int nurseUserId, DateTime? from, DateTime? to)
    {
        from = NormalizeOptionalDateTime(from);
        to = NormalizeOptionalDateTime(to);

        var nurseProfile = await _context.NurseProfiles.FirstOrDefaultAsync(n => n.UserId == nurseUserId);
        if (nurseProfile == null) return [];

        var query = _context.AvailabilitySlots
            .Where(s => s.NurseProfileId == nurseProfile.Id)
            .AsQueryable();

        if (from.HasValue) query = query.Where(s => s.StartTime >= from.Value);
        if (to.HasValue) query = query.Where(s => s.EndTime <= to.Value);

        var slots = await query.OrderBy(s => s.StartTime).ToListAsync();
        
        // Get all active bookings for these slots
        var slotIds = slots.Select(s => s.Id).ToList();
        var bookedSlotIds = await _context.Bookings
            .Where(b => slotIds.Contains(b.AvailabilitySlotId ?? 0) && 
                        b.Status != BookingStatuses.Cancelled && 
                        b.Status != BookingStatuses.Rejected)
            .Select(b => b.AvailabilitySlotId)
            .ToListAsync();

        var packageSessions = await _context.PackageSessionLogs
            .Include(session => session.Booking)
            .ThenInclude(booking => booking.Service)
            .Where(session =>
                session.Booking.NurseId == nurseUserId &&
                session.Booking.Status != BookingStatuses.Cancelled &&
                session.Booking.Status != BookingStatuses.Rejected &&
                session.Status != "skipped")
            .ToListAsync();

        return slots
            .Select(s =>
            {
                var overlapsPackageSession = packageSessions.Any(session =>
                {
                    var sessionEnd = session.SessionDate.AddMinutes(Math.Max(session.Booking.Service.EstimatedDurationMinutes, 1));
                    return s.StartTime < sessionEnd && s.EndTime > session.SessionDate;
                });

                return new AvailabilitySlotDto
                {
                    Id = s.Id,
                    NurseProfileId = s.NurseProfileId,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    IsAvailable = !bookedSlotIds.Contains(s.Id) && !overlapsPackageSession
                };
            });
    }

    public async Task<IEnumerable<AvailabilitySlotDto>> GetMySlotsAsync(int nurseUserId, DateTime? from, DateTime? to)
    {
        from = NormalizeOptionalDateTime(from);
        to = NormalizeOptionalDateTime(to);

        var nurseProfile = await _context.NurseProfiles.FirstOrDefaultAsync(n => n.UserId == nurseUserId);
        if (nurseProfile == null) return [];

        var query = _context.AvailabilitySlots
            .Where(s => s.NurseProfileId == nurseProfile.Id)
            .AsQueryable();

        if (from.HasValue) query = query.Where(s => s.StartTime >= from.Value);
        if (to.HasValue) query = query.Where(s => s.EndTime <= to.Value);

        var slots = await query.OrderBy(s => s.StartTime).ToListAsync();
        var slotIds = slots.Select(s => s.Id).ToList();
        
        var bookedSlotIds = await _context.Bookings
            .Where(b => slotIds.Contains(b.AvailabilitySlotId ?? 0) && 
                        b.Status != BookingStatuses.Cancelled && 
                        b.Status != BookingStatuses.Rejected)
            .Select(b => b.AvailabilitySlotId)
            .ToListAsync();

        return slots.Select(s => new AvailabilitySlotDto
        {
            Id = s.Id,
            NurseProfileId = s.NurseProfileId,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            IsAvailable = !bookedSlotIds.Contains(s.Id)
        });
    }

    public async Task<IEnumerable<AvailabilitySlotDto>> GetSlotsByServiceAsync(
        int nurseUserId, int serviceId, DateTime? from, DateTime? to)
    {
        var nurseProfile = await _context.NurseProfiles
            .Include(np => np.NurseServices)
            .FirstOrDefaultAsync(n => n.UserId == nurseUserId);

        if (nurseProfile == null) return [];

        // Check if nurse provides this service
        var providesService = nurseProfile.NurseServices.Any(ns => ns.ServiceId == serviceId && ns.Status == "enabled");
        if (!providesService) return [];

        // If they provide the service, all unbooked slots are valid for this service
        return await GetNurseSlotsAsync(nurseUserId, from, to);
    }

    public async Task<AvailabilitySlotDto?> CreateSlotAsync(int nurseUserId, CreateAvailabilitySlotDto dto)
    {
        var startTime = NormalizeDateTime(dto.StartTime);
        var endTime = NormalizeDateTime(dto.EndTime);

        if (endTime <= startTime) return null;

        var nurseProfile = await _context.NurseProfiles.FirstOrDefaultAsync(n => n.UserId == nurseUserId);
        if (nurseProfile == null) return null;

        // Check for overlapping slots
        var overlapExists = await _context.AvailabilitySlots.AnyAsync(s =>
            s.NurseProfileId == nurseProfile.Id &&
            startTime < s.EndTime &&
            endTime > s.StartTime);

        if (overlapExists) return null;

        var slot = new AvailabilitySlot
        {
            NurseProfileId = nurseProfile.Id,
            StartTime = startTime,
            EndTime = endTime
        };

        _context.AvailabilitySlots.Add(slot);
        await _context.SaveChangesAsync();

        return new AvailabilitySlotDto
        {
            Id = slot.Id,
            NurseProfileId = slot.NurseProfileId,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            IsAvailable = true
        };
    }

    public async Task<bool> DeleteSlotAsync(int nurseUserId, int slotId)
    {
        var nurseProfile = await _context.NurseProfiles.FirstOrDefaultAsync(n => n.UserId == nurseUserId);
        if (nurseProfile == null) return false;

        var slot = await _context.AvailabilitySlots
            .FirstOrDefaultAsync(s => s.Id == slotId && s.NurseProfileId == nurseProfile.Id);

        if (slot == null) return false;

        // Verify no active bookings for this slot
        var hasBooking = await _context.Bookings.AnyAsync(b => 
            b.AvailabilitySlotId == slotId && 
            b.Status != BookingStatuses.Cancelled && 
            b.Status != BookingStatuses.Rejected);

        if (hasBooking) return false;

        _context.AvailabilitySlots.Remove(slot);
        return await _context.SaveChangesAsync() > 0;
    }

    private static DateTime? NormalizeOptionalDateTime(DateTime? value)
    {
        return value.HasValue ? NormalizeDateTime(value.Value) : null;
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
}
