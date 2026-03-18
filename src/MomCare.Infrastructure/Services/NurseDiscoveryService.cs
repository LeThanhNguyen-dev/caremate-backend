using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;

namespace MomCare.Services;

public class NurseDiscoveryService : INurseDiscoveryService
{
    private readonly MomCareContext _context;

    public NurseDiscoveryService(MomCareContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<NurseDiscoveryDto>> SearchAsync(
        int? serviceId,
        decimal? minPrice,
        decimal? maxPrice,
        DateTime? startTime,
        DateTime? endTime)
    {
        var query = _context.NurseProfiles
            .Include(np => np.User)
            .Include(np => np.NurseServices)
            .Where(np => np.IsActive && np.IsVerified == "verified")
            .AsQueryable();

        if (serviceId.HasValue)
        {
            query = query.Where(np => np.NurseServices.Any(ns =>
                ns.ServiceId == serviceId.Value &&
                ns.Status == "enabled"));
        }

        if (minPrice.HasValue)
        {
            query = query.Where(np => np.NurseServices.Any(ns => ns.Price >= minPrice.Value));
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(np => np.NurseServices.Any(ns => ns.Price <= maxPrice.Value));
        }

        if (startTime.HasValue && endTime.HasValue)
        {
            var start = startTime.Value;
            var end = endTime.Value;

            query = query.Where(np =>
                _context.AvailabilitySlots.Any(a =>
                    a.NurseProfileId == np.Id &&
                    !a.IsBooked &&
                    a.StartTime <= start &&
                    a.EndTime >= end) &&
                !_context.Bookings.Any(b =>
                    b.NurseId == np.UserId &&
                    b.Status != BookingStatuses.Cancelled &&
                    b.Status != BookingStatuses.Rejected &&
                    start < b.EndTime &&
                    end > b.StartTime));
        }

        var nurses = await query
            .OrderByDescending(np => np.AverageRating)
            .ThenByDescending(np => np.YearsExperience)
            .ToListAsync();

        return nurses.Select(np =>
        {
            var nurseService = serviceId.HasValue
                ? np.NurseServices.FirstOrDefault(ns => ns.ServiceId == serviceId.Value)
                : np.NurseServices.OrderBy(ns => ns.Price).FirstOrDefault();

            return new NurseDiscoveryDto
            {
                UserId = np.UserId,
                NurseProfileId = np.Id,
                FullName = np.User.FullName,
                Avatar = np.User.Avatar,
                Bio = np.Bio,
                Specialization = np.Specialization,
                AverageRating = np.AverageRating,
                YearsExperience = np.YearsExperience,
                ServiceRadiusKm = np.ServiceRadiusKm,
                ServicePrice = nurseService?.Price,
                ServiceUnit = nurseService?.Unit
            };
        }).ToList();
    }

    public async Task<NurseProfileDetailDto?> GetDetailAsync(int userId)
    {
        var profile = await _context.NurseProfiles
            .Include(np => np.User)
            .Include(np => np.Documents)
            .FirstOrDefaultAsync(np => np.UserId == userId && np.IsActive);

        if (profile == null)
        {
            return null;
        }

        return new NurseProfileDetailDto
        {
            UserId = profile.UserId,
            FullName = profile.User.FullName,
            Email = profile.User.Email ?? string.Empty,
            Phone = profile.User.PhoneNumber,
            Bio = profile.Bio,
            YearsExperience = profile.YearsExperience,
            ServiceRadiusKm = profile.ServiceRadiusKm,
            IsVerified = profile.IsVerified,
            Documents = profile.Documents.Select(d => new NurseDocumentDto
            {
                Id = d.Id,
                Type = d.Type,
                FileUrl = d.FileUrl,
                Status = d.Status
            }).ToList()
        };
    }
}
