using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Infrastructure.Configurations;
using MomCare.Interfaces;

namespace MomCare.Services;

public class FptAiCccdOcrService : ICccdOcrService
{
    private const long MaxFileBytes = 5 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly FptAiOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FptAiCccdOcrService> _logger;

    public FptAiCccdOcrService(
        HttpClient httpClient,
        IOptions<FptAiOptions> options,
        IConfiguration configuration,
        ILogger<FptAiCccdOcrService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CccdOcrResultDto> ExtractAsync(string documentType, IFormFile file, CancellationToken cancellationToken)
    {
        var normalizedType = NormalizeDocumentType(documentType);
        if (!DocumentTypes.IsIdCard(normalizedType))
        {
            throw new ArgumentException("OCR is only supported for CCCD front/back images.");
        }

        ValidateFile(file);

        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        using var imageContent = new StreamContent(stream);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(imageContent, "image", file.FileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, GetEndpoint())
        {
            Content = content
        };
        request.Headers.Add("api-key", GetApiKey());

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("FPT AI CCCD OCR failed with status code {StatusCode}. Response: {Response}", response.StatusCode, rawResponse);
            throw new HttpRequestException($"FPT AI OCR request failed with status {(int)response.StatusCode}.", null, response.StatusCode);
        }

        var payload = JsonSerializer.Deserialize<FptAiIdCardResponse>(rawResponse, JsonOptions)
            ?? throw new InvalidOperationException("FPT AI OCR returned an empty response.");

        var expectedSide = normalizedType == DocumentTypes.IdCardFront ? "front" : "back";
        if (payload.ErrorCode != 0)
        {
            return new CccdOcrResultDto
            {
                IsIdentityCard = false,
                Side = expectedSide,
                ConfidenceScore = 0,
                Warning = string.IsNullOrWhiteSpace(payload.ErrorMessage)
                    ? "FPT AI could not recognize a Vietnamese ID card in this image."
                    : payload.ErrorMessage,
                RawText = rawResponse
            };
        }

        var card = payload.Data.FirstOrDefault();
        if (card == null)
        {
            return new CccdOcrResultDto
            {
                IsIdentityCard = false,
                Side = expectedSide,
                ConfidenceScore = 0,
                Warning = "FPT AI did not return any ID card data.",
                RawText = rawResponse
            };
        }

        var detectedSide = DetectSide(card.Type);
        var warning = detectedSide != "unknown" && detectedSide != expectedSide
            ? $"Ảnh có vẻ là mặt {(detectedSide == "front" ? "trước" : "sau")} CCCD, nhưng bạn đang chọn loại mặt {(expectedSide == "front" ? "trước" : "sau")}."
            : null;

        return new CccdOcrResultDto
        {
            IsIdentityCard = true,
            Side = detectedSide == "unknown" ? expectedSide : detectedSide,
            IdNumber = NormalizeValue(card.Id),
            FullName = NormalizeValue(card.Name),
            DateOfBirth = NormalizeValue(card.Dob),
            Gender = NormalizeValue(card.Sex),
            Nationality = NormalizeValue(card.Nationality ?? card.Ethnicity),
            PlaceOfOrigin = NormalizeValue(card.Home),
            PlaceOfResidence = NormalizeValue(card.Address),
            DateOfIssue = NormalizeValue(card.IssueDate),
            DateOfExpiry = NormalizeValue(card.Doe),
            IssuingAuthority = NormalizeValue(card.IssueLoc),
            ConfidenceScore = CalculateConfidenceScore(card),
            Warning = warning,
            RawText = BuildRawText(card)
        };
    }

    private string GetApiKey()
    {
        var apiKey = new[]
            {
                _options.ApiKey,
                _configuration["FPT_AI_API_KEY"],
                _configuration["FPTAI_API_KEY"],
                Environment.GetEnvironmentVariable("FPT_AI_API_KEY"),
                Environment.GetEnvironmentVariable("FPTAI_API_KEY")
            }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("FPT_AI_API_KEY is not configured.");
        }

