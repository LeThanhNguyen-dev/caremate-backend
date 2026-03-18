using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpPost("conversations/by-booking/{bookingId:int}")]
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

    [HttpGet("conversations/{conversationId:int}/messages")]
    public async Task<IActionResult> GetMessages(int conversationId)
    {
        var userId = GetUserId();
        var messages = await _chatService.GetMessagesAsync(userId, conversationId);
        return Ok(messages);
    }

    [HttpPost("conversations/{conversationId:int}/messages")]
    public async Task<IActionResult> SendMessage(int conversationId, [FromBody] SendChatMessageDto dto)
    {
        var userId = GetUserId();
        var message = await _chatService.SendMessageAsync(userId, conversationId, dto.Content);
        if (message == null)
        {
            return BadRequest(new { message = "Unable to send message" });
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
