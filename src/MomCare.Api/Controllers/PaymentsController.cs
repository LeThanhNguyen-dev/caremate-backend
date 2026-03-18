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

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.Parse(raw ?? "0");
    }
}