        return apiKey.Trim();
    }

    private Uri GetEndpoint()
    {
        var endpoint = string.IsNullOrWhiteSpace(_options.IdCardEndpoint)
            ? "https://api.fpt.ai/vision/idr/vnm"
            : _options.IdCardEndpoint.Trim();

        return new Uri(endpoint);
    }

    private static int CalculateConfidenceScore(FptAiIdCardData card)
    {
        var probabilities = new[]
            {
                card.IdProb,
                card.NameProb,
                card.DobProb,
                card.SexProb,
                card.NationalityProb,
                card.HomeProb,
                card.AddressProb,
                card.DoeProb,
                card.IssueDateProb,
                card.IssueLocProb
            }
            .Select(ParseProbability)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        if (probabilities.Count == 0)
        {
            return 0;
        }

        var average = probabilities.Average();
        return (int)Math.Round(Math.Clamp(average <= 1 ? average * 100 : average, 0, 100));
    }

    private static double? ParseProbability(string? value)
    {
        var normalized = NormalizeValue(value)?.TrimEnd('%');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string BuildRawText(FptAiIdCardData card)
    {
        var values = new[]
        {
            card.Id,
            card.Name,
            card.Dob,
            card.Sex,
            card.Nationality,
            card.Ethnicity,
            card.Home,
            card.Address,
            card.Doe,
            card.IssueDate,
            card.IssueLoc,
            card.Features
        };

        return string.Join("\n", values.Select(NormalizeValue).Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string DetectSide(string? type)
    {
        var normalized = NormalizeValue(type)?.ToLowerInvariant();
        return normalized switch
        {
            "old" or "new" => "front",
            "old_back" or "new_back" => "back",
            _ => normalized?.Contains("back", StringComparison.OrdinalIgnoreCase) == true ? "back" : "unknown"
        };
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || value.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase)
            ? null
            : value.Trim();
    }

    private static void ValidateFile(IFormFile file)
    {
        if (file.Length <= 0)
        {
            throw new ArgumentException("Uploaded file is empty.");
        }

        if (file.Length > MaxFileBytes)
        {
            throw new ArgumentException("CCCD image must be 5MB or smaller.");
        }

        var contentType = file.ContentType.Trim().ToLowerInvariant();
        if (contentType is not ("image/jpeg" or "image/png"))
        {
            throw new ArgumentException("Only JPG and PNG CCCD images are supported.");
        }
    }

    private static string NormalizeDocumentType(string type)
    {
        var normalized = type.Trim().ToLowerInvariant();
        return normalized switch
        {
            "id_card" => DocumentTypes.IdCardFront,
            _ => normalized
        };
    }

    private sealed class FptAiIdCardResponse
    {
        public int ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public List<FptAiIdCardData> Data { get; set; } = [];
    }

    private sealed class FptAiIdCardData
    {
        public string? Id { get; set; }
        [JsonPropertyName("id_prob")]
        public string? IdProb { get; set; }
        public string? Name { get; set; }
        [JsonPropertyName("name_prob")]
        public string? NameProb { get; set; }
        public string? Dob { get; set; }
        [JsonPropertyName("dob_prob")]
        public string? DobProb { get; set; }
        public string? Sex { get; set; }
        [JsonPropertyName("sex_prob")]
        public string? SexProb { get; set; }
        public string? Nationality { get; set; }
        [JsonPropertyName("nationality_prob")]
        public string? NationalityProb { get; set; }
        public string? Ethnicity { get; set; }
        [JsonPropertyName("ethnicity_prob")]
        public string? EthnicityProb { get; set; }
        public string? Home { get; set; }
        [JsonPropertyName("home_prob")]
        public string? HomeProb { get; set; }
        public string? Address { get; set; }
        [JsonPropertyName("address_prob")]
        public string? AddressProb { get; set; }
        public string? Doe { get; set; }
        [JsonPropertyName("doe_prob")]
        public string? DoeProb { get; set; }
        [JsonPropertyName("issue_date")]
        public string? IssueDate { get; set; }
        [JsonPropertyName("issue_date_prob")]
        public string? IssueDateProb { get; set; }
        [JsonPropertyName("issue_loc")]
        public string? IssueLoc { get; set; }
        [JsonPropertyName("issue_loc_prob")]
        public string? IssueLocProb { get; set; }
        public string? Features { get; set; }
        [JsonPropertyName("features_prob")]
        public string? FeaturesProb { get; set; }
        public string? Type { get; set; }
    }
}
