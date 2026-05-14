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
    private const string Disclaimer = "Thông tin từ AI chỉ mang tính tham khảo, không thay thế tư vấn từ bác sĩ hoặc chuyên gia y tế.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<string, string> ServiceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["postpartum-mother-care"] = "Chăm sóc mẹ sau sinh",
        ["newborn-care"] = "Hỗ trợ chăm bé sơ sinh",
        ["breastfeeding-support"] = "Tư vấn cho bé bú",
        ["wound-monitoring-support"] = "Hỗ trợ theo dõi vết mổ",
        ["mental-wellness-support"] = "Hỗ trợ tinh thần sau sinh",
        ["baby-bath-care"] = "Tắm bé tại nhà",
        ["nutrition-guidance"] = "Tư vấn dinh dưỡng sau sinh"
    };
    private static readonly string[] DangerousKeywords =
    [
        "sốt cao",
        "khó thở",
        "chảy máu nhiều",
        "đau dữ dội",
        "vết mổ sưng đỏ",
        "vết mổ chảy dịch"
    ];

    private readonly MomCareContext _context;
    private readonly IOpenAiHealthAnalysisService _openAiHealthAnalysisService;
    private readonly ILogger<HealthCheckInService> _logger;

    public HealthCheckInService(
        MomCareContext context,
        IOpenAiHealthAnalysisService openAiHealthAnalysisService,
        ILogger<HealthCheckInService> logger)
    {
        _context = context;
        _openAiHealthAnalysisService = openAiHealthAnalysisService;
        _logger = logger;
    }

    public async Task<HealthAnalysisResponse> AnalyzeAsync(int userId, AnalyzeHealthCheckInRequest request, CancellationToken cancellationToken)
    {
        var checkIn = new HealthCheckIn
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SleepHours = request.SleepHours,
            PainLevel = request.PainLevel,
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

        HealthAnalysisResult analysisResult;
        try
        {
            analysisResult = await _openAiHealthAnalysisService.AnalyzeAsync(checkIn, recentHistory, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to rule-based health analysis for user {UserId}", userId);
            analysisResult = BuildFallbackAnalysis(checkIn, recentHistory);
        }

        analysisResult = NormalizeAnalysisResult(analysisResult, checkIn, recentHistory);

        var analysis = new AiHealthAnalysis
        {
            Id = Guid.NewGuid(),
            HealthCheckInId = checkIn.Id,
            Summary = analysisResult.Summary,
            WarningLevel = analysisResult.WarningLevel,
            RecommendationsJson = JsonSerializer.Serialize(analysisResult.Recommendations, JsonOptions),
            SuggestedServicesJson = JsonSerializer.Serialize(analysisResult.SuggestedServices, JsonOptions),
            RawAiResponse = analysisResult.RawAiResponse,
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

    private static HealthAnalysisResult BuildFallbackAnalysis(HealthCheckIn currentCheckIn, List<HealthCheckIn> recentHistory)
    {
        var warningLevel = "Low";
        var recommendations = new List<string>();
        var suggestedServices = new List<SuggestedServiceDto>();

        if (HasDangerKeyword(currentCheckIn.Note))
        {
            warningLevel = "High";
            recommendations.Add("Có dấu hiệu cảnh báo nghiêm trọng. Hãy liên hệ cơ sở y tế hoặc bác sĩ sớm nhất có thể.");
        }
        else if (currentCheckIn.PainLevel >= 8)
        {
            warningLevel = "High";
        }
        else if (currentCheckIn.SleepHours < 5 || IsStressMood(currentCheckIn.Mood))
        {
            warningLevel = "Medium";
        }

        if (currentCheckIn.PainLevel >= 7)
        {
            AddService(suggestedServices, "postpartum-mother-care", "Mẹ đang đau nhiều và cần thêm hỗ trợ chăm sóc sau sinh.");
        }

        if (IsLowMilk(currentCheckIn.MilkStatus))
        {
            AddService(suggestedServices, "breastfeeding-support", "Tình trạng sữa hiện tại cần thêm hỗ trợ cho bé bú.");
        }

        if (IsFeedingConcern(currentCheckIn.BabyFeeding))
        {
            AddService(suggestedServices, "newborn-care", "Bé bú ít hơn thường lệ hoặc từ chối bú, cần được theo dõi sát hơn.");
        }

        if (IsStressMood(currentCheckIn.Mood))
        {
            AddService(suggestedServices, "mental-wellness-support", "Tâm trạng đang căng thẳng hoặc lo âu, cần thêm hỗ trợ tinh thần.");
        }

        if (recentHistory.Take(3).Count(x => x.SleepHours < 5) >= 3)
        {
            AddService(suggestedServices, "newborn-care", "Mẹ thiếu ngủ nhiều ngày liên tiếp, cần thêm hỗ trợ chăm bé.");
            AddService(suggestedServices, "postpartum-mother-care", "Mẹ thiếu ngủ nhiều ngày liên tiếp, cần thêm hỗ trợ chăm sóc sau sinh.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Theo dõi tình trạng của mẹ và bé hằng ngày để phát hiện sớm thay đổi bất thường.");
        }

        if (currentCheckIn.SleepHours < 5)
        {
            recommendations.Add("Cố gắng tranh thủ nghỉ ngơi thêm khi có thể.");
        }

        if (IsFeedingConcern(currentCheckIn.BabyFeeding))
        {
            recommendations.Add("Theo dõi lượng bú, số lần bú và tình trạng của bé trong ngày.");
        }

        if (IsLowMilk(currentCheckIn.MilkStatus))
        {
            recommendations.Add("Nếu tình trạng sữa kéo dài, hãy tham khảo người có chuyên môn về hỗ trợ cho bé bú.");
        }

        return new HealthAnalysisResult
        {
            Summary = BuildFallbackSummary(currentCheckIn, warningLevel),
            WarningLevel = warningLevel,
            Recommendations = recommendations.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SuggestedServices = suggestedServices,
            RawAiResponse = null
        };
    }

    private static string BuildFallbackSummary(HealthCheckIn currentCheckIn, string warningLevel)
    {
        if (warningLevel == "High")
        {
            return "Có một số dấu hiệu cần được ưu tiên theo dõi sát. Nên liên hệ cơ sở y tế nếu triệu chứng tăng lên hoặc kéo dài.";
        }

        if (warningLevel == "Medium")
        {
            return "Tình trạng hiện tại cho thấy mẹ có dấu hiệu mệt mỏi hoặc cần thêm hỗ trợ trong chăm sóc hằng ngày.";
        }

        return "Tình trạng hôm nay tương đối ổn định. Tiếp tục theo dõi giấc ngủ, mức độ đau và việc cho bé bú hằng ngày.";
    }

    private static HealthAnalysisResult NormalizeAnalysisResult(HealthAnalysisResult input, HealthCheckIn currentCheckIn, List<HealthCheckIn> recentHistory)
    {
        input.WarningLevel = NormalizeWarningLevel(input.WarningLevel, currentCheckIn, recentHistory);
        input.Recommendations = input.Recommendations
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        input.SuggestedServices = input.SuggestedServices
            .Where(x => !string.IsNullOrWhiteSpace(x.ServiceKey))
            .GroupBy(x => x.ServiceKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var item = group.First();
                if (string.IsNullOrWhiteSpace(item.ServiceName) && ServiceNames.TryGetValue(item.ServiceKey, out var serviceName))
                {
                    item.ServiceName = serviceName;
                }

                return item;
            })
            .ToList();

        if (input.Recommendations.Count == 0)
        {
            input.Recommendations.Add("Theo doi them tinh trang cua me va be, va lien he nguoi co chuyen mon neu co dau hieu bat thuong.");
        }

        return input;
    }

    private static string NormalizeWarningLevel(string? warningLevel, HealthCheckIn currentCheckIn, List<HealthCheckIn> recentHistory)
    {
        var normalized = warningLevel?.Trim().ToLowerInvariant() switch
        {
            "high" => "High",
            "medium" => "Medium",
            _ => "Low"
        };

        if (HasDangerKeyword(currentCheckIn.Note))
        {
            return "High";
        }

        if (currentCheckIn.PainLevel >= 8 && normalized == "Low")
        {
            return "Medium";
        }

        if (recentHistory.Take(3).Count(x => x.SleepHours < 5) >= 3 && normalized == "Low")
        {
            return "Medium";
        }

        return normalized;
    }

    private HealthAnalysisResponse MapAnalysisResponse(Guid checkInId, AiHealthAnalysis analysis)
    {
        return new HealthAnalysisResponse
        {
            CheckInId = checkInId,
            AnalysisId = analysis.Id,
            Summary = analysis.Summary,
            WarningLevel = analysis.WarningLevel,
            Recommendations = DeserializeRecommendations(analysis.RecommendationsJson),
            SuggestedServices = DeserializeSuggestedServices(analysis.SuggestedServicesJson),
            Disclaimer = Disclaimer
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
            Mood = checkIn.Mood,
            MilkStatus = checkIn.MilkStatus,
            BabyFeeding = checkIn.BabyFeeding,
            BabySleep = checkIn.BabySleep,
            Note = checkIn.Note,
            Analysis = checkIn.Analysis is null ? null : MapAnalysisResponse(checkIn.Id, checkIn.Analysis)
        };
    }

    private static List<string> DeserializeRecommendations(string json)
    {
        return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
    }

    private static List<SuggestedServiceDto> DeserializeSuggestedServices(string json)
    {
        return JsonSerializer.Deserialize<List<SuggestedServiceDto>>(json, JsonOptions) ?? [];
    }

    private static bool IsStressMood(string mood)
    {
        return mood.Equals("Stressed", StringComparison.OrdinalIgnoreCase)
            || mood.Equals("Anxious", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLowMilk(string milkStatus)
    {
        return milkStatus.Equals("Low", StringComparison.OrdinalIgnoreCase)
            || milkStatus.Equals("Painful", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFeedingConcern(string babyFeeding)
    {
        return babyFeeding.Equals("LessThanUsual", StringComparison.OrdinalIgnoreCase)
            || babyFeeding.Equals("RefusesFeeding", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDangerKeyword(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return false;
        }

        return DangerousKeywords.Any(keyword => note.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddService(List<SuggestedServiceDto> services, string serviceKey, string reason)
    {
        if (services.Any(x => x.ServiceKey.Equals(serviceKey, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        services.Add(new SuggestedServiceDto
        {
            ServiceKey = serviceKey,
            ServiceName = ServiceNames[serviceKey],
            Reason = reason
        });
    }
}
