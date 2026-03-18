namespace MomCare.Interfaces;

public interface INotificationService
{
    Task CreateAsync(int userId, string title, string content, string type = "booking");
}
