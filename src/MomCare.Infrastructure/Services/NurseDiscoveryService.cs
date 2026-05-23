using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;

namespace MomCare.Services;

public class NurseDiscoveryService : INurseDiscoveryService
{
    private readonly MomCareContext _context;
    private readonly ICloudinaryService _cloudinaryService;

    public NurseDiscoveryService(MomCareContext context, ICloudinaryService cloudinaryService)
    {
        _context = context;
        _cloudinaryService = cloudinaryService;
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

            // Availability check: Nurse must have an unbooked slot covering the range
            query = query.Where(np =>
                _context.AvailabilitySlots.Any(a =>
                    a.NurseProfileId == np.Id &&
                    a.StartTime <= start &&
                    a.EndTime >= end &&
                    !_context.Bookings.Any(b => 
                        b.AvailabilitySlotId == a.Id && 
                        b.Status != BookingStatuses.Cancelled && 
                        b.Status != BookingStatuses.Rejected)) &&
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

        if (profile == null) return null;

        // Get reviews
        var reviews = await _context.Reviews
            .Where(r => r.NurseId == userId && !r.IsDeleted)
            .Include(r => r.Booking).ThenInclude(b => b.Service)
            .Include(r => r.Customer)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDetailDto
            {
                Id = r.Id,
                BookingId = r.BookingId,
                CustomerId = r.CustomerId,
                CustomerName = r.Customer.FullName,
                CustomerAvatar = r.Customer.Avatar,
                ServiceId = r.Booking.ServiceId,
                ServiceName = r.Booking.Service.Name,
                ServiceCategory = r.Booking.Service.Category,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        var ratingDistribution = reviews
            .GroupBy(r => r.Rating)
            .ToDictionary(g => g.Key, g => g.Count());

        for (int i = 1; i <= 5; i++) ratingDistribution.TryAdd(i, 0);

        return new NurseProfileDetailDto
        {
            UserId = profile.UserId,
            FullName = profile.User.FullName,
            Email = profile.User.Email ?? string.Empty,
            Phone = profile.User.PhoneNumber,
            Avatar = profile.User.Avatar,
            Bio = profile.Bio,
            Specialization = profile.Specialization,
            YearsExperience = profile.YearsExperience,
            ServiceRadiusKm = profile.ServiceRadiusKm,
            IsVerified = profile.IsVerified,
            AverageRating = profile.AverageRating,
            TotalReviews = reviews.Count,
            RatingDistribution = ratingDistribution,
            Documents = profile.Documents.Select(d => new NurseDocumentDto
            {
                Id = d.Id,
                Type = d.Type,
                FileUrl = string.Empty, // Publicly, we don't expose private document URLs without explicit requests
                PublicId = d.PublicId,
                Status = d.Status,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            }).ToList(),
            Reviews = reviews
        };
    }
}
