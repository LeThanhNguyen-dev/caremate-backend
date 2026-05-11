using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class DisputeService : IDisputeService
{
    private readonly MomCareContext _context;
    private readonly INotificationService _notificationService;

    public DisputeService(MomCareContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<DisputeDto?> CreateAsync(int actorUserId, CreateDisputeDto dto)
    {
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == dto.BookingId);
        if (booking == null)
        {
            return null;
        }

        if (booking.CustomerId != actorUserId && booking.NurseId != actorUserId)
        {
            return null;
        }

        var existing = await _context.Disputes.FirstOrDefaultAsync(d => d.BookingId == dto.BookingId);
        if (existing != null)
        {
            return null;
        }

        var dispute = new Dispute
        {
            BookingId = dto.BookingId,
            Reason = dto.Reason,
            Status = "open",
            CreatedAt = DateTime.UtcNow
        };

        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var receiverId = booking.CustomerId == actorUserId ? booking.NurseId : booking.CustomerId;
        await _notificationService.CreateAsync(receiverId, "Dispute opened", $"A dispute has been opened for booking #{booking.Id}.", "dispute");

        return MapDispute(dispute);
    }

    public async Task<IEnumerable<DisputeDto>> GetDisputesAsync(int actorUserId, bool isAdmin)
    {
        List<Dispute> disputes;

        if (isAdmin)
        {
            disputes = await _context.Disputes
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }
        else
        {
            disputes = await _context.Disputes
                .Where(d => _context.Bookings.Any(b => b.Id == d.BookingId && (b.CustomerId == actorUserId || b.NurseId == actorUserId)))
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        return disputes.Select(MapDispute).ToList();
    }

    public async Task<bool> UpdateStatusAsync(int disputeId, UpdateDisputeStatusDto dto)
    {
        var dispute = await _context.Disputes.FirstOrDefaultAsync(d => d.Id == disputeId);
        if (dispute == null)
        {
            return false;
        }

        dispute.Status = dto.Status;
        dispute.AdminNote = dto.AdminNote;

        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == dispute.BookingId);
        await _context.SaveChangesAsync();

        if (booking != null)
        {
            await _notificationService.CreateAsync(booking.CustomerId, "Dispute updated", $"Dispute for booking #{booking.Id} is now '{dispute.Status}'.", "dispute");
            await _notificationService.CreateAsync(booking.NurseId, "Dispute updated", $"Dispute for booking #{booking.Id} is now '{dispute.Status}'.", "dispute");
        }

        return true;
    }

    private static DisputeDto MapDispute(Dispute d) => new()
    {
        Id = d.Id,
        BookingId = d.BookingId,
        Reason = d.Reason,
        Status = d.Status,
        AdminNote = d.AdminNote,
        CreatedAt = d.CreatedAt
    };
}
