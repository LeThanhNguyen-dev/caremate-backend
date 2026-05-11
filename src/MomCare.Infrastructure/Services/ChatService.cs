using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class ChatService : IChatService
{
    private readonly MomCareContext _context;
    private readonly INotificationService _notificationService;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public ChatService(MomCareContext context, INotificationService notificationService, IRealtimeNotifier realtimeNotifier)
    {
        _context = context;
        _notificationService = notificationService;
        _realtimeNotifier = realtimeNotifier;
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

        var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.BookingId == bookingId);
        if (conversation != null)
        {
            return MapConversation(conversation);
        }

        conversation = new Conversation
        {
            BookingId = bookingId,
            User1Id = booking.CustomerId,
            User2Id = booking.NurseId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        return MapConversation(conversation);
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
            .Take(limit)
            .ToListAsync();

        // Reverse to maintain chronological order for the frontend
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

        var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conversation == null)
        {
            return null;
        }

        if (actorUserId != conversation.User1Id && actorUserId != conversation.User2Id)
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
        await _notificationService.CreateAsync(receiverId, "New message", "You have a new chat message.", "chat");

        var messageDto = MapMessage(message);
        await _realtimeNotifier.NotifyChatMessageReceivedAsync(conversationId, messageDto);

        return messageDto;
    }

    private static ConversationDto MapConversation(Conversation c) => new()
    {
        Id = c.Id,
        BookingId = c.BookingId,
        User1Id = c.User1Id,
        User2Id = c.User2Id,
        CreatedAt = c.CreatedAt
    };

    private static ChatMessageDto MapMessage(ChatMessage m) => new()
    {
        Id = m.Id,
        ConversationId = m.ConversationId,
        SenderId = m.SenderId,
        Content = m.Content,
        IsRead = m.IsRead,
        CreatedAt = m.CreatedAt
    };
}
