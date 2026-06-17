using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ILlmService _llmService;
    private readonly ILogger<NurseDiscoveryService> _logger;

    public NurseDiscoveryService(
        MomCareContext context,
        ICloudinaryService cloudinaryService,
        ILlmService llmService,
        ILogger<NurseDiscoveryService> logger)
    {
        _context = context;
        _cloudinaryService = cloudinaryService;
        _llmService = llmService;
        _logger = logger;
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

        if (latitude.HasValue && longitude.HasValue)
        {
            nurses = nurses
                .Where(np =>
                {
                    var address = addresses.FirstOrDefault(a => a.UserId == np.UserId);
                    if (address?.Latitude.HasValue != true || address.Longitude.HasValue != true)
                    {
                        return false;
                    }

                    var nurseLatitude = address.Latitude.GetValueOrDefault();
                    var nurseLongitude = address.Longitude.GetValueOrDefault();
                    var distanceKm = CalculateDistanceKm(latitude.Value, longitude.Value, nurseLatitude, nurseLongitude);
                    return distanceKm <= Math.Max(np.ServiceRadiusKm, 1);
                })
                .ToList();
            nurseProfileIds = nurses.Select(np => np.Id).ToList();
            nurseUserIds = nurses.Select(np => np.UserId).ToList();
        }

        var slots = await _context.AvailabilitySlots
            .Where(a => nurseProfileIds.Contains(a.NurseProfileId) && a.EndTime >= now)
            .OrderBy(a => a.StartTime)
            .ToListAsync();

        var selectedService = serviceId.HasValue
            ? await _context.Services.FirstOrDefaultAsync(s => s.Id == serviceId.Value)
            : null;

        var completedBookingCounts = await _context.Bookings
            .Where(b => nurseUserIds.Contains(b.NurseId) && b.Status == BookingStatuses.Completed)
            .GroupBy(b => b.NurseId)
            .Select(g => new { NurseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.NurseId, x => x.Count);

        var reviewCounts = await _context.Reviews
            .Where(r => nurseUserIds.Contains(r.NurseId) && !r.IsDeleted)
            .GroupBy(r => r.NurseId)
            .Select(g => new { NurseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.NurseId, x => x.Count);

        var busySlotIds = await _context.Bookings
            .Where(b =>
                b.AvailabilitySlotId != null &&
                b.Status != BookingStatuses.Cancelled &&
                b.Status != BookingStatuses.Rejected)
            .Select(b => b.AvailabilitySlotId!.Value)
            .ToListAsync();

        var busySlotIdSet = busySlotIds.ToHashSet();

        var ranked = nurses.Select(np =>
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
            var completedBookings = completedBookingCounts.GetValueOrDefault(np.UserId);
            var totalReviews = reviewCounts.GetValueOrDefault(np.UserId);
            var specialtyMatches = SpecialtyMatchesService(np, selectedService);
            var score = CalculateMatchScore(
                np,
                nurseService?.Price,
                distanceKm,
                nextAvailableAt,
                districtMatches,
                specialtyMatches,
                completedBookings,
                totalReviews);

            var matchReasons = BuildMatchReasons(np, distanceKm, nextAvailableAt, districtMatches, specialtyMatches, completedBookings, totalReviews);
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
                DistanceSource = distanceKm.HasValue ? "straight_line_from_customer_address" : null,
                MatchScore = score,
                MatchReasons = matchReasons,
                AiMatchSummary = BuildFallbackAiSummary(np.User.FullName, matchReasons, distanceKm),
                AiSummaryFallback = true,
                CompletedBookings = completedBookings,
                TotalReviews = totalReviews,
                NextAvailableAt = nextAvailableAt,
                District = address?.District
            };
        })
        .OrderByDescending(n => string.Equals(sortBy, "bestMatch", StringComparison.OrdinalIgnoreCase) ? n.MatchScore : (int)Math.Round(n.AverageRating * 20))
        .ThenBy(n => n.DistanceKm ?? double.MaxValue)
        .ThenByDescending(n => n.YearsExperience)
        .ToList();

        await TryEnhanceMatchSummariesAsync(ranked);
        return ranked;
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

        var address = await _context.Addresses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Type == "nurse_base" && a.IsDefault);

        return new NurseProfileDetailDto
        {
            UserId = profile.UserId,
            FullName = profile.User.FullName,
            Email = profile.User.Email ?? string.Empty,
            Phone = profile.User.PhoneNumber,
            Avatar = profile.User.Avatar,
            Address = address?.FullAddress,
            Ward = address?.Ward,
            District = address?.District,
            Latitude = address?.Latitude,
            Longitude = address?.Longitude,
            DefaultAddress = address == null ? null : new
            {
                fullAddress = address.FullAddress,
                ward = address.Ward,
                district = address.District,
                latitude = address.Latitude,
                longitude = address.Longitude
            },
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

    private static int CalculateMatchScore(
        NurseProfile profile,
        decimal? price,
        double? distanceKm,
        DateTime? nextAvailableAt,
        bool districtMatches,
        bool specialtyMatches,
        int completedBookings,
        int totalReviews)
    {
        var distanceScore = distanceKm.HasValue
            ? Math.Clamp(25 - (distanceKm.Value / Math.Max(profile.ServiceRadiusKm, 1) * 25), 0, 25)
            : districtMatches ? 20 : 12;
        var ratingScore = Math.Clamp((double)profile.AverageRating / 5 * 20, 0, 20);
        var experienceScore = Math.Clamp(profile.YearsExperience / 10d * 15, 0, 15);
        var priceScore = price.HasValue
            ? Math.Clamp(10 - ((double)Math.Max(price.Value - 350_000m, 0m) / 1_200_000d * 10), 0, 10)
            : 5;
        var availabilityScore = nextAvailableAt.HasValue
            ? Math.Clamp(10 - Math.Max((nextAvailableAt.Value - DateTime.UtcNow).TotalDays, 0), 2, 10)
            : 0;
        var specialtyScore = specialtyMatches ? 15 : 6;
        var reliabilityScore = Math.Clamp(completedBookings / 40d * 3, 0, 3) +
                               Math.Clamp(totalReviews / 20d * 2, 0, 2);

        return (int)Math.Round(distanceScore + ratingScore + experienceScore + priceScore + availabilityScore + specialtyScore + reliabilityScore);
    }

    private static List<string> BuildMatchReasons(
        NurseProfile profile,
        double? distanceKm,
        DateTime? nextAvailableAt,
        bool districtMatches,
        bool specialtyMatches,
        int completedBookings,
        int totalReviews)
    {
        var reasons = new List<string>();

        if (specialtyMatches)
        {
            reasons.Add("Chuyên môn khớp dịch vụ");
        }

        if (distanceKm.HasValue)
        {
            reasons.Add($"Cách bạn {distanceKm.Value:0.0}km đường thẳng");
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

        if (completedBookings >= 10)
        {
            reasons.Add($"{completedBookings} booking hoàn thành");
        }
        else if (totalReviews >= 5)
        {
            reasons.Add($"{totalReviews} đánh giá xác thực");
        }

        if (nextAvailableAt.HasValue)
        {
            reasons.Add(nextAvailableAt.Value.Date == DateTime.UtcNow.Date ? "Có lịch rảnh hôm nay" : "Có lịch rảnh gần nhất");
        }

        return reasons;
    }

    private async Task TryEnhanceMatchSummariesAsync(List<NurseDiscoveryDto> ranked)
    {
        var top = ranked.Take(5).ToList();
        if (top.Count == 0)
        {
            return;
        }

        try
        {
            var response = await _llmService.GenerateAsync(new GeminiGenerateRequest
            {
                SystemInstruction = """
Bạn viết giải thích ngắn cho danh sách y tá CareMate. Không đổi thứ tự, không thêm y tá mới.
Chỉ trả JSON object: {"summaries":[{"userId":1,"summary":"..."}]}.
Mỗi summary tối đa 140 ký tự, tiếng Việt thân thiện.
""",
                Prompt = JsonSerializer.Serialize(top.Select(n => new
                {
                    n.UserId,
                    n.FullName,
                    n.Specialization,
                    n.AverageRating,
                    n.YearsExperience,
                    n.DistanceKm,
                    n.MatchReasons
                })),
                Temperature = 0.2,
                MaxOutputTokens = 500
            }, CancellationToken.None);

            var parsed = ParseMatchSummaries(response.Text);
            foreach (var nurse in ranked)
            {
                if (parsed.TryGetValue(nurse.UserId, out var summary) && !string.IsNullOrWhiteSpace(summary))
                {
                    nurse.AiMatchSummary = summary.Length <= 180 ? summary : summary[..180].TrimEnd() + "...";
                    nurse.AiSummaryFallback = false;
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Gemini nurse match summary failed. Falling back to match reasons.");
        }
    }

    private static Dictionary<int, string> ParseMatchSummaries(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return [];
        }

        var payload = JsonSerializer.Deserialize<MatchSummaryPayload>(text[start..(end + 1)], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return payload?.Summaries
            .Where(x => x.UserId > 0 && !string.IsNullOrWhiteSpace(x.Summary))
            .GroupBy(x => x.UserId)
            .ToDictionary(x => x.Key, x => x.First().Summary.Trim()) ?? [];
    }

    private static string BuildFallbackAiSummary(string name, List<string> reasons, double? distanceKm)
    {
        var reasonText = reasons.Count > 0 ? string.Join(", ", reasons.Take(2)) : "hồ sơ phù hợp với nhu cầu chăm sóc";
        var distance = distanceKm.HasValue ? $" Khoảng cách khoảng {distanceKm.Value:0.0}km." : string.Empty;
        return $"Y tá {name} phù hợp vì {reasonText}.{distance}";
    }

    private sealed class MatchSummaryPayload
    {
        public List<MatchSummaryItem> Summaries { get; set; } = [];
    }

    private sealed class MatchSummaryItem
    {
        public int UserId { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    private static bool SpecialtyMatchesService(NurseProfile profile, Service? service)
    {
        if (service == null)
        {
            return false;
        }

        var profileText = NormalizeSearchText($"{profile.Specialization} {profile.Bio} {profile.Certificates}");
        if (string.IsNullOrWhiteSpace(profileText))
        {
            return false;
        }

        var serviceTerms = new[]
            {
                service.Name,
                service.Category,
                service.Description,
                service.IncludedServiceKeys,
            }
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .SelectMany(term => NormalizeSearchText(term).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(term => term.Length >= 4)
            .Distinct()
            .ToList();

        if (serviceTerms.Count == 0)
        {
            return false;
        }

        return serviceTerms.Any(profileText.Contains);
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

    private static string NormalizeDistrict(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant()
            .Replace("district", string.Empty)
            .Replace("quận", string.Empty)
            .Replace("quan", string.Empty)
            .Trim();
        var formD = normalized.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant()
            .Replace("-", " ")
            .Replace("_", " ");
        var formD = normalized.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
