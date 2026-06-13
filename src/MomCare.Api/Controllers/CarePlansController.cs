using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[ApiController]
[Authorize]
public class CarePlansController : ControllerBase
{
    private readonly ICarePlanService _carePlanService;

    public CarePlansController(ICarePlanService carePlanService)
    {
        _carePlanService = carePlanService;
    }

    [HttpPost("api/care-plans/recommend")]
    [EnableRateLimiting("health-checkin")]
    public async Task<IActionResult> Recommend([FromBody] CarePlanRecommendRequest request, CancellationToken cancellationToken)
    {
        var result = await _carePlanService.RecommendAsync(GetUserId(), request, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Error });
    }

    [HttpPost("api/bookings/{bookingId:int}/care-plan/generate")]
    [EnableRateLimiting("health-checkin")]
    public async Task<IActionResult> GenerateForBooking(int bookingId, CancellationToken cancellationToken)
    {
        var result = await _carePlanService.GenerateForBookingAsync(GetUserId(), IsAdmin(), bookingId, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Error });
    }

    [HttpGet("api/bookings/{bookingId:int}/care-plan")]
    public async Task<IActionResult> GetForBooking(int bookingId, CancellationToken cancellationToken)
    {
        var result = await _carePlanService.GetForBookingAsync(GetUserId(), IsAdmin(), bookingId, cancellationToken);
        return result.Success ? Ok(result.Data) : NotFound(new { message = result.Error });
    }

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.Parse(raw ?? "0");
    }

    private bool IsAdmin() => User.IsInRole(AppRoles.Admin);
}
