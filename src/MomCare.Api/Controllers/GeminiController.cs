using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[Authorize]
[ApiController]
[Route("api/gemini")]
public class GeminiController : ControllerBase
{
    private readonly IGeminiService _geminiService;
    private readonly IConfiguration _configuration;

    public GeminiController(IGeminiService geminiService, IConfiguration configuration)
    {
        _geminiService = geminiService;
        _configuration = configuration;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GeminiGenerateRequest request, CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue<bool>("Features:EnableGeminiTestEndpoint"))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await _geminiService.GenerateAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Gemini request failed." });
        }
    }
}
