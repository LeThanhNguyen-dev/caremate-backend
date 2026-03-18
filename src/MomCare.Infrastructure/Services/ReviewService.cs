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

    public ReviewService(MomCareContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<bool> CreateReviewAsync(int customerId, CreateReviewDto dto)
    {
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == dto.BookingId);
        if (booking == null || booking.CustomerId != customerId)
        {
            return false;
        }

        if (booking.Status != BookingStatuses.Completed)
        {
            return false;
        }

        var existing = await _context.Reviews.AnyAsync(r => r.BookingId == dto.BookingId);
        if (existing)
        {
            return false;
        }

        var review = new Review
        {
            BookingId = booking.Id,
            CustomerId = customerId,
            NurseId = booking.NurseId,
            Rating = dto.Rating,
            Comment = dto.Comment,
            CreatedAt = DateTime.UtcNow
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var nurseProfile = await _context.NurseProfiles.FirstOrDefaultAsync(n => n.UserId == booking.NurseId);
        if (nurseProfile != null)
        {
            var avg = await _context.Reviews
                .Where(r => r.NurseId == booking.NurseId)
                .AverageAsync(r => (decimal)r.Rating);
            nurseProfile.AverageRating = decimal.Round(avg, 2);
            await _context.SaveChangesAsync();
        }

        await _notificationService.CreateAsync(booking.NurseId, "New review", $"You received a new review for booking #{booking.Id}.", "review");

        return true;
    }
}
