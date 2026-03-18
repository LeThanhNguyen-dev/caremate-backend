using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class ChatService : IChatService
{
    private readonly MomCareContext _context;
    private readonly INotificationService _notificationService;

    public ChatService(MomCareContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<Conversation?> GetOrCreateConversationAsync(int actorUserId, int bookingId)
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
            return conversation;
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

        return conversation;
    }

    public async Task<IEnumerable<ChatMessage>> GetMessagesAsync(int actorUserId, int conversationId)
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

        var messages = await _context.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        foreach (var msg in messages.Where(m => m.SenderId != actorUserId && !m.IsRead))
        {
            msg.IsRead = true;
        }

        await _context.SaveChangesAsync();
        return messages;
    }

    public async Task<ChatMessage?> SendMessageAsync(int actorUserId, int conversationId, string content)
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

        return message;
    }
}
