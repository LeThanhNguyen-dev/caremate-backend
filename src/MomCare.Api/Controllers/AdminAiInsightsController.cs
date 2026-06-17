using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[ApiController]
[Route("api/admin/ai-insights")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminAiInsightsController : ControllerBase
{
    private readonly IAdminAiInsightService _adminAiInsightService;

    public AdminAiInsightsController(IAdminAiInsightService adminAiInsightService)
    {
        _adminAiInsightService = adminAiInsightService;
    }

    [HttpPost("generate")]
    [EnableRateLimiting("chat")]
    public async Task<IActionResult> Generate([FromBody] AdminAiInsightRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminAiInsightService.GenerateAsync(request, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Error });
    }
}
