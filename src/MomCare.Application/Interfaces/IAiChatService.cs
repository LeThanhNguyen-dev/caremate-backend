using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IAiChatService
{
    Task<CreateAiChatConversationResponse> CreateConversationAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AiChatConversationDto>> GetConversationsAsync(int userId, CancellationToken cancellationToken);
    Task<ServiceResult<AiChatMessageDto>> SendOrCreateMessageAsync(int userId, string content, CancellationToken cancellationToken);
    Task<ServiceResult<AiChatMessageDto>> SendMessageAsync(int userId, Guid conversationId, string content, CancellationToken cancellationToken);
    Task<ServiceResult<IReadOnlyList<AiChatMessageDto>>> GetMessagesAsync(int userId, Guid conversationId, CancellationToken cancellationToken);
}
