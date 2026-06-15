using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MomCare.Dto;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[ApiController]
[Route("api/ai-chat")]
[Authorize]
public class AiChatController : ControllerBase
{
    private readonly IAiChatService _aiChatService;

    public AiChatController(IAiChatService aiChatService)
    {
        _aiChatService = aiChatService;
    }

    [HttpPost("conversations")]
    [EnableRateLimiting("chat")]
    public async Task<IActionResult> CreateConversation(CancellationToken cancellationToken)
    {
        var result = await _aiChatService.CreateConversationAsync(GetUserId(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        var result = await _aiChatService.GetConversationsAsync(GetUserId(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    [EnableRateLimiting("chat")]
    public async Task<IActionResult> SendMessage(Guid conversationId, [FromBody] SendAiChatMessageDto dto, CancellationToken cancellationToken)
    {
        var result = await _aiChatService.SendMessageAsync(GetUserId(), conversationId, dto.Content, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Error });
    }

    [HttpPost("messages")]
    [EnableRateLimiting("chat")]
    public async Task<IActionResult> SendOrCreateMessage([FromBody] SendAiChatMessageDto dto, CancellationToken cancellationToken)
    {
        var result = await _aiChatService.SendOrCreateMessageAsync(GetUserId(), dto.Content, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Error });
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid conversationId, CancellationToken cancellationToken)
    {
        var result = await _aiChatService.GetMessagesAsync(GetUserId(), conversationId, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Error });
    }

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.Parse(raw ?? "0");
    }
}
