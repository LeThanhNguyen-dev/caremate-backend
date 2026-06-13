namespace MomCare.Dto;

public class CreateAiChatConversationResponse
{
    public Guid ConversationId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AiChatConversationDto
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string Status { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

public class SendAiChatMessageDto
{
    public string Content { get; set; } = string.Empty;
}

public class AiChatMessageDto
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool SafetyFlag { get; set; }
    public string? SafetyTriggeredBy { get; set; }
    public string? CtaAction { get; set; }
    public string Disclaimer { get; set; } = string.Empty;
    public bool FallbackMode { get; set; }
    public DateTime CreatedAt { get; set; }
}
