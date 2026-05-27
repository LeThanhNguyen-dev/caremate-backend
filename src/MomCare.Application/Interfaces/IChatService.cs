using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IChatService
{
    Task<IEnumerable<ConversationDto>> GetConversationsAsync(int actorUserId);
    Task<ConversationDto?> GetOrCreateConversationAsync(int actorUserId, int bookingId);
    Task<ConversationDto?> GetOrCreateSupportConversationAsync(int actorUserId, int? targetUserId = null);
    Task<IEnumerable<ChatMessageDto>> GetMessagesAsync(int actorUserId, int conversationId, int limit = 50, int? lastMessageId = null);
    Task<ChatMessageDto?> SendMessageAsync(int actorUserId, int conversationId, string content);
}
