using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class NotificationQueryService : INotificationQueryService
{
    private readonly MomCareContext _context;

    public NotificationQueryService(MomCareContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Notification>> GetMyNotificationsAsync(int userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> MarkAsReadAsync(int userId, int notificationId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
        {
            return false;
        }

        notification.IsRead = true;
        return await _context.SaveChangesAsync() > 0;
    }
}
