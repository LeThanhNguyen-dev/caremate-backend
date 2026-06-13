using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("ai_chat_messages")]
public class AiChatMessage
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("conversation_id")]
    public Guid ConversationId { get; set; }

    [Column("role")]
    [MaxLength(10)]
    public string Role { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("safety_flag")]
    public bool SafetyFlag { get; set; }

    [Column("safety_triggered_by")]
    [MaxLength(100)]
    public string? SafetyTriggeredBy { get; set; }

    [Column("fallback_mode")]
    public bool FallbackMode { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ConversationId))]
    public virtual AiChatConversation Conversation { get; set; } = null!;
}
