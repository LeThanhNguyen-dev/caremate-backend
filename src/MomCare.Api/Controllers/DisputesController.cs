using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[ApiController]
[Route("api/disputes")]
[Authorize]
public class DisputesController : ControllerBase
{
    private readonly IDisputeService _disputeService;

    public DisputesController(IDisputeService disputeService)
    {
        _disputeService = disputeService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDisputeDto dto)
    {
        var userId = GetUserId();
        var dispute = await _disputeService.CreateAsync(userId, dto);
        if (dispute == null)
        {
            return BadRequest(new { message = "Cannot create dispute" });
        }

        return Ok(dispute);
    }

    [HttpGet]
    public async Task<IActionResult> GetMineOrAll()
    {
        var userId = GetUserId();
        var isAdmin = User.IsInRole(AppRoles.Admin);
        var disputes = await _disputeService.GetDisputesAsync(userId, isAdmin);
        return Ok(disputes);
    }

    [HttpPatch("{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateDisputeStatusDto dto)
    {
        var ok = await _disputeService.UpdateStatusAsync(id, dto);
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
