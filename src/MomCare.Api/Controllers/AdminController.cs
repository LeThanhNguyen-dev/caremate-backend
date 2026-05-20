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

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var result = await _adminService.GetUsersAsync();
        return Ok(result);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateAdminUserDto dto)
    {
        var result = await _adminService.CreateUserAsync(dto);
        if (result == null) return BadRequest(new { message = "Unable to create user" });

        return CreatedAtAction(nameof(GetUsers), new { id = result.UserId }, result);
    }

    [HttpPatch("users/{id}/status")]
    public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateAdminUserStatusDto dto)
    {
        var result = await _adminService.UpdateUserStatusAsync(id, dto);
        if (result == null) return BadRequest(new { message = "Unable to update user status" });

        return Ok(result);
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
        try
        {
            var result = await _adminService.ReviewNurseAsync(id, reviewDto);
            if (!result) return BadRequest("Review failed");

            return Ok(new { message = reviewDto.IsApproved ? "Nurse confirmed" : "Nurse rejected" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _adminService.GetDashboardAsync();
        return Ok(result);
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookings([FromQuery] string? status)
    {
        var result = await _adminService.GetBookingsAsync(status);
        return Ok(result);
    }

    [HttpGet("disputes")]
    public async Task<IActionResult> GetDisputes([FromQuery] string? status)
    {
        var result = await _adminService.GetDisputesAsync(status);
        return Ok(result);
    }

    [HttpGet("refunds")]
    public async Task<IActionResult> GetRefunds([FromQuery] string? refundStatus)
    {
        var result = await _adminService.GetRefundsAsync(refundStatus);
        return Ok(result);
    }

    [HttpPost("refunds/{bookingId:int}/complete")]
    public async Task<IActionResult> CompleteRefund(int bookingId, [FromBody] CompleteRefundDto dto)
    {
        var result = await _adminService.CompleteRefundAsync(bookingId, dto);
        if (!result)
        {
            return BadRequest(new { message = "Unable to complete refund" });
        }

        return Ok(new { message = "Refund marked as completed" });
    }

    [HttpGet("payouts")]
    public async Task<IActionResult> GetPayouts([FromQuery] string? payoutStatus)
    {
        var result = await _adminService.GetPayoutsAsync(payoutStatus);
        return Ok(result);
    }

    [HttpPost("payouts/{payoutId:int}/complete")]
    public async Task<IActionResult> CompletePayout(int payoutId, [FromBody] CompletePayoutDto dto)
    {
        var result = await _adminService.CompletePayoutAsync(payoutId, dto);
        if (!result)
        {
            return BadRequest(new { message = "Unable to complete payout" });
        }

        return Ok(new { message = "Payout marked as completed" });
    }
}
