using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[Authorize]
[ApiController]
[Route("api/groq")]
public class GroqController : ControllerBase
{
    private readonly ILlmService _llmService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GroqController> _logger;

    public GroqController(ILlmService llmService, IConfiguration configuration, ILogger<GroqController> logger)
    {
        _llmService = llmService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GeminiGenerateRequest request, CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue<bool>("Features:EnableGroqTestEndpoint"))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await _llmService.GenerateAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Groq request failed.");
            var message = HttpContext.RequestServices
                .GetRequiredService<IWebHostEnvironment>()
                .IsDevelopment()
                ? ex.Message
                : "Groq request failed.";

            return StatusCode(StatusCodes.Status502BadGateway, new { message });
        }
    }
}
