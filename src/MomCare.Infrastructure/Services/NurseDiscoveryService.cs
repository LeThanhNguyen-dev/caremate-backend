using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;

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
        DateTime? endTime,
        double? latitude,
        double? longitude,
        string? district,
        string? sortBy)
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

        var nurses = await query.ToListAsync();
        var nurseProfileIds = nurses.Select(np => np.Id).ToList();
        var nurseUserIds = nurses.Select(np => np.UserId).ToList();
        var now = DateTime.UtcNow;

        var addresses = await _context.Addresses
            .Where(a => nurseUserIds.Contains(a.UserId) && a.Type == "nurse_base")
            .ToListAsync();
        var normalizedDistrict = NormalizeDistrict(district);

        if (!string.IsNullOrWhiteSpace(normalizedDistrict))
        {
            nurses = nurses
                .Where(np => NormalizeDistrict(addresses.FirstOrDefault(a => a.UserId == np.UserId)?.District) == normalizedDistrict)
                .ToList();
            nurseProfileIds = nurses.Select(np => np.Id).ToList();
            nurseUserIds = nurses.Select(np => np.UserId).ToList();
        }

        var slots = await _context.AvailabilitySlots
            .Where(a => nurseProfileIds.Contains(a.NurseProfileId) && a.EndTime >= now)
            .OrderBy(a => a.StartTime)
            .ToListAsync();

        var busySlotIds = await _context.Bookings
            .Where(b =>
                b.AvailabilitySlotId != null &&
                b.Status != BookingStatuses.Cancelled &&
                b.Status != BookingStatuses.Rejected)
            .Select(b => b.AvailabilitySlotId!.Value)
            .ToListAsync();

        var busySlotIdSet = busySlotIds.ToHashSet();

        return nurses.Select(np =>
        {
            var nurseService = serviceId.HasValue
                ? np.NurseServices.FirstOrDefault(ns => ns.ServiceId == serviceId.Value)
                : np.NurseServices.OrderBy(ns => ns.Price).FirstOrDefault();
            var address = addresses.FirstOrDefault(a => a.UserId == np.UserId);
            var distanceKm = latitude.HasValue && longitude.HasValue && address?.Latitude.HasValue == true && address.Longitude.HasValue
                ? Math.Round(CalculateDistanceKm(latitude.Value, longitude.Value, address.Latitude.Value, address.Longitude.Value), 1)
                : (double?)null;
            var nextAvailableAt = slots
                .Where(s => s.NurseProfileId == np.Id && !busySlotIdSet.Contains(s.Id))
                .Select(s => (DateTime?)s.StartTime)
                .FirstOrDefault();
            var districtMatches = !string.IsNullOrWhiteSpace(normalizedDistrict) &&
                NormalizeDistrict(address?.District) == normalizedDistrict;
            var score = CalculateMatchScore(np, nurseService?.Price, distanceKm, nextAvailableAt, districtMatches);

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
                ServiceUnit = nurseService?.Unit,
                DistanceKm = distanceKm,
                MatchScore = score,
                MatchReasons = BuildMatchReasons(np, distanceKm, nextAvailableAt, districtMatches),
                NextAvailableAt = nextAvailableAt,
                District = address?.District
            };
        })
        .OrderByDescending(n => string.Equals(sortBy, "bestMatch", StringComparison.OrdinalIgnoreCase) ? n.MatchScore : (int)Math.Round(n.AverageRating * 20))
        .ThenBy(n => n.DistanceKm ?? double.MaxValue)
        .ThenByDescending(n => n.YearsExperience)
        .ToList();
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

    private static int CalculateMatchScore(NurseProfile profile, decimal? price, double? distanceKm, DateTime? nextAvailableAt, bool districtMatches)
    {
        var distanceScore = distanceKm.HasValue
            ? Math.Clamp(35 - (distanceKm.Value / Math.Max(profile.ServiceRadiusKm, 1) * 35), 0, 35)
            : districtMatches ? 26 : 18;
        var ratingScore = Math.Clamp((double)profile.AverageRating / 5 * 25, 0, 25);
        var experienceScore = Math.Clamp(profile.YearsExperience / 12d * 20, 0, 20);
        var priceScore = price.HasValue
            ? Math.Clamp(10 - ((double)Math.Max(price.Value - 350_000m, 0m) / 1_200_000d * 10), 0, 10)
            : 5;
        var availabilityScore = nextAvailableAt.HasValue
            ? Math.Clamp(10 - Math.Max((nextAvailableAt.Value - DateTime.UtcNow).TotalDays, 0), 2, 10)
            : 0;

        return (int)Math.Round(distanceScore + ratingScore + experienceScore + priceScore + availabilityScore);
    }

    private static List<string> BuildMatchReasons(NurseProfile profile, double? distanceKm, DateTime? nextAvailableAt, bool districtMatches)
    {
        var reasons = new List<string>();

        if (distanceKm.HasValue)
        {
            reasons.Add($"Cách bạn {distanceKm.Value:0.0}km");
        }
        else if (districtMatches)
        {
            reasons.Add("Cùng khu vực bạn chọn");
        }

        if (profile.AverageRating >= 4.5m)
        {
            reasons.Add($"{profile.AverageRating:0.0} sao từ khách hàng");
        }

        if (profile.YearsExperience >= 3)
        {
            reasons.Add($"{profile.YearsExperience} năm kinh nghiệm");
        }

        if (nextAvailableAt.HasValue)
        {
            reasons.Add(nextAvailableAt.Value.Date == DateTime.UtcNow.Date ? "Có lịch rảnh hôm nay" : "Có lịch rảnh gần nhất");
        }

        return reasons;
    }

    private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static string NormalizeDistrict(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant().Replace("district", string.Empty).Replace("quan", string.Empty).Trim();
}
