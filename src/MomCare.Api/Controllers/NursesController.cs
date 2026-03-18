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

    public NursesController(INurseDiscoveryService nurseDiscoveryService, IAvailabilityService availabilityService)
    {
        _nurseDiscoveryService = nurseDiscoveryService;
        _availabilityService = availabilityService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search(
        [FromQuery] int? serviceId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime)
    {
        var result = await _nurseDiscoveryService.SearchAsync(serviceId, minPrice, maxPrice, startTime, endTime);
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

    [HttpGet("{userId:int}/availability")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailability(int userId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var slots = await _availabilityService.GetNurseSlotsAsync(userId, from, to);
        return Ok(slots);
    }
}
