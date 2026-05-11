using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IRealtimeNotifier
{
    Task NotifyNotificationCreatedAsync(NotificationDto notification, int unreadCount);
    Task NotifyNotificationReadAsync(int userId, int notificationId, int unreadCount);
    Task NotifyAllNotificationsReadAsync(int userId);
    Task NotifyChatMessageReceivedAsync(int conversationId, ChatMessageDto message);
    Task NotifyChatMessagesReadAsync(int conversationId, IReadOnlyCollection<int> messageIds, int readerUserId);

    // Booking real-time events
    Task NotifyBookingCreatedAsync(int nurseUserId, BookingDetailDto booking);
    Task NotifyBookingStatusChangedAsync(int targetUserId, BookingDetailDto booking);

    // Review real-time events
    Task NotifyNewReviewAsync(int nurseUserId, ReviewDetailDto review, decimal newAverageRating);

    // Availability real-time events (optional)
    Task NotifyAvailabilityChangedAsync(int nurseUserId);
}
