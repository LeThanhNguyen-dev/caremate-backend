using Microsoft.AspNetCore.SignalR;
using MomCare.Api.Hubs;
using MomCare.Dto;
using MomCare.Interfaces;

namespace MomCare.Api.Realtime;

public class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _notificationHubContext;
    private readonly IHubContext<ChatHub> _chatHubContext;

    public SignalRRealtimeNotifier(
        IHubContext<NotificationHub> notificationHubContext,
        IHubContext<ChatHub> chatHubContext)
    {
        _notificationHubContext = notificationHubContext;
        _chatHubContext = chatHubContext;
    }

    // ── Notification Events ──────────────────────────────────────────

    public Task NotifyNotificationCreatedAsync(NotificationDto notification, int unreadCount)
    {
        return Task.WhenAll(
            _notificationHubContext.Clients.User(notification.UserId.ToString())
                .SendAsync("NotificationReceived", notification),
            _notificationHubContext.Clients.User(notification.UserId.ToString())
                .SendAsync("NotificationUnreadCountChanged", unreadCount));
    }

    public Task NotifyNotificationReadAsync(int userId, int notificationId, int unreadCount)
    {
        return Task.WhenAll(
            _notificationHubContext.Clients.User(userId.ToString())
                .SendAsync("NotificationRead", notificationId),
            _notificationHubContext.Clients.User(userId.ToString())
                .SendAsync("NotificationUnreadCountChanged", unreadCount));
    }

    public Task NotifyAllNotificationsReadAsync(int userId)
    {
        return Task.WhenAll(
            _notificationHubContext.Clients.User(userId.ToString())
                .SendAsync("AllNotificationsRead"),
            _notificationHubContext.Clients.User(userId.ToString())
                .SendAsync("NotificationUnreadCountChanged", 0));
    }

    // ── Chat Events ──────────────────────────────────────────────────

    public Task NotifyChatMessageReceivedAsync(int conversationId, ChatMessageDto message)
    {
        return _chatHubContext.Clients.Group(ChatHub.GetConversationGroup(conversationId))
            .SendAsync("MessageReceived", message);
    }

    public Task NotifyChatMessagesReadAsync(int conversationId, IReadOnlyCollection<int> messageIds, int readerUserId)
    {
        return _chatHubContext.Clients.Group(ChatHub.GetConversationGroup(conversationId))
            .SendAsync("MessagesRead", new
            {
                conversationId,
                messageIds,
                readerUserId
            });
    }

    // ── Booking Events ───────────────────────────────────────────────

    /// <summary>
    /// Fired when a new booking is created → notifies the nurse immediately.
    /// Client event: "BookingCreated"
    /// </summary>
    public Task NotifyBookingCreatedAsync(int nurseUserId, BookingDetailDto booking)
    {
        return _notificationHubContext.Clients.User(nurseUserId.ToString())
            .SendAsync("BookingCreated", booking);
    }

    /// <summary>
    /// Fired when booking status changes → notifies the target user (customer or nurse).
    /// Client event: "BookingStatusChanged"
    /// </summary>
    public Task NotifyBookingStatusChangedAsync(int targetUserId, BookingDetailDto booking)
    {
        return _notificationHubContext.Clients.User(targetUserId.ToString())
            .SendAsync("BookingStatusChanged", booking);
    }

    // ── Review Events ────────────────────────────────────────────────

    /// <summary>
    /// Fired when a new review is submitted → notifies the nurse with the review and updated rating.
    /// Client event: "NewReviewReceived"
    /// </summary>
    public Task NotifyNewReviewAsync(int nurseUserId, ReviewDetailDto review, decimal newAverageRating)
    {
        return _notificationHubContext.Clients.User(nurseUserId.ToString())
            .SendAsync("NewReviewReceived", new
            {
                review,
                newAverageRating
            });
    }

    // ── Availability Events ──────────────────────────────────────────

    /// <summary>
    /// Fired when nurse availability changes (slot created/deleted, booking cancelled).
    /// Broadcasts to all connected clients so discovery pages can refresh.
    /// Client event: "AvailabilityChanged"
    /// </summary>
    public Task NotifyAvailabilityChangedAsync(int nurseUserId)
    {
        // Notify the nurse themselves + any clients viewing this nurse's schedule
        return _notificationHubContext.Clients.All
            .SendAsync("AvailabilityChanged", new { nurseUserId });
    }
}
