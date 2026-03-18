using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationQueryService _notificationQueryService;

    public NotificationsController(INotificationQueryService notificationQueryService)
    {
        _notificationQueryService = notificationQueryService;
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var userId = GetUserId();
        var data = await _notificationQueryService.GetMyNotificationsAsync(userId);
        return Ok(data);
    }

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = GetUserId();
        var ok = await _notificationQueryService.MarkAsReadAsync(userId, id);
        if (!ok)
        {
            return NotFound();
        }

        return NoContent();
    }

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.Parse(raw ?? "0");
    }
}
