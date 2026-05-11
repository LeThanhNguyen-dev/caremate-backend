using MomCare.Dto;

namespace MomCare.Interfaces;

public interface INotificationQueryService
{
    Task<IEnumerable<NotificationDto>> GetMyNotificationsAsync(int userId);
    Task<int> GetUnreadCountAsync(int userId);
    Task<bool> MarkAsReadAsync(int userId, int notificationId);
    Task<int> MarkAllAsReadAsync(int userId);
    Task<bool> DeleteAsync(int userId, int notificationId);
    Task<int> DeleteAllAsync(int userId);
}
