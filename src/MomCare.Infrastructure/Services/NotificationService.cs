using MomCare.Data;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class NotificationService : INotificationService
{
    private readonly MomCareContext _context;

    public NotificationService(MomCareContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(int userId, string title, string content, string type = "booking")
    {
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Content = content,
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }
}
