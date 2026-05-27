using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[ApiController]
[Route("api/nurses")]
public class NursesController : ControllerBase
{
    private readonly INurseDiscoveryService _nurseDiscoveryService;
    private readonly IAvailabilityService _availabilityService;
    private readonly IReviewService _reviewService;

    public NursesController(
        INurseDiscoveryService nurseDiscoveryService,
        IAvailabilityService availabilityService,
        IReviewService reviewService)
    {
        _nurseDiscoveryService = nurseDiscoveryService;
        _availabilityService = availabilityService;
        _reviewService = reviewService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search(
        [FromQuery] int? serviceId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] string? district,
        [FromQuery] string? sortBy)
    {
        var result = await _nurseDiscoveryService.SearchAsync(serviceId, minPrice, maxPrice, startTime, endTime, latitude, longitude, district, sortBy);
        return Ok(result);
    }

    [HttpGet("{userId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDetail(int userId)
    {
        var nurse = await _nurseDiscoveryService.GetDetailAsync(userId);
        if (nurse == null)
        {
            return NotFound();
        }

        return Ok(nurse);
    }

    /// <summary>
    /// Get all available (unbooked) slots for a nurse.
    /// </summary>
    [HttpGet("{userId:int}/availability")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailability(int userId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var slots = await _availabilityService.GetNurseSlotsAsync(userId, from, to);
        return Ok(slots);
    }

    /// <summary>
    /// Get available slots for a nurse filtered by a specific service.
    /// </summary>
    [HttpGet("{userId:int}/availability/service/{serviceId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailabilityByService(
        int userId,
        int serviceId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var slots = await _availabilityService.GetSlotsByServiceAsync(userId, serviceId, from, to);
        return Ok(slots);
    }

    /// <summary>
    /// Get paginated reviews for a nurse (public).
    /// </summary>
    [HttpGet("{userId:int}/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReviews(int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var reviews = await _reviewService.GetNurseReviewsAsync(userId, page, pageSize);
        return Ok(reviews);
    }

    /// <summary>
    /// Get aggregated rating for a nurse (public).
    /// </summary>
    [HttpGet("{userId:int}/rating")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRating(int userId)
    {
        var rating = await _reviewService.GetNurseRatingAsync(userId);
        return Ok(rating);
    }
}
