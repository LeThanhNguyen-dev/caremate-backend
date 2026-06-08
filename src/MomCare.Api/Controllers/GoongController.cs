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

    [HttpGet("reverse-geocode")]
    public async Task<IActionResult> ReverseGeocode(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        CancellationToken cancellationToken)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
        {
            return BadRequest(new { message = "latitude and longitude are required." });
        }

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "GOONG_API_KEY is not configured." });
        }

        var url = BuildUrl("/Geocode", new Dictionary<string, string>
        {
            ["api_key"] = apiKey,
            ["latlng"] = $"{latitude},{longitude}"
        });

        return await ForwardGoongRequestAsync(url, cancellationToken);
    }

    private string? GetApiKey() =>
        _configuration["Goong:RestApiKey"]
        ?? _configuration["GOONG_REST_API_KEY"]
        ?? Environment.GetEnvironmentVariable("GOONG_REST_API_KEY")
        ?? _configuration["Goong:ApiKey"]
        ?? _configuration["GOONG_API_KEY"]
        ?? Environment.GetEnvironmentVariable("GOONG_API_KEY")
        ?? _configuration["VITE_GOONG_API_KEY"]
        ?? Environment.GetEnvironmentVariable("VITE_GOONG_API_KEY");

    private async Task<IActionResult> ForwardGoongRequestAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();

        try
        {
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499, new { message = "Goong request was cancelled by the client." });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                message = "Goong request timed out."
            });
        }
    }

    private static string BuildUrl(string path, IReadOnlyDictionary<string, string> parameters)
    {
        var query = string.Join("&", parameters.Select(item =>
            $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
        return $"{GoongBaseUrl}{path}?{query}";
    }
}
