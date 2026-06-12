using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using Microsoft.AspNetCore.RateLimiting;

namespace MomCare.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Customer)]
    [EnableRateLimiting("booking")]
    public async Task<IActionResult> Create([FromBody] CreateBookingDto dto)
    {
        var customerId = GetUserId();
        var result = await _bookingService.CreateBookingAsync(customerId, dto);
        if (!result.Success)
        {
            return BadRequest(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpGet("my/customer")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> GetMyAsCustomer()
    {
        var customerId = GetUserId();
        var bookings = await _bookingService.GetCustomerBookingsAsync(customerId);
        return Ok(bookings);
    }

    [HttpGet("my/nurse")]
    [Authorize(Roles = $"{AppRoles.NurseConfirmed},{AppRoles.NurseUnconfirmed}")]
    public async Task<IActionResult> GetMyAsNurse()
    {
        var nurseId = GetUserId();
        var bookings = await _bookingService.GetNurseBookingsAsync(nurseId);
        return Ok(bookings);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var userId = GetUserId();
        var isAdmin = User.IsInRole(AppRoles.Admin);

        var booking = await _bookingService.GetBookingDetailAsync(userId, id, isAdmin);
        if (booking == null)
        {
            return NotFound();
        }

        return Ok(booking);
    }

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> GetHistory(int id)
    {
        var userId = GetUserId();
        var isAdmin = User.IsInRole(AppRoles.Admin);

        var history = await _bookingService.GetBookingHistoryAsync(userId, id, isAdmin);
        if (history == null)
        {
            return NotFound();
        }

        return Ok(history);
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBookingStatusDto dto)
    {
        var userId = GetUserId();
        var isAdmin = User.IsInRole(AppRoles.Admin);

        var result = await _bookingService.UpdateBookingStatusAsync(userId, isAdmin, dto, id);
        if (!result.Success)
        {
            return BadRequest(new { message = result.Error });
        }

        return NoContent();
    }

    [HttpPost("{id}/cancel")]
    [Authorize(Roles = $"{AppRoles.Customer},{AppRoles.Admin}")]
    public async Task<IActionResult> Cancel([FromRoute] int id, [FromBody] CancelBookingDto dto)
    {
        var userId = GetUserId();
        var isAdmin = User.IsInRole(AppRoles.Admin);
        var result = await _bookingService.CancelBookingAsync(userId, isAdmin, id, dto);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Error });
        }

        return Ok(new { message = "Booking cancelled successfully" });
    }

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.Parse(raw ?? "0");
    }
}
