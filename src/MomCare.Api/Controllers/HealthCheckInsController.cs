using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[Authorize]
[ApiController]
[Route("api/health-checkins")]
public class HealthCheckInsController : ControllerBase
{
    private readonly IHealthCheckInService _healthCheckInService;

    public HealthCheckInsController(IHealthCheckInService healthCheckInService)
    {
        _healthCheckInService = healthCheckInService;
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeHealthCheckInRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = GetUserId();
        var result = await _healthCheckInService.AnalyzeAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _healthCheckInService.GetLatestAsync(userId, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(new { message = "page and pageSize must be greater than 0." });
        }

        var userId = GetUserId();
        var result = await _healthCheckInService.GetHistoryAsync(userId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.Parse(raw ?? "0");
    }
}
