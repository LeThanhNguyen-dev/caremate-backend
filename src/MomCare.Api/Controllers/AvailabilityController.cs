using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[ApiController]
[Route("api/availability")]
[Authorize(Roles = $"{AppRoles.NurseUnconfirmed},{AppRoles.NurseConfirmed}")]
public class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityService _availabilityService;

    public AvailabilityController(IAvailabilityService availabilityService)
    {
        _availabilityService = availabilityService;
    }

    [HttpGet("my-slots")]
    public async Task<IActionResult> GetMySlots([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var nurseId = GetUserId();
        var slots = await _availabilityService.GetMySlotsAsync(nurseId, from, to);
        return Ok(slots);
    }

    [HttpPost("slots")]
    public async Task<IActionResult> CreateSlot([FromBody] CreateAvailabilitySlotDto dto)
    {
        var nurseId = GetUserId();
        var slot = await _availabilityService.CreateSlotAsync(nurseId, dto);
        if (slot == null)
        {
            return BadRequest(new { message = "Invalid or overlapping slot" });
        }

        return Ok(slot);
    }

    [HttpDelete("slots/{slotId:int}")]
    public async Task<IActionResult> DeleteSlot(int slotId)
    {
        var nurseId = GetUserId();
        var ok = await _availabilityService.DeleteSlotAsync(nurseId, slotId);
        if (!ok)
        {
            return BadRequest(new { message = "Slot not found or already booked" });
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
