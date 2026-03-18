using MomCare.Models;

namespace MomCare.Interfaces;

public interface IChatService
{
    Task<Conversation?> GetOrCreateConversationAsync(int actorUserId, int bookingId);
    Task<IEnumerable<ChatMessage>> GetMessagesAsync(int actorUserId, int conversationId);
    Task<ChatMessage?> SendMessageAsync(int actorUserId, int conversationId, string content);
}
