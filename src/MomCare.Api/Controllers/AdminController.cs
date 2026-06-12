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
    private readonly IPaymentService _paymentService;

    public AdminController(IAdminService adminService, IPaymentService paymentService)
    {
        _adminService = adminService;
        _paymentService = paymentService;
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

    [HttpGet("payments/webhook-logs")]
    public async Task<IActionResult> GetPayOsWebhookLogs([FromQuery] string? status)
    {
        var result = await _adminService.GetPayOsWebhookLogsAsync(status);
        return Ok(result);
    }

    [HttpPost("payments/webhook-logs/{logId:guid}/retry")]
    public async Task<IActionResult> RetryPayOsWebhookLog(Guid logId)
    {
        var result = await _paymentService.RetryPayOSWebhookLogAsync(logId);
        if (!result)
        {
            return BadRequest(new { message = "Unable to retry PayOS webhook log" });
        }

        return Ok(new { message = "PayOS webhook retried" });
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactionHistory(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int? userId,
        [FromQuery] int? bookingId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var result = await _adminService.GetTransactionHistoryAsync(type, status, userId, bookingId, from, to);
        return Ok(result);
    }

    [HttpGet("finance/analytics")]
    public async Task<IActionResult> GetFinanceAnalytics([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var result = await _adminService.GetFinanceAnalyticsAsync(from, to);
        return Ok(result);
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] int? actorUserId, [FromQuery] string? path, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var result = await _adminService.GetAuditLogsAsync(actorUserId, path, from, to);
        return Ok(result);
    }

    [HttpGet("settings/ocr")]
    public async Task<IActionResult> GetOcrSettings()
    {
        var result = await _adminService.GetOcrSettingsAsync();
        return Ok(result);
    }

    [HttpPost("nurses/documents/{documentId:int}/ocr")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> OcrNurseDocument(int documentId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _adminService.OcrNurseDocumentAsync(documentId, cancellationToken);
            if (result == null) return NotFound(new { message = "Document not found" });

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "FPT AI OCR quota or rate limit has been reached. Please check FPT AI billing/quota or try again later." });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    [HttpPost("ocr/reprocess/{documentId:int}")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public Task<IActionResult> ReprocessOcr(int documentId, CancellationToken cancellationToken)
    {
        return OcrNurseDocument(documentId, cancellationToken);
    }

    [HttpGet("ocr/logs/{nurseUserId:int}")]
    public async Task<IActionResult> GetOcrLogs(int nurseUserId)
    {
        var result = await _adminService.GetNurseOcrLogsAsync(nurseUserId);
        return Ok(result);
    }

    [HttpPut("nurses/{nurseUserId:int}/documents/{documentId:int}/approve")]
    public async Task<IActionResult> ApproveNurseDocument(int nurseUserId, int documentId, [FromBody] ReviewNurseDocumentDto dto)
    {
        var result = await _adminService.UpdateNurseDocumentStatusAsync(nurseUserId, documentId, DocumentStatuses.Approved, dto);
        if (!result) return NotFound(new { message = "Document not found" });

        return Ok(new { message = "Document approved" });
    }

    [HttpPut("nurses/{nurseUserId:int}/documents/{documentId:int}/reject")]
    public async Task<IActionResult> RejectNurseDocument(int nurseUserId, int documentId, [FromBody] ReviewNurseDocumentDto dto)
    {
        var result = await _adminService.UpdateNurseDocumentStatusAsync(nurseUserId, documentId, DocumentStatuses.Rejected, dto);
        if (!result) return NotFound(new { message = "Document not found" });

        return Ok(new { message = "Document rejected" });
    }
}
