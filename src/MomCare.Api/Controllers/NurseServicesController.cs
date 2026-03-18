using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[ApiController]
[Route("api/nurse/services")]
[Authorize(Roles = $"{AppRoles.NurseUnconfirmed},{AppRoles.NurseConfirmed}")]
public class NurseServicesController : ControllerBase
{
    private readonly INurseServiceManagementService _serviceManagementService;

    public NurseServicesController(INurseServiceManagementService serviceManagementService)
    {
        _serviceManagementService = serviceManagementService;
    }

    [HttpPost]
    public async Task<IActionResult> AddService([FromBody] CreateNurseServiceDto dto)
    {
        var nurseUserId = GetUserId();
        var result = await _serviceManagementService.AddServiceAsync(nurseUserId, dto);

        if (result == null)
        {
            return BadRequest(new { message = "Cannot add service. Service may not exist or already offered." });
        }

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetMyServices()
    {
        var nurseUserId = GetUserId();
        var services = await _serviceManagementService.GetMyServicesAsync(nurseUserId);
        return Ok(services);
    }

    [HttpPut("{serviceId:int}")]
    public async Task<IActionResult> UpdateService(int serviceId, [FromBody] UpdateNurseServiceDto dto)
    {
        var nurseUserId = GetUserId();
        var result = await _serviceManagementService.UpdateServiceAsync(nurseUserId, serviceId, dto);

        if (result == null)
        {
            return NotFound(new { message = "Service not found" });
        }

        return Ok(result);
    }

    [HttpDelete("{serviceId:int}")]
    public async Task<IActionResult> RemoveService(int serviceId)
    {
        var nurseUserId = GetUserId();
        var ok = await _serviceManagementService.RemoveServiceAsync(nurseUserId, serviceId);

        if (!ok)
        {
            return NotFound(new { message = "Service not found" });
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
