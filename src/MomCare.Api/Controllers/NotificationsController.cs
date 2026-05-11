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

    [HttpGet("mine/unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetUserId();
        var count = await _notificationQueryService.GetUnreadCountAsync(userId);
        return Ok(new { unreadCount = count });
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

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetUserId();
        var markedCount = await _notificationQueryService.MarkAllAsReadAsync(userId);
        return Ok(new { markedCount });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetUserId();
        var ok = await _notificationQueryService.DeleteAsync(userId, id);
        if (!ok)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAll()
    {
        var userId = GetUserId();
        var deletedCount = await _notificationQueryService.DeleteAllAsync(userId);
        return Ok(new { deletedCount });
    }

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.Parse(raw ?? "0");
    }
}
