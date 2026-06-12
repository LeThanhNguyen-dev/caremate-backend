using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class HealthCheckInService : IHealthCheckInService
{
    private const string Disclaimer = "Thông tin từ CareMate Engine chỉ mang tính tham khảo, không thay thế tư vấn từ bác sĩ hoặc chuyên gia y tế.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly MomCareContext _context;
    private readonly IGeminiService _geminiService;
    private readonly ILogger<HealthCheckInService> _logger;

    public HealthCheckInService(
        MomCareContext context,
        IGeminiService geminiService,
        ILogger<HealthCheckInService> logger)
    {
        _context = context;
        _geminiService = geminiService;
        _logger = logger;
    }
    public async Task<HealthAnalysisResponse> AnalyzeAsync(int userId, AnalyzeHealthCheckInRequest request, CancellationToken cancellationToken)
    {
        var availableServices = await GetAvailableServicesAsync(cancellationToken);
        var checkIn = new HealthCheckIn
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SleepHours = request.SleepHours,
            PainLevel = request.PainLevel,
            PainLocation = Clean(request.PainLocation),
            PainType = Clean(request.PainType),
            PainDuration = Clean(request.PainDuration),
            PainTrend = Clean(request.PainTrend),
            SymptomsJson = JsonSerializer.Serialize(CleanList(request.Symptoms), JsonOptions),
            MedicalHistoryJson = JsonSerializer.Serialize(CleanList(request.MedicalHistory), JsonOptions),
            ContextDataJson = JsonSerializer.Serialize(CleanContextData(request.ContextData), JsonOptions),
            MotherAge = request.MotherAge,
            SystolicBloodPressure = request.SystolicBloodPressure,
            DiastolicBloodPressure = request.DiastolicBloodPressure,
            TemperatureCelsius = request.TemperatureCelsius,
            TookMedicationToday = request.TookMedicationToday,
            MedicationNote = Clean(request.MedicationNote),
            Mood = request.Mood.Trim(),
            MilkStatus = request.MilkStatus.Trim(),
            BabyFeeding = request.BabyFeeding.Trim(),
            BabySleep = request.BabySleep.Trim(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.HealthCheckIns.Add(checkIn);
        await _context.SaveChangesAsync(cancellationToken);

        var recentHistory = await _context.HealthCheckIns
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(7)
            .ToListAsync(cancellationToken);

        var analysisResult = RiskAssessmentEngine.Analyze(checkIn, recentHistory, availableServices);
        var rawGeminiResponse = await TryEnhanceWithGeminiAsync(checkIn, analysisResult, cancellationToken);

        var analysis = new AiHealthAnalysis
        {
            Id = Guid.NewGuid(),
            HealthCheckInId = checkIn.Id,
            Summary = analysisResult.Summary,
            WarningLevel = analysisResult.WarningLevel,
            TriageColor = analysisResult.WarningLevel,
            UrgencyAction = analysisResult.UrgencyAction,
            WeeklySummary = analysisResult.WeeklySummary,
            RiskScore = analysisResult.RiskScore,
            ConfidenceScore = analysisResult.ConfidenceScore,
            TrendSummary = analysisResult.TrendSummary,
            RiskFactorsJson = JsonSerializer.Serialize(analysisResult.RiskFactors, JsonOptions),
            TrendSignalsJson = JsonSerializer.Serialize(analysisResult.TrendSignals, JsonOptions),
            RecommendationsJson = JsonSerializer.Serialize(analysisResult.Recommendations, JsonOptions),
            CarePlanJson = JsonSerializer.Serialize(analysisResult.CarePlan, JsonOptions),
            SuggestedServicesJson = JsonSerializer.Serialize(analysisResult.SuggestedServices, JsonOptions),
            RawAiResponse = rawGeminiResponse,
            PpdScreeningScore = analysisResult.PpdScreeningScore,
            PpdScreeningLevel = analysisResult.PpdScreeningLevel,
            PpdScreeningNote = analysisResult.PpdScreeningNote,
            NarrativeSummary = analysisResult.NarrativeSummary,
            NutritionGuidanceJson = JsonSerializer.Serialize(analysisResult.NutritionGuidance, JsonOptions),
            DataCoveragePercent = analysisResult.DataCoveragePercent,
            DataCoverageItemsJson = JsonSerializer.Serialize(analysisResult.DataCoverageItems, JsonOptions),
            MissingDataItemsJson = JsonSerializer.Serialize(analysisResult.MissingDataItems, JsonOptions),
            CreatedAt = DateTime.UtcNow
        };

        _context.AiHealthAnalyses.Add(analysis);
        await _context.SaveChangesAsync(cancellationToken);

        return MapAnalysisResponse(checkIn.Id, analysis);
    }

    public async Task<LatestHealthCheckInDto?> GetLatestAsync(int userId, CancellationToken cancellationToken)
    {
        var checkIn = await _context.HealthCheckIns
            .AsNoTracking()
            .Include(x => x.Analysis)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (checkIn is null)
        {
            return null;
        }

        var latest = MapLatest(checkIn);
        latest.Analysis = await AnalyzeExistingCheckInAsync(userId, checkIn, cancellationToken);
        return latest;
    }

    public async Task<IReadOnlyList<HealthCheckInHistoryDto>> GetHistoryAsync(int userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var skip = (page - 1) * pageSize;
        var items = await _context.HealthCheckIns
            .AsNoTracking()
            .Include(x => x.Analysis)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return items.Select(MapHistory).ToList();
    }

    private async Task<IReadOnlyList<SuggestedServiceDto>> GetAvailableServicesAsync(CancellationToken cancellationToken)
    {
        return await _context.Services
            .AsNoTracking()
            .Where(x => x.Status == "active")
            .OrderBy(x => x.ServiceKind)
            .ThenBy(x => x.Name)
            .Select(x => new SuggestedServiceDto
            {
                ServiceKey = x.Id.ToString(),
                ServiceName = x.Name,
                Reason = string.Empty
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<HealthAnalysisResponse> AnalyzeExistingCheckInAsync(
        int userId,
        HealthCheckIn checkIn,
        CancellationToken cancellationToken)
    {
        var availableServices = await GetAvailableServicesAsync(cancellationToken);
        var recentHistory = await _context.HealthCheckIns
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.CreatedAt <= checkIn.CreatedAt)
            .OrderByDescending(x => x.CreatedAt)
            .Take(7)
            .ToListAsync(cancellationToken);

        if (recentHistory.All(x => x.Id != checkIn.Id))
        {
            recentHistory.Insert(0, checkIn);
        }

        var result = RiskAssessmentEngine.Analyze(checkIn, recentHistory, availableServices);
        return MapAnalysisResponse(checkIn.Id, result, checkIn.Analysis?.Id ?? Guid.Empty);
    }

    private async Task<string?> TryEnhanceWithGeminiAsync(
        HealthCheckIn checkIn,
        HealthAnalysisResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _geminiService.GenerateAsync(new GeminiGenerateRequest
            {
                SystemInstruction = """
Bạn là trợ lý CareMate cho mẹ sau sinh. Giữ nguyên mức cảnh báo và điểm rủi ro từ rule engine.
Chỉ viết lại kết quả cho dễ hiểu, ngắn gọn, bám sát ghi chú của mẹ. Không chẩn đoán, không kê đơn thuốc, không bịa dữ liệu.
Chỉ trả JSON hợp lệ, không markdown.
""",
                Prompt = BuildGeminiPrompt(checkIn, result),
                Temperature = 0.2,
                MaxOutputTokens = 450
            }, cancellationToken);

            var enhanced = ParseGeminiResult(response.Text);
            if (enhanced is null)
            {
                return response.RawResponse;
            }

            result.Summary = Limit(enhanced.Summary, 320) ?? result.Summary;
            result.UrgencyAction = Limit(enhanced.UrgencyAction, 180) ?? result.UrgencyAction;
            result.NarrativeSummary = Limit(enhanced.NarrativeSummary, 420) ?? result.NarrativeSummary;

            var recommendations = enhanced.Recommendations
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => Limit(x, 160))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(3)
                .Cast<string>()
                .ToList();

            if (recommendations.Count > 0)
            {
                result.Recommendations = recommendations;
            }

            return response.RawResponse;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Gemini enhancement failed. Falling back to rule-based health analysis.");
            return null;
        }
    }

    private static string BuildGeminiPrompt(HealthCheckIn checkIn, HealthAnalysisResult result)
    {
        var input = new
        {
            motherNote = checkIn.Note,
            checkIn.PainLevel,
            checkIn.MilkStatus,
            checkIn.BabyFeeding,
            symptoms = DeserializeStringList(checkIn.SymptomsJson),
            contextData = DeserializeStringDictionary(checkIn.ContextDataJson),
            fixedResult = new
            {
                result.WarningLevel,
                result.RiskScore,
                result.RiskFactors,
                result.SuggestedServices
            },
            draft = new
            {
                result.Summary,
                result.UrgencyAction,
                result.Recommendations,
                result.NarrativeSummary
            }
        };

        return $$"""
Viết lại kết quả check-in cho mẹ sau sinh theo JSON schema:
{
  "summary": "1-2 câu, tối đa 320 ký tự",
  "urgencyAction": "1 câu hành động ưu tiên, tối đa 180 ký tự",
  "recommendations": ["tối đa 3 ý ngắn, mỗi ý tối đa 160 ký tự"],
  "narrativeSummary": "2 câu, tối đa 420 ký tự"
}

Quy tắc:
- Giữ nguyên warningLevel và riskScore nếu nhắc tới.
- Nếu mẹ có ghi chú, bám sát ghi chú đó.
- Nếu Red hoặc Emergency, ưu tiên đi khám/liên hệ y tế.
- Không lặp disclaimer.

Dữ liệu:
{{JsonSerializer.Serialize(input, JsonOptions)}}
""";
    }

    private static EnhancedHealthAnalysis? ParseGeminiResult(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return JsonSerializer.Deserialize<EnhancedHealthAnalysis>(text[start..(end + 1)], JsonOptions);
    }

    private static string? Limit(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].TrimEnd() + "...";
    }

    private sealed class EnhancedHealthAnalysis
    {
        public string? Summary { get; set; }
        public string? UrgencyAction { get; set; }
        public List<string> Recommendations { get; set; } = [];
        public string? NarrativeSummary { get; set; }
    }

    private HealthAnalysisResponse MapAnalysisResponse(Guid checkInId, AiHealthAnalysis analysis)
    {
        return new HealthAnalysisResponse
        {
            CheckInId = checkInId,
            AnalysisId = analysis.Id,
            Summary = analysis.Summary,
            WarningLevel = analysis.WarningLevel,
            UrgencyAction = analysis.UrgencyAction,
            WeeklySummary = analysis.WeeklySummary,
            RiskScore = analysis.RiskScore,
            ConfidenceScore = analysis.ConfidenceScore,
            TrendSummary = analysis.TrendSummary,
            RiskFactors = DeserializeRiskFactors(analysis.RiskFactorsJson),
            TrendSignals = DeserializeTrendSignals(analysis.TrendSignalsJson),
            Recommendations = DeserializeRecommendations(analysis.RecommendationsJson),
            CarePlan = DeserializeCarePlan(analysis.CarePlanJson),
            SuggestedServices = DeserializeSuggestedServices(analysis.SuggestedServicesJson),
            PpdScreeningScore = analysis.PpdScreeningScore,
            PpdScreeningLevel = analysis.PpdScreeningLevel,
            PpdScreeningNote = analysis.PpdScreeningNote,
            NutritionGuidance = DeserializeNutritionGuidance(analysis.NutritionGuidanceJson),
            NarrativeSummary = analysis.NarrativeSummary,
            DataCoveragePercent = analysis.DataCoveragePercent,
            DataCoverageItems = DeserializeStringList(analysis.DataCoverageItemsJson),
            MissingDataItems = DeserializeStringList(analysis.MissingDataItemsJson),
            Disclaimer = Disclaimer,
            ConfidenceLabel = BuildConfidenceLabel(analysis.ConfidenceScore),
            EngineVersion = RiskAssessmentEngine.EngineVersion
        };
    }

    private static HealthAnalysisResponse MapAnalysisResponse(
        Guid checkInId,
        HealthAnalysisResult result,
        Guid analysisId)
    {
        return new HealthAnalysisResponse
        {
            CheckInId = checkInId,
            AnalysisId = analysisId,
            Summary = result.Summary,
            WarningLevel = result.WarningLevel,
            UrgencyAction = result.UrgencyAction,
            WeeklySummary = result.WeeklySummary,
            RiskScore = result.RiskScore,
            ConfidenceScore = result.ConfidenceScore,
            TrendSummary = result.TrendSummary,
            RiskFactors = result.RiskFactors,
            TrendSignals = result.TrendSignals,
            Recommendations = result.Recommendations,
            CarePlan = result.CarePlan,
            SuggestedServices = result.SuggestedServices,
            PpdScreeningScore = result.PpdScreeningScore,
            PpdScreeningLevel = result.PpdScreeningLevel,
            PpdScreeningNote = result.PpdScreeningNote,
            NutritionGuidance = result.NutritionGuidance,
            NarrativeSummary = result.NarrativeSummary,
            DataCoveragePercent = result.DataCoveragePercent,
            DataCoverageItems = result.DataCoverageItems,
            MissingDataItems = result.MissingDataItems,
            Disclaimer = Disclaimer,
            ConfidenceLabel = BuildConfidenceLabel(result.ConfidenceScore),
            EngineVersion = RiskAssessmentEngine.EngineVersion
        };
    }

    private HealthCheckInHistoryDto MapHistory(HealthCheckIn checkIn)
    {
        return new HealthCheckInHistoryDto
        {
            CheckInId = checkIn.Id,
            CreatedAt = checkIn.CreatedAt,
            SleepHours = checkIn.SleepHours,
            PainLevel = checkIn.PainLevel,
            PainLocation = checkIn.PainLocation,
            PainType = checkIn.PainType,
            PainDuration = checkIn.PainDuration,
            PainTrend = checkIn.PainTrend,
            Symptoms = DeserializeStringList(checkIn.SymptomsJson),
            MedicalHistory = DeserializeStringList(checkIn.MedicalHistoryJson),
            ContextData = DeserializeStringDictionary(checkIn.ContextDataJson),
            MotherAge = checkIn.MotherAge,
            SystolicBloodPressure = checkIn.SystolicBloodPressure,
            DiastolicBloodPressure = checkIn.DiastolicBloodPressure,
            TemperatureCelsius = checkIn.TemperatureCelsius,
            TookMedicationToday = checkIn.TookMedicationToday,
            MedicationNote = checkIn.MedicationNote,
            Mood = checkIn.Mood,
            MilkStatus = checkIn.MilkStatus,
            BabyFeeding = checkIn.BabyFeeding,
            BabySleep = checkIn.BabySleep,
            Note = checkIn.Note,
            Analysis = checkIn.Analysis is null ? null : MapAnalysisResponse(checkIn.Id, checkIn.Analysis)
        };
    }

    private LatestHealthCheckInDto MapLatest(HealthCheckIn checkIn)
    {
        return new LatestHealthCheckInDto
        {
            CheckInId = checkIn.Id,
            CreatedAt = checkIn.CreatedAt,
            SleepHours = checkIn.SleepHours,
            PainLevel = checkIn.PainLevel,
            PainLocation = checkIn.PainLocation,
            PainType = checkIn.PainType,
            PainDuration = checkIn.PainDuration,
            PainTrend = checkIn.PainTrend,
            Symptoms = DeserializeStringList(checkIn.SymptomsJson),
            MedicalHistory = DeserializeStringList(checkIn.MedicalHistoryJson),
            ContextData = DeserializeStringDictionary(checkIn.ContextDataJson),
            MotherAge = checkIn.MotherAge,
            SystolicBloodPressure = checkIn.SystolicBloodPressure,
            DiastolicBloodPressure = checkIn.DiastolicBloodPressure,
            TemperatureCelsius = checkIn.TemperatureCelsius,
            TookMedicationToday = checkIn.TookMedicationToday,
            MedicationNote = checkIn.MedicationNote,
            Mood = checkIn.Mood,
            MilkStatus = checkIn.MilkStatus,
            BabyFeeding = checkIn.BabyFeeding,
            BabySleep = checkIn.BabySleep,
            Note = checkIn.Note,
            Analysis = checkIn.Analysis is null ? null : MapAnalysisResponse(checkIn.Id, checkIn.Analysis)
        };
    }

    private static List<string> DeserializeRecommendations(string json) => JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];

    private static List<CarePlanItemDto> DeserializeCarePlan(string json) => JsonSerializer.Deserialize<List<CarePlanItemDto>>(json, JsonOptions) ?? [];

    private static List<SuggestedServiceDto> DeserializeSuggestedServices(string json) => JsonSerializer.Deserialize<List<SuggestedServiceDto>>(json, JsonOptions) ?? [];

    private static List<RiskFactorDto> DeserializeRiskFactors(string json) => JsonSerializer.Deserialize<List<RiskFactorDto>>(json, JsonOptions) ?? [];

    private static List<TrendSignalDto> DeserializeTrendSignals(string json) => JsonSerializer.Deserialize<List<TrendSignalDto>>(json, JsonOptions) ?? [];

    private static List<NutritionTipDto> DeserializeNutritionGuidance(string json) => JsonSerializer.Deserialize<List<NutritionTipDto>>(json, JsonOptions) ?? [];

    private static List<string> DeserializeStringList(string json) => JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];

    private static Dictionary<string, string> DeserializeStringDictionary(string json) => JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? [];

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static List<string> CleanList(IEnumerable<string>? values)
    {
        return values?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList() ?? [];
    }

    private static Dictionary<string, string> CleanContextData(Dictionary<string, string>? values)
    {
        return values?
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => new KeyValuePair<string, string>(x.Key.Trim(), x.Value.Trim()))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase) ?? [];
    }

    private static string BuildConfidenceLabel(int confidenceScore)
    {
        return confidenceScore switch
        {
            >= 70 => "Cao",
            >= 40 => "Trung bình",
            _ => "Thấp - nhập thêm chỉ số để phân tích chính xác hơn"
        };
    }
}
