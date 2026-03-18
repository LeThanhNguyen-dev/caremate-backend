using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly MomCareContext _context;

    public AvailabilityService(MomCareContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<AvailabilitySlotDto>> GetNurseSlotsAsync(int nurseUserId, DateTime? from, DateTime? to)
    {
        var nurseProfile = await _context.NurseProfiles.FirstOrDefaultAsync(n => n.UserId == nurseUserId);
        if (nurseProfile == null)
        {
            return [];
        }

        var query = _context.AvailabilitySlots
            .Where(s => s.NurseProfileId == nurseProfile.Id && !s.IsBooked)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(s => s.StartTime >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(s => s.EndTime <= to.Value);
        }

        return await query
            .OrderBy(s => s.StartTime)
            .Select(s => new AvailabilitySlotDto
            {
                Id = s.Id,
                NurseProfileId = s.NurseProfileId,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                IsBooked = s.IsBooked
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<AvailabilitySlotDto>> GetMySlotsAsync(int nurseUserId, DateTime? from, DateTime? to)
    {
        var nurseProfile = await _context.NurseProfiles.FirstOrDefaultAsync(n => n.UserId == nurseUserId);
        if (nurseProfile == null)
        {
            return [];
        }

        var query = _context.AvailabilitySlots
            .Where(s => s.NurseProfileId == nurseProfile.Id)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(s => s.StartTime >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(s => s.EndTime <= to.Value);
        }

        return await query
            .OrderBy(s => s.StartTime)
            .Select(s => new AvailabilitySlotDto
            {
                Id = s.Id,
                NurseProfileId = s.NurseProfileId,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                IsBooked = s.IsBooked
            })
            .ToListAsync();
    }

    public async Task<AvailabilitySlotDto?> CreateSlotAsync(int nurseUserId, CreateAvailabilitySlotDto dto)
    {
        if (dto.EndTime <= dto.StartTime)
        {
            return null;
        }

        var nurseProfile = await _context.NurseProfiles.FirstOrDefaultAsync(n => n.UserId == nurseUserId);
        if (nurseProfile == null)
        {
            return null;
        }

        var overlapExists = await _context.AvailabilitySlots.AnyAsync(s =>
            s.NurseProfileId == nurseProfile.Id &&
            dto.StartTime < s.EndTime &&
            dto.EndTime > s.StartTime);

        if (overlapExists)
        {
            return null;
        }

        var slot = new AvailabilitySlot
        {
            NurseProfileId = nurseProfile.Id,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            IsBooked = false
        };

        _context.AvailabilitySlots.Add(slot);
        await _context.SaveChangesAsync();

        return new AvailabilitySlotDto
        {
            Id = slot.Id,
            NurseProfileId = slot.NurseProfileId,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            IsBooked = slot.IsBooked
        };
    }

    public async Task<bool> DeleteSlotAsync(int nurseUserId, int slotId)
    {
        var nurseProfile = await _context.NurseProfiles.FirstOrDefaultAsync(n => n.UserId == nurseUserId);
        if (nurseProfile == null)
        {
            return false;
        }

        var slot = await _context.AvailabilitySlots.FirstOrDefaultAsync(s => s.Id == slotId && s.NurseProfileId == nurseProfile.Id);
        if (slot == null || slot.IsBooked)
        {
            return false;
        }

        _context.AvailabilitySlots.Remove(slot);
        return await _context.SaveChangesAsync() > 0;
    }
}
