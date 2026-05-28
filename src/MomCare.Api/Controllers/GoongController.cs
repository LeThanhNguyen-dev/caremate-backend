using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MomCare.Controllers;

[ApiController]
[Route("api/goong")]
[AllowAnonymous]
public class GoongController : ControllerBase
{
    private const string GoongBaseUrl = "https://rsapi.goong.io";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public GoongController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet("autocomplete")]
    public async Task<IActionResult> Autocomplete(
        [FromQuery] string input,
        [FromQuery] string? sessionToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Trim().Length < 3)
        {
            return Ok(new { predictions = Array.Empty<object>(), status = "EMPTY_INPUT" });
        }

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "GOONG_API_KEY is not configured." });
        }

        var url = BuildUrl("/Place/AutoComplete", new Dictionary<string, string>
        {
            ["api_key"] = apiKey,
            ["input"] = input.Trim(),
            ["limit"] = "6",
            ["more_compound"] = "true",
            ["sessiontoken"] = string.IsNullOrWhiteSpace(sessionToken) ? Guid.NewGuid().ToString() : sessionToken
        });

        return await ForwardGoongRequestAsync(url, cancellationToken);
    }

    [HttpGet("place-detail")]
    public async Task<IActionResult> PlaceDetail(
        [FromQuery] string placeId,
        [FromQuery] string? sessionToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(placeId))
        {
            return BadRequest(new { message = "placeId is required." });
        }

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "GOONG_API_KEY is not configured." });
        }

        var url = BuildUrl("/Place/Detail", new Dictionary<string, string>
        {
            ["api_key"] = apiKey,
            ["place_id"] = placeId.Trim(),
            ["sessiontoken"] = string.IsNullOrWhiteSpace(sessionToken) ? Guid.NewGuid().ToString() : sessionToken
        });

        return await ForwardGoongRequestAsync(url, cancellationToken);
    }

    private string? GetApiKey() =>
        _configuration["Goong:RestApiKey"]
        ?? _configuration["GOONG_REST_API_KEY"]
        ?? Environment.GetEnvironmentVariable("GOONG_REST_API_KEY")
        ?? _configuration["Goong:ApiKey"]
        ?? _configuration["GOONG_API_KEY"]
        ?? Environment.GetEnvironmentVariable("GOONG_API_KEY");

    private async Task<IActionResult> ForwardGoongRequestAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(url, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, new
            {
                message = "Goong request failed.",
                statusCode = (int)response.StatusCode,
                detail = content
            });
        }

        using var document = JsonDocument.Parse(content);
        return Ok(document.RootElement.Clone());
    }

    private static string BuildUrl(string path, IReadOnlyDictionary<string, string> parameters)
    {
        var query = string.Join("&", parameters.Select(item =>
            $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
        return $"{GoongBaseUrl}{path}?{query}";
    }
}
