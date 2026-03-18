using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using System.Security.Claims;

namespace MomCare.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{AppRoles.NurseUnconfirmed},{AppRoles.NurseConfirmed}")]
public class NurseController : ControllerBase
{
    private readonly INurseService _nurseService;

    public NurseController(INurseService nurseService)
    {
        _nurseService = nurseService;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetUserId();
        var profile = await _nurseService.GetProfileAsync(userId);
        
        if (profile == null) return NotFound("Profile not found");

        return Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateNurseProfileDto updateDto)
    {
        var userId = GetUserId();
        var result = await _nurseService.UpdateProfileAsync(userId, updateDto);
        
        if (!result) return BadRequest("Update failed");

        return Ok(new { message = "Profile updated successfully" });
    }

    [HttpPost("documents")]
    public async Task<IActionResult> UploadDocument([FromBody] UploadDocumentDto uploadDto)
    {
        var userId = GetUserId();
        var result = await _nurseService.AddDocumentAsync(userId, uploadDto);
        
        if (!result) return BadRequest("Upload failed");

        return Ok(new { message = "Document uploaded successfully" });
    }

    private int GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }
}
