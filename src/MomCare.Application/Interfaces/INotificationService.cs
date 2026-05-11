using MomCare.Dto;

namespace MomCare.Interfaces;

public interface INotificationService
{
    Task<NotificationDto> CreateAsync(int userId, string title, string content, string type = "booking");
    Task<IEnumerable<NotificationDto>> GetUnreadAsync(int userId);
    Task<int> GetUnreadCountAsync(int userId);
}
