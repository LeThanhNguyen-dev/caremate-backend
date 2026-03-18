using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;

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
    public async Task<IActionResult> Create([FromBody] CreateBookingDto dto)
    {
        var customerId = GetUserId();
        var booking = await _bookingService.CreateBookingAsync(customerId, dto);
        if (booking == null)
        {
            return BadRequest(new { message = "Invalid booking request or slot unavailable" });
        }

        return Ok(booking);
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

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBookingStatusDto dto)
    {
        var userId = GetUserId();
        var isAdmin = User.IsInRole(AppRoles.Admin);

        var ok = await _bookingService.UpdateBookingStatusAsync(userId, isAdmin, dto, id);
        if (!ok)
        {
            return BadRequest(new { message = "Status transition is not allowed" });
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
