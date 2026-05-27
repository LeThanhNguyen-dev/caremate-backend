using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;
using Npgsql;

namespace MomCare.Services;

public class ChatService : IChatService
{
    private static readonly TimeSpan BookingChatGracePeriod = TimeSpan.FromHours(2);
    private readonly MomCareContext _context;
    private readonly INotificationService _notificationService;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public ChatService(MomCareContext context, INotificationService notificationService, IRealtimeNotifier realtimeNotifier)
    {
        _context = context;
        _notificationService = notificationService;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<IEnumerable<ConversationDto>> GetConversationsAsync(int actorUserId)
    {
        var conversations = await _context.Conversations
            .Include(c => c.Booking)
                .ThenInclude(b => b!.Service)
            .Include(c => c.Booking)
                .ThenInclude(b => b!.SessionLogs)
            .Include(c => c.User1)
            .Include(c => c.User2)
            .Where(c => c.User1Id == actorUserId || c.User2Id == actorUserId)
            .OrderByDescending(c => c.Messages.Max(m => (DateTime?)m.CreatedAt) ?? c.CreatedAt)
            .ToListAsync();

        return conversations.Select(c => MapConversation(c, actorUserId)).ToList();
    }

    public async Task<ConversationDto?> GetOrCreateConversationAsync(int actorUserId, int bookingId)
    {
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null)
        {
            return null;
        }

        if (actorUserId != booking.CustomerId && actorUserId != booking.NurseId)
        {
            return null;
        }

        var conversation = await GetBookingConversationAsync(bookingId);

        if (conversation != null)
        {
            return MapConversation(conversation, actorUserId);
        }

        conversation = new Conversation
        {
            BookingId = bookingId,
            User1Id = booking.CustomerId,
            User2Id = booking.NurseId,
            Type = "booking",
            CreatedAt = DateTime.UtcNow
        };

        _context.Conversations.Add(conversation);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "IX_conversations_booking_id"))
        {
            _context.Entry(conversation).State = EntityState.Detached;
            conversation = await GetBookingConversationAsync(bookingId);
            if (conversation == null)
            {
                throw;
            }

            return MapConversation(conversation, actorUserId);
        }

        conversation.Booking = booking;
        return MapConversation(conversation, actorUserId);
    }

    public async Task<ConversationDto?> GetOrCreateSupportConversationAsync(int actorUserId, int? targetUserId = null)
    {
        var actorIsAdmin = await IsInRoleAsync(actorUserId, AppRoles.Admin);
        var userId = actorIsAdmin ? targetUserId : actorUserId;
        if (!userId.HasValue || actorIsAdmin && userId.Value == actorUserId)
        {
            return null;
        }

        if (!await _context.Users.AnyAsync(u => u.Id == userId.Value))
        {
            return null;
        }

        var adminId = actorIsAdmin
            ? actorUserId
            : await _context.UserRoles
                .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, Role = r.Name })
                .Where(x => x.Role == AppRoles.Admin)
                .Select(x => x.UserId)
                .OrderBy(id => id)
                .FirstOrDefaultAsync();

        if (adminId == 0)
        {
            return null;
        }

        var conversation = await _context.Conversations
            .Include(c => c.User1)
            .Include(c => c.User2)
            .FirstOrDefaultAsync(c =>
                c.Type == "support" &&
                c.BookingId == null &&
                ((c.User1Id == userId.Value && c.User2Id == adminId) ||
                 (c.User1Id == adminId && c.User2Id == userId.Value)));

        if (conversation != null)
        {
            return MapConversation(conversation, actorUserId);
        }

        conversation = new Conversation
        {
            BookingId = null,
            User1Id = userId.Value,
            User2Id = adminId,
            Type = "support",
            CreatedAt = DateTime.UtcNow
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        conversation.User1 = await _context.Users.FindAsync(conversation.User1Id) ?? conversation.User1;
        conversation.User2 = await _context.Users.FindAsync(conversation.User2Id) ?? conversation.User2;
        return MapConversation(conversation, actorUserId);
    }

    public async Task<IEnumerable<ChatMessageDto>> GetMessagesAsync(int actorUserId, int conversationId, int limit = 50, int? lastMessageId = null)
    {
        var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conversation == null)
        {
            return [];
        }

        if (actorUserId != conversation.User1Id && actorUserId != conversation.User2Id)
        {
            return [];
        }

        var query = _context.ChatMessages
            .Where(m => m.ConversationId == conversationId);

        if (lastMessageId.HasValue)
        {
            query = query.Where(m => m.Id < lastMessageId.Value);
        }

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync();

        messages = messages.OrderBy(m => m.CreatedAt).ToList();

        var newlyReadMessageIds = messages
            .Where(m => m.SenderId != actorUserId && !m.IsRead)
            .Select(m => m.Id)
            .ToList();

        if (newlyReadMessageIds.Count > 0)
        {
            await _context.ChatMessages
                .Where(m => newlyReadMessageIds.Contains(m.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));

            await _realtimeNotifier.NotifyChatMessagesReadAsync(conversationId, newlyReadMessageIds, actorUserId);
        }

        return messages.Select(MapMessage).ToList();
    }

    public async Task<ChatMessageDto?> SendMessageAsync(int actorUserId, int conversationId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var conversation = await _context.Conversations
            .Include(c => c.Booking)
                .ThenInclude(b => b!.Service)
            .Include(c => c.Booking)
                .ThenInclude(b => b!.SessionLogs)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null)
        {
            return null;
        }

        if (actorUserId != conversation.User1Id && actorUserId != conversation.User2Id)
        {
            return null;
        }

        if (!CanSend(conversation))
        {
            return null;
        }

        var message = new ChatMessage
        {
            ConversationId = conversationId,
            SenderId = actorUserId,
            Content = content.Trim(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        var receiverId = actorUserId == conversation.User1Id ? conversation.User2Id : conversation.User1Id;
        await _notificationService.CreateAsync(receiverId, "Tin nhắn mới", "Bạn có một tin nhắn mới.", "chat");

        var messageDto = MapMessage(message);
        await _realtimeNotifier.NotifyChatMessageReceivedAsync(conversationId, messageDto);

        return messageDto;
    }

    private ConversationDto MapConversation(Conversation c, int actorUserId)
    {
        var peer = actorUserId == c.User1Id ? c.User2 : c.User1;
        var lastMessage = _context.ChatMessages
            .Where(m => m.ConversationId == c.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new { m.Content, m.CreatedAt })
            .FirstOrDefault();

        return new ConversationDto
        {
            Id = c.Id,
            BookingId = c.BookingId,
            User1Id = c.User1Id,
            User2Id = c.User2Id,
            Type = c.Type,
            PeerName = peer?.FullName,
            LastMessage = lastMessage?.Content,
            LastMessageAt = lastMessage?.CreatedAt,
            CanSend = CanSend(c),
            CreatedAt = c.CreatedAt
        };
    }

    private static ChatMessageDto MapMessage(ChatMessage m) => new()
    {
        Id = m.Id,
        ConversationId = m.ConversationId,
        SenderId = m.SenderId,
        Content = m.Content,
        IsRead = m.IsRead,
        CreatedAt = m.CreatedAt
    };

    private static bool CanSend(Conversation conversation)
    {
        if (conversation.Type == "support")
        {
            return true;
        }

        if (conversation.Booking == null)
        {
            return false;
        }

        var openStatuses = new[] { BookingStatuses.Confirmed, BookingStatuses.InProgress };
        if (!openStatuses.Contains(conversation.Booking.Status))
        {
            return false;
        }

        return DateTime.UtcNow <= GetBookingChatDeadline(conversation.Booking);
    }

    private static DateTime GetBookingChatDeadline(Booking booking)
    {
        var serviceDurationMinutes = Math.Max(booking.Service?.EstimatedDurationMinutes ?? 0, 1);
        var packageLastSessionEnd = booking.SessionLogs.Count == 0
            ? booking.EndTime
            : booking.SessionLogs
                .Select(session => session.SessionDate.AddMinutes(serviceDurationMinutes))
                .DefaultIfEmpty(booking.EndTime)
                .Max();

        var serviceEndTime = packageLastSessionEnd > booking.EndTime
            ? packageLastSessionEnd
            : booking.EndTime;

        return serviceEndTime.Add(BookingChatGracePeriod);
    }

    private async Task<bool> IsInRoleAsync(int userId, string role)
    {
        return await _context.UserRoles
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, Role = r.Name })
            .AnyAsync(x => x.UserId == userId && x.Role == role);
    }

    private Task<Conversation?> GetBookingConversationAsync(int bookingId)
    {
        return _context.Conversations
            .Include(c => c.Booking)
                .ThenInclude(b => b!.Service)
            .Include(c => c.Booking)
                .ThenInclude(b => b!.SessionLogs)
            .Include(c => c.User1)
            .Include(c => c.User2)
            .FirstOrDefaultAsync(c => c.BookingId == bookingId && c.Type == "booking");
    }

    private static bool IsUniqueViolation(DbUpdateException ex, string constraintName)
    {
        return ex.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && postgresException.ConstraintName == constraintName;
    }
}
