using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>
    /// Submit a review for a completed booking.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
    {
        var customerId = GetUserId();
        var ok = await _reviewService.CreateReviewAsync(customerId, dto);
        if (!ok) return BadRequest(new { message = "Review is only allowed once for completed bookings" });

        return Ok(new { message = "Review created" });
    }

    /// <summary>
    /// Update an existing review within 24 hours.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateReviewDto dto)
    {
        var customerId = GetUserId();
        var ok = await _reviewService.UpdateReviewAsync(customerId, id, dto);
        if (!ok) return BadRequest(new { message = "Update failed. Review not found, not owned, or edit time expired (24h)." });

        return Ok(new { message = "Review updated" });
    }

    /// <summary>
    /// Soft delete a review.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> Delete(int id)
    {
        var customerId = GetUserId();
        var ok = await _reviewService.DeleteReviewAsync(customerId, id);
        if (!ok) return NotFound(new { message = "Review not found or not owned" });

        return NoContent();
    }

    /// <summary>
    /// Get paginated reviews for a nurse (public endpoint).
    /// </summary>
    [HttpGet("nurse/{nurseUserId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetNurseReviews(
        int nurseUserId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var reviews = await _reviewService.GetNurseReviewsAsync(nurseUserId, page, pageSize);
        return Ok(reviews);
    }

    /// <summary>
    /// Get aggregated rating for a nurse (public endpoint).
    /// </summary>
    [HttpGet("nurse/{nurseUserId:int}/rating")]
    [AllowAnonymous]
    public async Task<IActionResult> GetNurseRating(int nurseUserId)
    {
        var rating = await _reviewService.GetNurseRatingAsync(nurseUserId);
        return Ok(rating);
    }

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.Parse(raw ?? "0");
    }
}
