using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MomCare.Interfaces;
using System.Security.Claims;

namespace MomCare.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly INotificationService _notificationService;

    public NotificationHub(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override async Task OnConnectedAsync()
    {
        var userIdStr = GetUserId();
        if (userIdStr != null && int.TryParse(userIdStr, out int userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

            // Load unread notifications and send to the connected client
            var unreadNotifications = await _notificationService.GetUnreadAsync(userId);
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

            // Send batch to the newly connected connection only
            await Clients.Caller.SendAsync("LoadUnreadNotifications", new
            {
                notifications = unreadNotifications,
                unreadCount = unreadCount
            });
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    private string? GetUserId()
    {
        return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
