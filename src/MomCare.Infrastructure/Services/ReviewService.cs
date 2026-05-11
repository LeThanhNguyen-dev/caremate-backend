using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class ReviewService : IReviewService
{
    private readonly MomCareContext _context;
    private readonly INotificationService _notificationService;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private const int EditLimitHours = 24;

    public ReviewService(
        MomCareContext context,
        INotificationService notificationService,
        IRealtimeNotifier realtimeNotifier)
    {
        _context = context;
        _notificationService = notificationService;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<bool> CreateReviewAsync(int customerId, CreateReviewDto dto)
    {
        var booking = await _context.Bookings
            .Include(b => b.Service)
            .FirstOrDefaultAsync(b => b.Id == dto.BookingId);

        if (booking == null || booking.CustomerId != customerId) return false;
        if (booking.Status != BookingStatuses.Completed) return false;

        // Prevent duplicate reviews (including soft-deleted ones for unique constraint)
        var existing = await _context.Reviews.AnyAsync(r => r.BookingId == dto.BookingId);
        if (existing) return false;

        var review = new Review
        {
            BookingId = booking.Id,
            CustomerId = customerId,
            NurseId = booking.NurseId,
            Rating = dto.Rating,
            Comment = dto.Comment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        await RecalculateNurseRatingAsync(booking.NurseId);

        await _notificationService.CreateAsync(
            booking.NurseId,
            "New review",
            $"You received a {dto.Rating}-star review for booking #{booking.Id}.",
            "review");

        var customer = await _context.Users.FindAsync(customerId);
        var nurseProfile = await _context.NurseProfiles.FirstOrDefaultAsync(n => n.UserId == booking.NurseId);

        var reviewDetail = new ReviewDetailDto
        {
            Id = review.Id,
            BookingId = review.BookingId,
            CustomerId = review.CustomerId,
            CustomerName = customer?.FullName ?? "Customer",
            CustomerAvatar = customer?.Avatar,
            ServiceId = booking.ServiceId,
            ServiceName = booking.Service.Name,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt
        };

        await _realtimeNotifier.NotifyNewReviewAsync(booking.NurseId, reviewDetail, nurseProfile?.AverageRating ?? 0);

        return true;
    }

    public async Task<bool> UpdateReviewAsync(int customerId, int reviewId, UpdateReviewDto dto)
    {
        var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId && r.CustomerId == customerId && !r.IsDeleted);
        if (review == null) return false;

        // Edit limit check (e.g., 24 hours)
        if (DateTime.UtcNow > review.CreatedAt.AddHours(EditLimitHours)) return false;

        review.Rating = dto.Rating;
        review.Comment = dto.Comment;
        review.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await RecalculateNurseRatingAsync(review.NurseId);

        return true;
    }

    public async Task<bool> DeleteReviewAsync(int customerId, int reviewId)
    {
        var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId && r.CustomerId == customerId && !r.IsDeleted);
        if (review == null) return false;

        // Soft delete
        review.IsDeleted = true;
        review.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await RecalculateNurseRatingAsync(review.NurseId);

        return true;
    }

    public async Task<IEnumerable<ReviewDetailDto>> GetNurseReviewsAsync(int nurseUserId, int page = 1, int pageSize = 10)
    {
        return await _context.Reviews
            .Where(r => r.NurseId == nurseUserId && !r.IsDeleted)
            .Include(r => r.Booking)
                .ThenInclude(b => b.Service)
            .Include(r => r.Customer)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReviewDetailDto
            {
                Id = r.Id,
                BookingId = r.BookingId,
                CustomerId = r.CustomerId,
                CustomerName = r.Customer.FullName,
                CustomerAvatar = r.Customer.Avatar,
                ServiceId = r.Booking.ServiceId,
                ServiceName = r.Booking.Service.Name,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<NurseRatingDto> GetNurseRatingAsync(int nurseUserId)
    {
        var reviews = await _context.Reviews
            .Where(r => r.NurseId == nurseUserId && !r.IsDeleted)
            .ToListAsync();

        var distribution = new Dictionary<int, int>();
        for (int i = 1; i <= 5; i++)
        {
            distribution[i] = reviews.Count(r => r.Rating == i);
        }

        return new NurseRatingDto
        {
            AverageRating = reviews.Count > 0 ? decimal.Round((decimal)reviews.Average(r => r.Rating), 2) : 0,
            TotalReviews = reviews.Count,
            RatingDistribution = distribution
        };
    }

    private async Task RecalculateNurseRatingAsync(int nurseUserId)
    {
        var nurseProfile = await _context.NurseProfiles.FirstOrDefaultAsync(n => n.UserId == nurseUserId);
        if (nurseProfile == null) return;

        var activeReviews = await _context.Reviews
            .Where(r => r.NurseId == nurseUserId && !r.IsDeleted)
            .ToListAsync();

        if (activeReviews.Count > 0)
        {
            var avg = activeReviews.Average(r => (decimal)r.Rating);
            nurseProfile.AverageRating = decimal.Round((decimal)avg, 2);
        }
        else
        {
            nurseProfile.AverageRating = 0;
        }

        await _context.SaveChangesAsync();
    }
}
