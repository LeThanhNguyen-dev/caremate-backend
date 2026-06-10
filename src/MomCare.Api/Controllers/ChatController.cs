using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MomCare.Dto;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var userId = GetUserId();
        var conversations = await _chatService.GetConversationsAsync(userId);
        return Ok(conversations);
    }

    [HttpPost("conversations/by-booking/{bookingId:int}")]
    [EnableRateLimiting("chat")]
    public async Task<IActionResult> GetOrCreateConversation(int bookingId)
    {
        var userId = GetUserId();
        var conversation = await _chatService.GetOrCreateConversationAsync(userId, bookingId);
        if (conversation == null)
        {
            return BadRequest(new { message = "Cannot create conversation for this booking" });
        }

        return Ok(conversation);
    }

    [HttpPost("conversations/support")]
    [EnableRateLimiting("chat")]
    public async Task<IActionResult> GetOrCreateSupportConversation([FromBody] CreateSupportConversationDto? dto)
    {
        var userId = GetUserId();
        var conversation = await _chatService.GetOrCreateSupportConversationAsync(userId, dto?.UserId);
        if (conversation == null)
        {
            return BadRequest(new { message = "Cannot create support conversation" });
        }

        return Ok(conversation);
    }

    [HttpGet("conversations/{conversationId:int}/messages")]
    public async Task<IActionResult> GetMessages(int conversationId, [FromQuery] int limit = 50, [FromQuery] int? lastMessageId = null)
    {
        var userId = GetUserId();
        var messages = await _chatService.GetMessagesAsync(userId, conversationId, limit, lastMessageId);
        return Ok(messages);
    }

    [HttpPost("conversations/{conversationId:int}/messages")]
    [EnableRateLimiting("chat")]
    public async Task<IActionResult> SendMessage(int conversationId, [FromBody] SendChatMessageDto dto)
    {
        var userId = GetUserId();
        var message = await _chatService.SendMessageAsync(userId, conversationId, dto.Content);
        if (message == null)
        {
            return BadRequest(new { message = "Unable to send message. This booking conversation may be closed." });
        }

        return Ok(message);
    }

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.Parse(raw ?? "0");
    }
}
