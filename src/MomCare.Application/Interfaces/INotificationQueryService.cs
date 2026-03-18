using MomCare.Models;

namespace MomCare.Interfaces;

public interface INotificationQueryService
{
    Task<IEnumerable<Notification>> GetMyNotificationsAsync(int userId);
    Task<bool> MarkAsReadAsync(int userId, int notificationId);
}
