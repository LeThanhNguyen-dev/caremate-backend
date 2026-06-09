using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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

    public HealthCheckInService(MomCareContext context)
    {
        _context = context;
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

        return checkIn is null ? null : MapLatest(checkIn);
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
