using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace MomCare.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{AppRoles.NurseUnconfirmed},{AppRoles.NurseConfirmed}")]
public class NurseController : ControllerBase
{
    private readonly INurseService _nurseService;
    private readonly ICccdOcrService _cccdOcrService;

    public NurseController(INurseService nurseService, ICccdOcrService cccdOcrService)
    {
        _nurseService = nurseService;
        _cccdOcrService = cccdOcrService;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetUserId();
        var profile = await _nurseService.GetProfileAsync(userId);
        
        if (profile == null) return NotFound(new { message = "Profile not found" });

        return Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateNurseProfileDto updateDto)
    {
        var userId = GetUserId();
        var result = await _nurseService.UpdateProfileAsync(userId, updateDto);
        
        if (!result) return BadRequest(new { message = "Update failed" });

        return Ok(new { message = "Profile updated successfully" });
    }

    [HttpPost("profile/avatar")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file)
    {
        try
        {
            var userId = GetUserId();
            var avatarUrl = await _nurseService.UploadAvatarAsync(userId, file);
            if (avatarUrl == null) return BadRequest(new { message = "Avatar upload failed." });

            return Ok(new { avatar = avatarUrl });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Upload a document (ID card front/back or certificate) to Cloudinary with private access.
    /// </summary>
    [HttpPost("documents")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5MB max
    public async Task<IActionResult> UploadDocument([FromForm] UploadDocumentDto uploadDto)
    {
        try
        {
            var userId = GetUserId();
            var result = await _nurseService.UploadDocumentAsync(userId, uploadDto);

            if (result == null) return BadRequest(new { message = "Upload failed. Nurse profile not found." });

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("documents/batch")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocumentsBatch([FromForm] UploadDocumentsDto uploadDto)
    {
        try
        {
            var userId = GetUserId();
            var result = await _nurseService.UploadDocumentsAsync(userId, uploadDto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("documents/ocr")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> OcrCccd([FromForm] CccdOcrRequestDto ocrDto, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _cccdOcrService.ExtractAsync(ocrDto.Type, ocrDto.File, cancellationToken);
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

    /// <summary>
    /// Replace an existing document with a new file.
    /// </summary>
    [HttpPut("documents/{documentId:int}")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> ReplaceDocument(int documentId, [FromForm] UploadDocumentDto uploadDto)
    {
        try
        {
            var userId = GetUserId();
            var result = await _nurseService.ReplaceDocumentAsync(userId, documentId, uploadDto);

            if (result == null) return NotFound(new { message = "Document not found" });

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a document and remove it from Cloudinary.
    /// </summary>
    [HttpDelete("documents/{documentId:int}")]
    public async Task<IActionResult> DeleteDocument(int documentId)
    {
        var userId = GetUserId();
        var result = await _nurseService.DeleteDocumentAsync(userId, documentId);

        if (!result) return NotFound(new { message = "Document not found" });

        return NoContent();
    }

    /// <summary>
    /// Get a temporary signed URL for a private document.
    /// </summary>
    [HttpGet("documents/{documentId:int}/url")]
    public async Task<IActionResult> GetDocumentUrl(int documentId)
    {
        var userId = GetUserId();
        var url = await _nurseService.GetDocumentSignedUrlAsync(userId, documentId);

        if (url == null) return NotFound(new { message = "Document not found" });

        return Ok(new { url });
    }

    [HttpPost("verification/submit")]
    public async Task<IActionResult> SubmitVerification()
    {
        try
        {
            var userId = GetUserId();
            var ok = await _nurseService.SubmitVerificationAsync(userId);
            if (!ok) return BadRequest(new { message = "Submit verification failed." });
            return Ok(new { message = "Verification dossier submitted successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private int GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }
}
