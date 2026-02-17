using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("nurses/pending")]
    public async Task<IActionResult> GetPendingNurses()
    {
        var result = await _adminService.GetPendingNursesAsync();
        return Ok(result);
    }

    [HttpGet("nurses/{id}/details")]
    public async Task<IActionResult> GetNurseDetails(int id)
    {
        var result = await _adminService.GetNurseDetailsAsync(id);
        if (result == null) return NotFound("Nurse not found");
        return Ok(result);
    }

    [HttpPost("nurses/{id}/review")]
    public async Task<IActionResult> ReviewNurse(int id, [FromBody] ReviewNurseProfileDto reviewDto)
    {
        var result = await _adminService.ReviewNurseAsync(id, reviewDto);
        if (!result) return BadRequest("Review failed");

        return Ok(new { message = reviewDto.IsApproved ? "Nurse confirmed" : "Nurse rejected" });
    }
}
