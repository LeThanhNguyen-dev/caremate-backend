using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class SendChatMessageDto
{
    [Required]
    public string Content { get; set; } = string.Empty;
}
