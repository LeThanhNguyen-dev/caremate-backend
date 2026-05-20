using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("booking/payos-link")]
    [Authorize(Roles = $"{AppRoles.Customer},{AppRoles.Admin}")]
    public async Task<IActionResult> CreatePayOSLinkForBooking([FromBody] CreatePayOSBookingPaymentDto dto)
    {
        var userId = GetUserId();
        var paymentLink = await _paymentService.CreatePayOSBookingPaymentLinkAsync(userId, dto);
        return Ok(paymentLink);
    }

    [HttpPost("booking/{bookingId:int}/payos-link")]
    [Authorize(Roles = $"{AppRoles.Customer},{AppRoles.Admin}")]
    public async Task<IActionResult> CreatePayOSLink(int bookingId, [FromBody] CreatePayOSPaymentLinkDto dto)
    {
        var userId = GetUserId();
        var isAdmin = User.IsInRole(AppRoles.Admin);

        var paymentLink = await _paymentService.CreatePayOSPaymentLinkAsync(userId, isAdmin, bookingId, dto);
        if (paymentLink == null)
        {
            return BadRequest(new { message = "Cannot create payment link for this booking" });
        }

        return Ok(paymentLink);
    }

    [HttpPut("booking/{bookingId:int}")]
    public async Task<IActionResult> Upsert(int bookingId, [FromBody] UpdatePaymentStatusDto dto)
    {
        var userId = GetUserId();
        var isAdmin = User.IsInRole(AppRoles.Admin);

        var payment = await _paymentService.UpsertPaymentAsync(userId, isAdmin, bookingId, dto);
        if (payment == null)
        {
            return BadRequest(new { message = "Cannot update payment for this booking" });
        }

        return Ok(payment);
    }

    [AllowAnonymous]
    [HttpPost("webhook/payos")]
    public async Task<IActionResult> HandlePayOSWebhook([FromBody] PayOSWebhookDto webhook)
    {
        var updated = await _paymentService.HandlePayOSWebhookAsync(webhook);
        if (!updated)
        {
            return NotFound(new { message = "Payment not found for webhook" });
        }

        return Ok(new { message = "OK" });
    }

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.Parse(raw ?? "0");
    }
}
