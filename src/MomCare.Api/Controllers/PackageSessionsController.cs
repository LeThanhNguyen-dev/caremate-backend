using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[ApiController]
[Route("api/bookings/{bookingId:int}/sessions")]
[Authorize]
public class PackageSessionsController : ControllerBase
{
    private readonly IPackageSessionService _sessionService;

    public PackageSessionsController(IPackageSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProgress(int bookingId)
    {
        var userId = GetUserId();
        var result = await _sessionService.GetProgressAsync(userId, bookingId);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(result.Data);
    }

    [HttpPost("checkin")]
    [Authorize(Roles = $"{AppRoles.NurseConfirmed},{AppRoles.NurseUnconfirmed}")]
    public async Task<IActionResult> CheckIn(int bookingId, [FromBody] CheckInSessionDto dto)
    {
        var userId = GetUserId();
        var result = await _sessionService.CheckInAsync(userId, bookingId, dto);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(result.Data);
    }

    [HttpPost("checkout")]
    [Authorize(Roles = $"{AppRoles.NurseConfirmed},{AppRoles.NurseUnconfirmed}")]
    public async Task<IActionResult> CheckOut(int bookingId, [FromBody] CheckOutSessionDto dto)
    {
        var userId = GetUserId();
        var result = await _sessionService.CheckOutAsync(userId, bookingId, dto);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(result.Data);
    }

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.Parse(raw ?? "0");
    }
}
