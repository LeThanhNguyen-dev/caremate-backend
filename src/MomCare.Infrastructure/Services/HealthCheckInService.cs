using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
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
    private static readonly (string Location, string[] Keywords)[] PainLocationKeywords =
    [
        ("bụng dưới", ["bung duoi", "ha vi", "vung duoi bung"]),
        ("bụng trên", ["bung tren", "thuong vi", "vung tren bung"]),
        ("vết mổ/khâu", ["vet mo", "vet khau", "duong khau", "mui khau"]),
        ("tầng sinh môn", ["tang sinh mon", "cua minh", "am dao", "vung kin", "vet rach"]),
        ("ngực/sữa", ["nguc", "vu", "bau vu", "num vu", "tac sua", "cang sua"]),
        ("đầu", ["dau dau", "nhuc dau", "dau nua dau"]),
        ("lưng", ["that lung", "dau lung", "lung"]),
        ("xương chậu/hông", ["xuong chau", "hong", "khung chau"]),
        ("bắp chân", ["bap chan", "cang chan"]),
        ("chân", ["chan", "dau chan"]),
        ("tay", ["tay", "co tay", "canh tay"]),
        ("vai/cổ", ["vai", "co", "gayi", "gay"])
    ];

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
        var inferred = await TryInferContextWithGeminiAsync(request.Note, cancellationToken)
            ?? InferContextFromNote(request.Note);
        var checkIn = BuildCheckIn(userId, request, inferred);

        _context.HealthCheckIns.Add(checkIn);
        await _context.SaveChangesAsync(cancellationToken);

        var recentHistory = await _context.HealthCheckIns
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(7)
            .ToListAsync(cancellationToken);

        var analysisResult = RiskAssessmentEngine.Analyze(checkIn, recentHistory, availableServices);
        var aiTelemetry = await TryEnhanceWithGeminiAsync(checkIn, analysisResult, cancellationToken);
        ApplyStrictMedicalGuardrails(analysisResult);

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
            RawAiResponse = null,
            AiModel = aiTelemetry.Model,
            AiLatencyMs = aiTelemetry.LatencyMs,
            AiFallbackMode = aiTelemetry.FallbackMode,
            EngineVersion = RiskAssessmentEngine.EngineVersion,
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

    public async Task<HealthCheckInFollowUpPreviewResponse> PreviewFollowUpAsync(int userId, AnalyzeHealthCheckInRequest request, CancellationToken cancellationToken)
    {
        var availableServices = await GetAvailableServicesAsync(cancellationToken);
        var checkIn = BuildCheckIn(userId, request, InferContextFromNote(request.Note));

        var recentHistory = await _context.HealthCheckIns
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(6)
            .ToListAsync(cancellationToken);

        recentHistory.Insert(0, checkIn);
        var result = RiskAssessmentEngine.Analyze(checkIn, recentHistory, availableServices);
        ApplyStrictMedicalGuardrails(result);

        return new HealthCheckInFollowUpPreviewResponse
        {
            DataCoveragePercent = result.DataCoveragePercent,
            DataCoverageItems = result.DataCoverageItems,
            MissingDataItems = result.MissingDataItems,
            FollowUpQuestions = result.FollowUpQuestions,
            EngineVersion = RiskAssessmentEngine.EngineVersion,
            EstimatedRiskPreview = new HealthCheckInRiskPreviewDto
            {
                WarningLevel = result.WarningLevel,
                RiskScore = result.RiskScore,
                ConfidenceScore = result.ConfidenceScore,
                Summary = result.Summary,
                UrgencyAction = result.UrgencyAction,
                RiskFactors = result.RiskFactors
                    .OrderByDescending(x => x.Points)
                    .Take(5)
                    .ToList()
            }
        };
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

    private static HealthCheckIn BuildCheckIn(int userId, AnalyzeHealthCheckInRequest request, InferredCheckInContext inferred)
    {
        var contextData = CleanContextData(request.ContextData);
        var symptoms = CleanList(request.Symptoms)
            .Concat(inferred.Symptoms)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        return new HealthCheckIn
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SleepHours = request.SleepHours,
            PainLevel = request.PainLevel ?? inferred.PainLevel ?? 0,
            PainLocation = Clean(request.PainLocation) ?? inferred.PainLocation,
            PainType = Clean(request.PainType) ?? inferred.PainType,
            PainDuration = Clean(request.PainDuration),
            PainTrend = Clean(request.PainTrend) ?? inferred.PainTrend,
            SymptomsJson = JsonSerializer.Serialize(symptoms, JsonOptions),
            MedicalHistoryJson = JsonSerializer.Serialize(CleanList(request.MedicalHistory), JsonOptions),
            ContextDataJson = JsonSerializer.Serialize(contextData, JsonOptions),
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
    }

    private static void ApplyStrictMedicalGuardrails(HealthAnalysisResult result)
    {
        if (result.WarningLevel.Equals("Emergency", StringComparison.OrdinalIgnoreCase))
        {
            result.UrgencyAction = "Gọi cấp cứu hoặc đến cơ sở y tế gần nhất ngay. Không chờ theo dõi tại nhà khi có dấu hiệu khẩn cấp.";
            PrependRecommendation(result, "Ưu tiên an toàn: nhờ người thân hỗ trợ di chuyển và mang theo thông tin check-in này khi gặp nhân viên y tế.");
            return;
        }

        if (result.WarningLevel.Equals("Red", StringComparison.OrdinalIgnoreCase))
        {
            result.UrgencyAction = "Liên hệ bác sĩ hoặc cơ sở y tế trong ngày để được đánh giá trực tiếp, đặc biệt nếu triệu chứng tăng lên.";
            PrependRecommendation(result, "Không tự dùng thuốc mới hoặc trì hoãn thăm khám nếu có sốt, ra máu nhiều, khó thở, đau ngực hoặc bé bú kém rõ.");
        }
    }

    private static void PrependRecommendation(HealthAnalysisResult result, string recommendation)
    {
        result.Recommendations = result.Recommendations
            .Prepend(recommendation)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
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
        ApplyStrictMedicalGuardrails(result);
        return MapAnalysisResponse(checkIn.Id, result, checkIn.Analysis?.Id ?? Guid.Empty);
    }

    private async Task<AiEnhancementTelemetry> TryEnhanceWithGeminiAsync(
        HealthCheckIn checkIn,
        HealthAnalysisResult result,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
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
                return new AiEnhancementTelemetry
                {
                    Model = response.Model,
                    LatencyMs = stopwatch.ElapsedMilliseconds,
                    FallbackMode = "parse_failed"
                };
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

            return new AiEnhancementTelemetry
            {
                Model = response.Model,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                FallbackMode = "gemini_enhanced"
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Gemini enhancement failed. Falling back to rule-based health analysis.");
            return new AiEnhancementTelemetry
            {
                LatencyMs = stopwatch.ElapsedMilliseconds,
                FallbackMode = "rule_engine_fallback"
            };
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

    private sealed class AiEnhancementTelemetry
    {
        public string? Model { get; set; }
        public long LatencyMs { get; set; }
        public string FallbackMode { get; set; } = "rule_engine";
    }

    private async Task<InferredCheckInContext?> TryInferContextWithGeminiAsync(
        string? note,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        try
        {
            var response = await _geminiService.GenerateAsync(new GeminiGenerateRequest
            {
                SystemInstruction = """
Bạn là bộ trích xuất dữ liệu check-in sau sinh cho CareMate.
Chỉ chuyển câu tiếng Việt tự nhiên của người dùng thành JSON có cấu trúc.
Không chẩn đoán, không tư vấn, không bịa thông tin không có trong câu.
Chỉ trả JSON hợp lệ, không markdown.
""",
                Prompt = $$"""
Trích xuất context y tế từ ghi chú sau:
"{{note.Trim()}}"

JSON schema:
{
  "painLevel": 1-10 hoặc null,
  "painLocation": "vị trí đau tiếng Việt" hoặc null,
  "painType": "kiểu đau tiếng Việt" hoặc null,
  "painTrend": "Worse" | "Better" | "Stable" | null,
  "symptoms": ["triệu chứng tiếng Việt"]
}

Quy ước:
- "rất nhiều", "đau lắm", "đau quá", "dữ dội", "không chịu nổi" => painLevel 8-9.
- "nhiều", "khá đau", "tăng lên", "nặng hơn" => painLevel 6-7.
- "âm ỉ", "hơi đau", "nhẹ" => painLevel 3-4.
- Nếu chỉ nói có đau nhưng không rõ mức, đặt painLevel 5.
- Nếu nói "đau bụng dưới", painLocation là "bụng dưới"; tương tự cho lưng, ngực/sữa, vết mổ/khâu, tầng sinh môn, bắp chân...
- Chỉ đưa symptom/location xuất hiện hoặc suy ra trực tiếp từ ghi chú.
""",
                Temperature = 0,
                MaxOutputTokens = 220
            }, cancellationToken);

            var extracted = ParseGeminiInference(response.Text);
            if (extracted is null)
            {
                return null;
            }

            return new InferredCheckInContext
            {
                PainLevel = extracted.PainLevel is >= 1 and <= 10 ? extracted.PainLevel : null,
                PainLocation = Clean(extracted.PainLocation),
                PainType = Clean(extracted.PainType),
                PainTrend = NormalizePainTrend(extracted.PainTrend),
                Symptoms = CleanList(extracted.Symptoms)
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Gemini context extraction failed. Falling back to local text inference.");
            return null;
        }
    }

    private static GeminiInferredCheckInContext? ParseGeminiInference(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return JsonSerializer.Deserialize<GeminiInferredCheckInContext>(text[start..(end + 1)], JsonOptions);
    }

    private static string? NormalizePainTrend(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim() switch
        {
            var trend when trend.Equals("Worse", StringComparison.OrdinalIgnoreCase) => "Worse",
            var trend when trend.Equals("Better", StringComparison.OrdinalIgnoreCase) => "Better",
            var trend when trend.Equals("Stable", StringComparison.OrdinalIgnoreCase) => "Stable",
            var trend when VietnameseTextHelper.ContainsAny(trend, "tăng", "tang", "nặng hơn", "nang hon") => "Worse",
            var trend when VietnameseTextHelper.ContainsAny(trend, "giảm", "giam", "bớt", "bot") => "Better",
            _ => null
        };
    }

    private static InferredCheckInContext InferContextFromNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return new InferredCheckInContext();
        }

        var normalized = VietnameseTextHelper.RemoveDiacritics(note).ToLowerInvariant();
        var inferred = new InferredCheckInContext
        {
            PainLevel = InferPainLevel(normalized),
            PainLocation = InferPainLocation(normalized),
            PainType = InferPainType(normalized),
            PainTrend = InferPainTrend(normalized)
        };

        if (normalized.Contains("dau", StringComparison.OrdinalIgnoreCase))
        {
            inferred.Symptoms.Add(inferred.PainLocation is null ? "đau" : $"đau {inferred.PainLocation}");
        }

        if (VietnameseTextHelper.ContainsAny(note, "sốt", "sot"))
        {
            inferred.Symptoms.Add("sốt");
        }

        if (VietnameseTextHelper.ContainsAny(note, "khó thở", "kho tho"))
        {
            inferred.Symptoms.Add("khó thở");
        }

        if (VietnameseTextHelper.ContainsAny(note, "chóng mặt", "chong mat"))
        {
            inferred.Symptoms.Add("chóng mặt");
        }

        if (VietnameseTextHelper.ContainsAny(note, "buồn nôn", "buon non", "nôn", "non"))
        {
            inferred.Symptoms.Add("buồn nôn");
        }

        return inferred;
    }

    private static int? InferPainLevel(string normalizedNote)
    {
        var numericMatch = Regex.Match(normalizedNote, @"(?:dau|muc dau|cap do dau|pain)\D{0,24}(10|[1-9])\s*(?:/|tren)?\s*10?");
        if (numericMatch.Success && int.TryParse(numericMatch.Groups[1].Value, out var explicitLevel))
        {
            return Math.Clamp(explicitLevel, 1, 10);
        }

        if (ContainsAnyNormalized(
                normalizedNote,
                "khong chiu noi",
                "du doi",
                "rat du doi",
                "dau qua",
                "dau lam",
                "rat dau",
                "rat nhieu",
                "nhieu qua",
                "nhieu lam",
                "qua nhieu",
                "quang quai",
                "vat va vi dau"))
        {
            return 8;
        }

        if (ContainsAnyNormalized(normalizedNote, "dau nhieu", "nhieu", "kha dau", "dau nang", "tang len", "nang hon"))
        {
            return 6;
        }

        if (ContainsAnyNormalized(normalizedNote, "hoi dau", "am i", "nhe", "it dau", "dau it"))
        {
            return 3;
        }

        return normalizedNote.Contains("dau", StringComparison.OrdinalIgnoreCase) ? 5 : null;
    }

    private static string? InferPainLocation(string normalizedNote)
    {
        foreach (var (location, keywords) in PainLocationKeywords)
        {
            if (ContainsAnyNormalized(normalizedNote, keywords))
            {
                return location;
            }
        }

        if (normalizedNote.Contains("bung", StringComparison.OrdinalIgnoreCase)) return "bụng";
        return null;
    }

    private static string? InferPainType(string normalizedNote)
    {
        if (ContainsAnyNormalized(normalizedNote, "quang", "co that")) return "quặn";
        if (ContainsAnyNormalized(normalizedNote, "nhoi")) return "nhói";
        if (ContainsAnyNormalized(normalizedNote, "rat", "nong rat")) return "rát";
        if (ContainsAnyNormalized(normalizedNote, "am i")) return "âm ỉ";
        return null;
    }

    private static string? InferPainTrend(string normalizedNote)
    {
        if (ContainsAnyNormalized(normalizedNote, "tang len", "nang hon", "te hon", "dau hon", "moi luc mot dau")) return "Worse";
        if (ContainsAnyNormalized(normalizedNote, "giam", "do hon", "bot dau")) return "Better";
        return null;
    }

    private static bool ContainsAnyNormalized(string normalizedText, params string[] keywords) =>
        keywords.Any(keyword => normalizedText.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private sealed class InferredCheckInContext
    {
        public int? PainLevel { get; set; }
        public string? PainLocation { get; set; }
        public string? PainType { get; set; }
        public string? PainTrend { get; set; }
        public List<string> Symptoms { get; set; } = [];
    }

    private sealed class GeminiInferredCheckInContext
    {
        public int? PainLevel { get; set; }
        public string? PainLocation { get; set; }
        public string? PainType { get; set; }
        public string? PainTrend { get; set; }
        public List<string> Symptoms { get; set; } = [];
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
            FollowUpQuestions = BuildFollowUpQuestions(DeserializeStringList(analysis.MissingDataItemsJson)),
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
            FollowUpQuestions = result.FollowUpQuestions,
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
            PainLevel = checkIn.PainLevel > 0 ? checkIn.PainLevel : null,
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
            PainLevel = checkIn.PainLevel > 0 ? checkIn.PainLevel : null,
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

    private static List<FollowUpQuestionDto> BuildFollowUpQuestions(List<string> missingItems)
    {
        return missingItems
            .Take(6)
            .Select(item => item switch
            {
                "Mức đau" => new FollowUpQuestionDto { Key = "painLevel", QuestionVi = "Hiện tại mẹ đau ở mức mấy trên thang 1-10?", InputType = "scale" },
                "Huyết áp" => new FollowUpQuestionDto { Key = "bloodPressure", QuestionVi = "Huyết áp gần nhất của mẹ là bao nhiêu?", InputType = "blood_pressure", Unit = "mmHg" },
                "Nhiệt độ" => new FollowUpQuestionDto { Key = "temperatureCelsius", QuestionVi = "Nhiệt độ cơ thể hiện tại của mẹ là bao nhiêu?", InputType = "number", Unit = "°C" },
                "Ngày sau sinh" => new FollowUpQuestionDto { Key = "postpartumDay", QuestionVi = "Mẹ đang ở ngày thứ mấy sau sinh?", InputType = "number", Unit = "ngày" },
                "Sản dịch" => new FollowUpQuestionDto { Key = "bleedingLevel", QuestionVi = "Sản dịch/ra máu hiện ở mức nào?", InputType = "select" },
                "Vết mổ/khâu" => new FollowUpQuestionDto { Key = "incisionStatus", QuestionVi = "Vết mổ hoặc vết khâu hiện thế nào?", InputType = "select" },
                "Tã ướt của bé" => new FollowUpQuestionDto { Key = "babyWetDiapers", QuestionVi = "Trong 24 giờ qua bé có khoảng bao nhiêu tã ướt?", InputType = "number", Unit = "tã" },
                "Hoạt động của bé" => new FollowUpQuestionDto { Key = "babyActivity", QuestionVi = "Bé hôm nay tỉnh táo hay lừ đừ hơn thường ngày?", InputType = "select" },
                "Tuổi mẹ" => new FollowUpQuestionDto { Key = "motherAge", QuestionVi = "Tuổi của mẹ là bao nhiêu?", InputType = "number", Unit = "tuổi" },
                _ => new FollowUpQuestionDto { Key = "missingData", QuestionVi = $"Bổ sung thông tin: {item}", InputType = "text" }
            })
            .ToList();
    }

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
