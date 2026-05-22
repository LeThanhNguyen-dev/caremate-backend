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
    private static readonly string[] DangerousKeywords =
    [
        "sốt cao", "sot cao", "khó thở", "kho tho", "chảy máu nhiều", "chay mau nhieu",
        "đau dữ dội", "dau du doi", "vết mổ sưng đỏ", "vet mo sung do",
        "vết mổ chảy dịch", "vet mo chay dich"
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
        var availableServices = await GetAvailableServicesAsync(cancellationToken);
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

        var baseline = BuildRuleBasedAnalysis(checkIn, recentHistory, availableServices);
        HealthAnalysisResult analysisResult;
        try
        {
            var aiResult = await _openAiHealthAnalysisService.AnalyzeAsync(checkIn, recentHistory, availableServices, cancellationToken);
            analysisResult = MergeAiTextIntoRuleBasedResult(baseline, aiResult);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to rule-based health analysis for user {UserId}", userId);
            analysisResult = baseline;
        }

        analysisResult = NormalizeAnalysisResult(analysisResult, checkIn, recentHistory, availableServices);

        var analysis = new AiHealthAnalysis
        {
            Id = Guid.NewGuid(),
            HealthCheckInId = checkIn.Id,
            Summary = analysisResult.Summary,
            WarningLevel = analysisResult.WarningLevel,
            RiskScore = analysisResult.RiskScore,
            ConfidenceScore = analysisResult.ConfidenceScore,
            TrendSummary = analysisResult.TrendSummary,
            RiskFactorsJson = JsonSerializer.Serialize(analysisResult.RiskFactors, JsonOptions),
            TrendSignalsJson = JsonSerializer.Serialize(analysisResult.TrendSignals, JsonOptions),
            RecommendationsJson = JsonSerializer.Serialize(analysisResult.Recommendations, JsonOptions),
            CarePlanJson = JsonSerializer.Serialize(analysisResult.CarePlan, JsonOptions),
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

    private static HealthAnalysisResult BuildRuleBasedAnalysis(
        HealthCheckIn currentCheckIn,
        List<HealthCheckIn> recentHistory,
        IReadOnlyList<SuggestedServiceDto> availableServices)
    {
        var factors = BuildRiskFactors(currentCheckIn, recentHistory);
        var riskScore = Math.Min(100, factors.Sum(x => x.Points));
        var warningLevel = DetermineWarningLevel(riskScore, currentCheckIn, recentHistory);
        var trendSignals = BuildTrendSignals(recentHistory);

        return new HealthAnalysisResult
        {
            Summary = BuildSummary(warningLevel, riskScore, factors),
            WarningLevel = warningLevel,
            RiskScore = riskScore,
            ConfidenceScore = Math.Min(95, 45 + recentHistory.Count * 7),
            TrendSummary = BuildTrendSummary(trendSignals, recentHistory),
            RiskFactors = factors,
            TrendSignals = trendSignals,
            Recommendations = BuildRecommendations(currentCheckIn, recentHistory, factors, warningLevel),
            CarePlan = BuildCarePlan(warningLevel, currentCheckIn, recentHistory),
            SuggestedServices = BuildSuggestedServices(currentCheckIn, recentHistory, availableServices),
            RawAiResponse = null
        };
    }

    private static HealthAnalysisResult MergeAiTextIntoRuleBasedResult(HealthAnalysisResult baseline, HealthAnalysisResult aiResult)
    {
        baseline.RawAiResponse = aiResult.RawAiResponse;

        if (baseline.RiskScore < 30 && !string.IsNullOrWhiteSpace(aiResult.Summary))
        {
            baseline.Summary = aiResult.Summary.Trim();
        }

        if (aiResult.Recommendations.Count > 0)
        {
            baseline.Recommendations = baseline.Recommendations
                .Concat(aiResult.Recommendations)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();
        }

        if (aiResult.CarePlan.Count > 0)
        {
            baseline.CarePlan = aiResult.CarePlan
                .Where(x => !string.IsNullOrWhiteSpace(x.Action))
                .Take(5)
                .ToList();
        }

        return baseline;
    }

    private static List<RiskFactorDto> BuildRiskFactors(HealthCheckIn currentCheckIn, List<HealthCheckIn> recentHistory)
    {
        var factors = new List<RiskFactorDto>();

        AddFactorIf(factors, HasDangerKeyword(currentCheckIn.Note), "danger_note", "Ghi chú có dấu hiệu nguy hiểm", 60);
        AddFactorIf(factors, currentCheckIn.PainLevel >= 9, "severe_pain", "Mức đau rất cao", 45);
        AddFactorIf(factors, currentCheckIn.PainLevel == 8, "high_pain", "Mức đau cao", 35);
        AddFactorIf(factors, currentCheckIn.PainLevel is >= 6 and <= 7, "elevated_pain", "Mức đau đang tăng", 20);
        AddFactorIf(factors, currentCheckIn.SleepHours < 4, "very_low_sleep", "Ngủ dưới 4 giờ", 25);
        AddFactorIf(factors, currentCheckIn.SleepHours is >= 4 and < 5, "low_sleep", "Ngủ dưới 5 giờ", 18);
        AddFactorIf(factors, currentCheckIn.SleepHours is >= 5 and < 6, "reduced_sleep", "Giấc ngủ hơi thấp", 10);
        AddFactorIf(factors, IsStressMood(currentCheckIn.Mood), "stress_mood", "Tâm trạng căng thẳng hoặc lo âu", 18);
        AddFactorIf(factors, IsLowMilk(currentCheckIn.MilkStatus), "milk_concern", "Có vấn đề về sữa hoặc đau khi cho bú", 15);
        AddFactorIf(factors, currentCheckIn.BabyFeeding.Equals("RefusesFeeding", StringComparison.OrdinalIgnoreCase), "baby_refuses_feeding", "Bé từ chối bú", 35);
        AddFactorIf(factors, currentCheckIn.BabyFeeding.Equals("LessThanUsual", StringComparison.OrdinalIgnoreCase), "baby_feeds_less", "Bé bú ít hơn thường ngày", 22);
        AddFactorIf(factors, IsBabySleepConcern(currentCheckIn.BabySleep), "baby_sleep_concern", "Bé quấy khóc hoặc thức giấc nhiều", 10);

        var lastThree = recentHistory.Take(3).ToList();
        AddFactorIf(factors, lastThree.Count == 3 && lastThree.Count(x => x.SleepHours < 5) >= 3, "repeated_low_sleep", "Mẹ ngủ dưới 5 giờ trong 3 lần check-in gần nhất", 25);
        AddFactorIf(factors, recentHistory.Count(x => IsStressMood(x.Mood)) >= 3, "repeated_stress", "Stress hoặc lo âu lặp lại nhiều lần trong lịch sử gần đây", 22);
        AddFactorIf(factors, recentHistory.Count(x => IsFeedingConcern(x.BabyFeeding)) >= 2, "repeated_feeding_concern", "Tình trạng bú của bé bất thường lặp lại", 25);
        AddFactorIf(factors, IsPainIncreasing(recentHistory), "pain_increasing", "Mức đau có xu hướng tăng", 15);

        return factors
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    private static void AddFactorIf(List<RiskFactorDto> factors, bool condition, string code, string label, int points)
    {
        if (condition)
        {
            factors.Add(new RiskFactorDto { Code = code, Label = label, Points = points });
        }
    }

    private static string DetermineWarningLevel(int riskScore, HealthCheckIn currentCheckIn, List<HealthCheckIn> recentHistory)
    {
        if (HasDangerKeyword(currentCheckIn.Note)
            || currentCheckIn.PainLevel >= 9
            || currentCheckIn.BabyFeeding.Equals("RefusesFeeding", StringComparison.OrdinalIgnoreCase)
            || riskScore >= 60)
        {
            return "High";
        }

        if (currentCheckIn.PainLevel >= 8
            || riskScore >= 30
            || recentHistory.Take(3).Count(x => x.SleepHours < 5) >= 3)
        {
            return "Medium";
        }

        return "Low";
    }

    private static List<TrendSignalDto> BuildTrendSignals(List<HealthCheckIn> recentHistory)
    {
        if (recentHistory.Count < 2)
        {
            return
            [
                new() { Metric = "Dữ liệu", Direction = "stable", Summary = "Chưa đủ dữ liệu để nhận diện xu hướng." }
            ];
        }

        var ordered = recentHistory.OrderBy(x => x.CreatedAt).ToList();
        var oldest = ordered.First();
        var newest = ordered.Last();
        var previousHalf = ordered.Take(Math.Max(1, ordered.Count / 2)).ToList();
        var latestHalf = ordered.Skip(Math.Max(1, ordered.Count / 2)).ToList();

        var sleepDiff = newest.SleepHours - oldest.SleepHours;
        var painDiff = newest.PainLevel - oldest.PainLevel;
        var previousStress = previousHalf.Count(IsStressMood);
        var latestStress = latestHalf.Count(IsStressMood);
        var previousFeeding = previousHalf.Count(x => IsFeedingConcern(x.BabyFeeding));
        var latestFeeding = latestHalf.Count(x => IsFeedingConcern(x.BabyFeeding));

        return
        [
            new()
            {
                Metric = "Giấc ngủ",
                Direction = sleepDiff <= -1 ? "down" : sleepDiff >= 1 ? "up" : "stable",
                Summary = sleepDiff <= -1 ? "Giấc ngủ đang giảm." : sleepDiff >= 1 ? "Giấc ngủ đang cải thiện." : "Giấc ngủ tương đối ổn định."
            },
            new()
            {
                Metric = "Mức đau",
                Direction = painDiff >= 2 ? "up" : painDiff <= -2 ? "down" : "stable",
                Summary = painDiff >= 2 ? "Mức đau đang tăng." : painDiff <= -2 ? "Mức đau đang giảm." : "Mức đau chưa thay đổi lớn."
            },
            new()
            {
                Metric = "Stress",
                Direction = latestStress > previousStress ? "up" : latestStress < previousStress ? "down" : "stable",
                Summary = latestStress > previousStress ? "Stress xuất hiện nhiều hơn gần đây." : latestStress < previousStress ? "Stress có dấu hiệu giảm." : "Stress chưa thay đổi rõ."
            },
            new()
            {
                Metric = "Bú của bé",
                Direction = latestFeeding > previousFeeding ? "down" : latestFeeding < previousFeeding ? "up" : "stable",
                Summary = latestFeeding > previousFeeding ? "Tình trạng bú của bé xấu hơn." : latestFeeding < previousFeeding ? "Tình trạng bú của bé cải thiện." : "Tình trạng bú của bé tương đối ổn định."
            }
        ];
    }

    private static string BuildTrendSummary(List<TrendSignalDto> signals, List<HealthCheckIn> recentHistory)
    {
        if (recentHistory.Count < 2)
        {
            return "Chưa đủ dữ liệu để nhận diện xu hướng. Hãy tiếp tục check-in để hệ thống theo dõi chính xác hơn.";
        }

        return string.Join(" ", signals.Select(x => x.Summary));
    }

    private static string BuildSummary(string warningLevel, int riskScore, List<RiskFactorDto> factors)
    {
        var mainFactors = factors
            .OrderByDescending(x => x.Points)
            .Take(3)
            .Select(x => x.Label.ToLowerInvariant())
            .ToList();
        var reason = mainFactors.Count > 0 ? $" Các yếu tố chính gồm: {string.Join(", ", mainFactors)}." : string.Empty;

        return warningLevel switch
        {
            "High" => $"Điểm rủi ro hiện tại là {riskScore}/100, thuộc mức cao. Cần ưu tiên theo dõi sát và cân nhắc liên hệ cơ sở y tế nếu triệu chứng kéo dài hoặc nặng hơn.{reason}",
            "Medium" => $"Điểm rủi ro hiện tại là {riskScore}/100, thuộc mức trung bình. Tình trạng chưa nên xem là ổn định hoàn toàn và cần theo dõi thêm trong 24-48 giờ tới.{reason}",
            _ => $"Điểm rủi ro hiện tại là {riskScore}/100, thuộc mức thấp. Tình trạng tương đối ổn nhưng vẫn nên tiếp tục check-in hằng ngày để phát hiện thay đổi sớm.{reason}"
        };
    }

    private static List<string> BuildRecommendations(
        HealthCheckIn currentCheckIn,
        List<HealthCheckIn> recentHistory,
        List<RiskFactorDto> factors,
        string warningLevel)
    {
        var recommendations = new List<string>();

        if (warningLevel == "High")
        {
            recommendations.Add("Ưu tiên an toàn: liên hệ bác sĩ hoặc cơ sở y tế nếu triệu chứng tiếp tục tăng, bé bỏ bú, sốt cao, khó thở hoặc có chảy máu bất thường.");
        }

        if (currentCheckIn.SleepHours < 5 || factors.Any(x => x.Code == "repeated_low_sleep"))
        {
            recommendations.Add("Ưu tiên nghỉ ngơi theo từng khoảng ngắn và nhờ người thân hoặc dịch vụ chăm bé hỗ trợ để mẹ có thêm thời gian ngủ.");
        }

        if (currentCheckIn.PainLevel >= 6 || factors.Any(x => x.Code == "pain_increasing"))
        {
            recommendations.Add("Theo dõi mức đau trong 24 giờ tới; nếu đau tăng, đau kéo dài hoặc kèm dấu hiệu bất thường, nên trao đổi với nhân viên y tế.");
        }

        if (IsStressMood(currentCheckIn.Mood) || factors.Any(x => x.Code == "repeated_stress"))
        {
            recommendations.Add("Theo dõi tâm trạng và mức căng thẳng; nếu stress hoặc lo âu lặp lại nhiều ngày, nên có người hỗ trợ chăm sóc và cân nhắc tư vấn tinh thần.");
        }

        if (IsFeedingConcern(currentCheckIn.BabyFeeding) || factors.Any(x => x.Code == "repeated_feeding_concern"))
        {
            recommendations.Add("Theo dõi số lần bú, lượng bú và biểu hiện của bé; nếu bé tiếp tục bú ít hoặc từ chối bú, nên liên hệ người có chuyên môn.");
        }

        if (IsLowMilk(currentCheckIn.MilkStatus))
        {
            recommendations.Add("Nếu sữa ít hoặc đau khi cho bú kéo dài, nên cân nhắc tư vấn cho bú để điều chỉnh tư thế và lịch bú phù hợp.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Tiếp tục theo dõi tình trạng của mẹ và bé hằng ngày, đặc biệt là giấc ngủ, mức đau và tình trạng bú của bé.");
        }

        return recommendations.Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();
    }

    private static List<CarePlanItemDto> BuildCarePlan(string warningLevel, HealthCheckIn currentCheckIn, List<HealthCheckIn> recentHistory)
    {
        var plan = new List<CarePlanItemDto>();

        if (warningLevel == "High")
        {
            plan.Add(new CarePlanItemDto
            {
                Timeframe = "Ngay hôm nay",
                Action = "Theo dõi sát dấu hiệu bất thường và liên hệ cơ sở y tế nếu triệu chứng không giảm.",
                Reason = "Điểm rủi ro đang ở mức cao hoặc có yếu tố nguy hiểm."
            });
        }

        plan.Add(new CarePlanItemDto
        {
            Timeframe = "Trong 24 giờ",
            Action = "Ghi lại giấc ngủ, mức đau, tâm trạng, tình trạng sữa và lượng bú của bé ở lần check-in tiếp theo.",
            Reason = "Dữ liệu liên tục giúp hệ thống nhận diện xu hướng chính xác hơn."
        });

        if (currentCheckIn.SleepHours < 5 || recentHistory.Take(3).Count(x => x.SleepHours < 5) >= 3)
        {
            plan.Add(new CarePlanItemDto
            {
                Timeframe = "1-3 ngày tới",
                Action = "Sắp xếp thêm thời gian nghỉ và cân nhắc hỗ trợ chăm bé nếu thiếu ngủ kéo dài.",
                Reason = "Thiếu ngủ nhiều ngày làm tăng rủi ro mệt mỏi và stress."
            });
        }

        if (IsFeedingConcern(currentCheckIn.BabyFeeding) || IsLowMilk(currentCheckIn.MilkStatus))
        {
            plan.Add(new CarePlanItemDto
            {
                Timeframe = "1-2 ngày tới",
                Action = "Theo dõi việc bú của bé và cân nhắc tư vấn cho bú nếu tình trạng không cải thiện.",
                Reason = "Bú ít hoặc khó bú cần được quan sát sát để đảm bảo bé nhận đủ dinh dưỡng."
            });
        }

        return plan.Take(5).ToList();
    }

    private static List<SuggestedServiceDto> BuildSuggestedServices(
        HealthCheckIn currentCheckIn,
        List<HealthCheckIn> recentHistory,
        IReadOnlyList<SuggestedServiceDto> availableServices)
    {
        var services = new List<SuggestedServiceDto>();
        var repeatedFeedingConcern = recentHistory.Count(x => IsFeedingConcern(x.BabyFeeding)) >= 2;
        var repeatedStress = recentHistory.Count(x => IsStressMood(x.Mood)) >= 3;
        var repeatedMilkConcern = recentHistory.Count(x => IsLowMilk(x.MilkStatus)) >= 2;
        var painIncreasing = IsPainIncreasing(recentHistory);

        if (currentCheckIn.PainLevel >= 6 || painIncreasing)
        {
            AddService(services, availableServices, ["mẹ", "me", "sau sinh", "phục hồi", "phuc hoi", "massage", "sức khỏe", "suc khoe"], "Mẹ đang đau hoặc mức đau có xu hướng tăng, cần thêm hỗ trợ chăm sóc sau sinh.");
        }

        if (IsLowMilk(currentCheckIn.MilkStatus) || repeatedMilkConcern)
        {
            AddService(services, availableServices, ["sữa", "sua", "bú", "bu", "cho bú"], "Tình trạng sữa hoặc cho bú có dấu hiệu cần thêm hỗ trợ chuyên môn.");
        }

        if (IsFeedingConcern(currentCheckIn.BabyFeeding) || repeatedFeedingConcern)
        {
            AddService(services, availableServices, ["bé", "be", "sơ sinh", "so sinh", "tắm bé", "tam be", "theo dõi sức khỏe bé"], "Tình trạng bú của bé từng bất thường nhiều lần, cần được theo dõi và hỗ trợ sát hơn.");
        }

        if (IsStressMood(currentCheckIn.Mood) || repeatedStress)
        {
            AddService(services, availableServices, ["tâm lý", "tam ly", "tinh thần", "tinh than"], "Mẹ có dấu hiệu căng thẳng hoặc lo âu lặp lại, nên có thêm hỗ trợ tinh thần.");
        }

        if (recentHistory.Take(3).Count(x => x.SleepHours < 5) >= 3)
        {
            AddService(services, availableServices, ["ban đêm", "ban dem", "bé", "be", "sơ sinh", "so sinh"], "Mẹ thiếu ngủ nhiều lần gần đây, có thể cần hỗ trợ chăm bé để nghỉ ngơi.");
        }

        return services.Take(4).ToList();
    }

    private static HealthAnalysisResult NormalizeAnalysisResult(
        HealthAnalysisResult input,
        HealthCheckIn currentCheckIn,
        List<HealthCheckIn> recentHistory,
        IReadOnlyList<SuggestedServiceDto> availableServices)
    {
        var serviceLookup = availableServices.ToDictionary(x => x.ServiceKey, StringComparer.OrdinalIgnoreCase);
        var ruleResult = BuildRuleBasedAnalysis(currentCheckIn, recentHistory, availableServices);

        input.RiskScore = ruleResult.RiskScore;
        input.ConfidenceScore = ruleResult.ConfidenceScore;
        input.WarningLevel = ruleResult.WarningLevel;
        input.RiskFactors = ruleResult.RiskFactors;
        input.TrendSignals = ruleResult.TrendSignals;
        input.TrendSummary = ruleResult.TrendSummary;

        if (input.WarningLevel != "Low")
        {
            input.Summary = ruleResult.Summary;
        }
        else if (string.IsNullOrWhiteSpace(input.Summary))
        {
            input.Summary = ruleResult.Summary;
        }

        input.Recommendations = input.Recommendations
            .Concat(ruleResult.Recommendations)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        input.CarePlan = input.CarePlan.Count > 0 ? input.CarePlan : ruleResult.CarePlan;
        input.CarePlan = input.CarePlan
            .Where(x => !string.IsNullOrWhiteSpace(x.Action))
            .Take(5)
            .ToList();

        input.SuggestedServices = input.SuggestedServices
            .Concat(ruleResult.SuggestedServices)
            .Where(x => !string.IsNullOrWhiteSpace(x.ServiceKey) && serviceLookup.ContainsKey(x.ServiceKey))
            .GroupBy(x => x.ServiceKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var item = group.First();
                item.ServiceName = serviceLookup[item.ServiceKey].ServiceName;
                item.Reason = string.IsNullOrWhiteSpace(item.Reason) ? serviceLookup[item.ServiceKey].Reason : item.Reason.Trim();
                return item;
            })
            .Take(4)
            .ToList();

        return input;
    }

    private HealthAnalysisResponse MapAnalysisResponse(Guid checkInId, AiHealthAnalysis analysis)
    {
        return new HealthAnalysisResponse
        {
            CheckInId = checkInId,
            AnalysisId = analysis.Id,
            Summary = analysis.Summary,
            WarningLevel = analysis.WarningLevel,
            RiskScore = analysis.RiskScore,
            ConfidenceScore = analysis.ConfidenceScore,
            TrendSummary = analysis.TrendSummary,
            RiskFactors = DeserializeRiskFactors(analysis.RiskFactorsJson),
            TrendSignals = DeserializeTrendSignals(analysis.TrendSignalsJson),
            Recommendations = DeserializeRecommendations(analysis.RecommendationsJson),
            CarePlan = DeserializeCarePlan(analysis.CarePlanJson),
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

    private static List<string> DeserializeRecommendations(string json) => JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];

    private static List<CarePlanItemDto> DeserializeCarePlan(string json) => JsonSerializer.Deserialize<List<CarePlanItemDto>>(json, JsonOptions) ?? [];

    private static List<SuggestedServiceDto> DeserializeSuggestedServices(string json) => JsonSerializer.Deserialize<List<SuggestedServiceDto>>(json, JsonOptions) ?? [];

    private static List<RiskFactorDto> DeserializeRiskFactors(string json) => JsonSerializer.Deserialize<List<RiskFactorDto>>(json, JsonOptions) ?? [];

    private static List<TrendSignalDto> DeserializeTrendSignals(string json) => JsonSerializer.Deserialize<List<TrendSignalDto>>(json, JsonOptions) ?? [];

    private static bool IsStressMood(HealthCheckIn checkIn) => IsStressMood(checkIn.Mood);

    private static bool IsStressMood(string mood)
    {
        return mood.Equals("Stressed", StringComparison.OrdinalIgnoreCase)
            || mood.Equals("Anxious", StringComparison.OrdinalIgnoreCase)
            || mood.Equals("Overwhelmed", StringComparison.OrdinalIgnoreCase);
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

    private static bool IsBabySleepConcern(string babySleep)
    {
        return babySleep.Equals("CryingOften", StringComparison.OrdinalIgnoreCase)
            || babySleep.Equals("WakingFrequently", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDangerKeyword(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return false;
        }

        return DangerousKeywords.Any(keyword => note.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPainIncreasing(List<HealthCheckIn> recentHistory)
    {
        if (recentHistory.Count < 3)
        {
            return false;
        }

        var ordered = recentHistory.OrderBy(x => x.CreatedAt).ToList();
        return ordered.Last().PainLevel - ordered.First().PainLevel >= 2;
    }

    private static void AddService(
        List<SuggestedServiceDto> services,
        IReadOnlyList<SuggestedServiceDto> availableServices,
        string[] keywords,
        string reason)
    {
        var service = availableServices.FirstOrDefault(x =>
            keywords.Any(keyword => x.ServiceName.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        if (service is null || services.Any(x => x.ServiceKey.Equals(service.ServiceKey, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        services.Add(new SuggestedServiceDto
        {
            ServiceKey = service.ServiceKey,
            ServiceName = service.ServiceName,
            Reason = reason
        });
    }
}
