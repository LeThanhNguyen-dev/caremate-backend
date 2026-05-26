namespace MomCare.Dto;

public class ConversationDto
{
    public int Id { get; set; }
    public int? BookingId { get; set; }
    public int User1Id { get; set; }
    public int User2Id { get; set; }
    public string Type { get; set; } = "booking";
    public string? PeerName { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public bool CanSend { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateSupportConversationDto
{
    public int? UserId { get; set; }
}

public class ChatMessageDto
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public int SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
