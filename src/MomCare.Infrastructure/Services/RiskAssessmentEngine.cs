using System.Globalization;
using System.Text;
using System.Text.Json;
using MomCare.Dto;
using MomCare.Models;

namespace MomCare.Services;

public static class RiskAssessmentEngine
{
    public const string EngineVersion = "rule-v2.0";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] DangerousKeywords =
    [
        "sốt cao", "khó thở", "chảy máu nhiều", "đau dữ dội", "vết mổ sưng đỏ", "vết mổ chảy dịch"
    ];
    public static HealthAnalysisResult Analyze(
        HealthCheckIn currentCheckIn,
        List<HealthCheckIn> recentHistory,
        IReadOnlyList<SuggestedServiceDto> availableServices)
    {
        var factors = BuildRiskFactors(currentCheckIn, recentHistory);
        var riskScore = Math.Min(100, factors.Sum(x => x.Points));
        var warningLevel = DetermineWarningLevel(riskScore, currentCheckIn, recentHistory);
        var trendSignals = BuildTrendSignals(recentHistory);
        var confidence = CalculateConfidence(currentCheckIn, recentHistory);

        return new HealthAnalysisResult
        {
            Summary = BuildSummary(warningLevel, riskScore, factors),
            WarningLevel = warningLevel,
            UrgencyAction = BuildUrgencyAction(warningLevel),
            RiskScore = riskScore,
            ConfidenceScore = confidence.Score,
            TrendSummary = BuildTrendSummary(trendSignals, recentHistory),
            WeeklySummary = BuildWeeklySummary(currentCheckIn, recentHistory),
            RiskFactors = factors,
            TrendSignals = trendSignals,
            Recommendations = BuildRecommendations(currentCheckIn, recentHistory, factors, warningLevel),
            CarePlan = BuildCarePlan(warningLevel, currentCheckIn, recentHistory),
            SuggestedServices = BuildSuggestedServices(currentCheckIn, recentHistory, availableServices)
        };
    }

    private static List<RiskFactorDto> BuildRiskFactors(HealthCheckIn currentCheckIn, List<HealthCheckIn> recentHistory)
    {
        var factors = new List<RiskFactorDto>();

        AddFactorIf(factors, HasDangerKeyword(currentCheckIn.Note), "danger_note", "Ghi chú có dấu hiệu nguy hiểm", 60);
        AddFactorIf(factors, HasEmergencySymptom(currentCheckIn), "emergency_symptom", "Có triệu chứng cần xử lý khẩn cấp", 80);
        AddFactorIf(factors, HasRedFlagSymptom(currentCheckIn), "red_flag_symptom", "Có dấu hiệu cảnh báo đỏ", 55);
        AddFactorIf(factors, HasChestPainBreathingCombo(currentCheckIn), "chest_pain_breathing", "Đau ngực kèm khó thở", 90);
        AddFactorIf(factors, currentCheckIn.MotherAge >= 50 && HasChestPainBreathingCombo(currentCheckIn), "age_chest_breathing", "Trên 50 tuổi kèm đau ngực và khó thở", 30);
        AddFactorIf(factors, currentCheckIn.TemperatureCelsius >= 38.5, "high_temperature", "Sốt từ 38.5°C trở lên", 40);
        AddFactorIf(factors, currentCheckIn.SystolicBloodPressure >= 160 || currentCheckIn.DiastolicBloodPressure >= 110, "very_high_bp", "Huyết áp rất cao", 60);
        AddFactorIf(factors, currentCheckIn.SystolicBloodPressure >= 140 || currentCheckIn.DiastolicBloodPressure >= 90, "high_bp", "Huyết áp cao", 30);
        AddFactorIf(factors,
            currentCheckIn.SystolicBloodPressure >= 140 && HasSymptom(currentCheckIn, "đau đầu", "mờ mắt"),
            "preeclampsia_signs",
            "Có dấu hiệu nghi ngờ tiền sản giật - cần khám ngay",
            75);
        AddFactorIf(factors,
            currentCheckIn.TemperatureCelsius >= 38.0 && HasContextValue(currentCheckIn, "incisionStatus", "RedSwollen", "Discharge"),
            "wound_infection_signs",
            "Sốt kèm vết mổ bất thường - nghi nhiễm trùng",
            70);
        AddFactorIf(factors,
            GetContextNumber(currentCheckIn, "babyWetDiapers") is double wd
                && wd < 4
                && IsFeedingConcern(currentCheckIn.BabyFeeding)
                && HasContextValue(currentCheckIn, "babyActivity", "Lethargic"),
            "baby_dehydration_signs",
            "Bé có dấu hiệu mất nước - cần theo dõi khẩn",
            65);
        AddFactorIf(factors, HasMedicalHistory(currentCheckIn, "tiểu đường", "tieu duong", "tim mạch", "tim mach", "huyết áp", "huyet ap"), "medical_history", "Có tiền sử bệnh cần theo dõi", 20);
        AddFactorIf(factors, HasContextValue(currentCheckIn, "bleedingLevel", "Heavy"), "heavy_bleeding_context", "Sản dịch hoặc ra máu đang ở mức nhiều", 55);
        AddFactorIf(factors, HasContextValue(currentCheckIn, "incisionStatus", "RedSwollen", "Discharge"), "incision_context", "Vết mổ hoặc vết khâu có dấu hiệu cần theo dõi", 35);
        AddFactorIf(factors, HasContextValue(currentCheckIn, "swellingLevel", "Severe"), "severe_swelling_context", "Phù nhiều cần theo dõi thêm", 25);
        AddFactorIf(factors, HasContextValue(currentCheckIn, "urinationIssue", "true"), "urination_issue_context", "Có khó khăn khi tiểu tiện", 18);
        AddFactorIf(factors, GetContextNumber(currentCheckIn, "babyWetDiapers") is double wetDiapers && wetDiapers > 0 && wetDiapers < 4, "low_wet_diapers_context", "Số tã ướt của bé thấp", 30);
        AddFactorIf(factors, HasContextValue(currentCheckIn, "babyActivity", "Lethargic"), "baby_lethargy_context", "Bé có dấu hiệu lừ đừ hoặc yếu", 45);
        AddFactorIf(factors, IsWorseningPain(currentCheckIn), "pain_worsening", "Đau đang tăng lên", 18);
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
        AddFactorIf(factors, HasActiveRepeatedFeedingConcern(currentCheckIn, recentHistory), "repeated_feeding_concern", "Tình trạng bú của bé bất thường lặp lại trong các lần gần đây", 25);
        AddFactorIf(factors, IsPainIncreasing(recentHistory), "pain_increasing", "Mức đau có xu hướng tăng", 15);
        AddFactorIf(factors, IsDeterioration(recentHistory, x => x.PainLevel, 3), "pain_deterioration", "Mức đau tăng liên tục 3 lần check-in gần đây", 30);

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
        if (HasEmergencySymptom(currentCheckIn)
            || HasChestPainBreathingCombo(currentCheckIn)
            || riskScore >= 85)
        {
            return "Emergency";
        }

        if (HasDangerKeyword(currentCheckIn.Note)
            || HasRedFlagSymptom(currentCheckIn)
            || HasContextValue(currentCheckIn, "bleedingLevel", "Heavy")
            || HasContextValue(currentCheckIn, "babyActivity", "Lethargic")
            || currentCheckIn.PainLevel >= 9
            || currentCheckIn.BabyFeeding.Equals("RefusesFeeding", StringComparison.OrdinalIgnoreCase)
            || riskScore >= 55)
        {
            return "Red";
        }

        if (currentCheckIn.PainLevel >= 8
            || riskScore >= 25
            || recentHistory.Take(3).Count(x => x.SleepHours < 5) >= 3)
        {
            return "Yellow";
        }

        return "Green";
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
            "Emergency" => $"Điểm rủi ro hiện tại là {riskScore}/100, thuộc mức đỏ khẩn cấp. Nên liên hệ cấp cứu hoặc cơ sở y tế ngay, đặc biệt khi triệu chứng đang tăng nhanh.{reason}",
            "Red" => $"Điểm rủi ro hiện tại là {riskScore}/100, thuộc mức đỏ. Cần ưu tiên liên hệ bác sĩ hoặc cơ sở y tế trong thời gian sớm.{reason}",
            "Yellow" => $"Điểm rủi ro hiện tại là {riskScore}/100, thuộc mức vàng. Tình trạng cần theo dõi thêm trong 24-48 giờ tới.{reason}",
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
        var postpartumDay = GetContextNumber(currentCheckIn, "postpartumDay");

        if (postpartumDay is > 0 and <= 7)
        {
            recommendations.Add("Tuần đầu sau sinh: theo dõi sản dịch, vết mổ/khâu và sữa non. Ưu tiên nghỉ ngơi tối đa.");
        }
        else if (postpartumDay is > 7 and <= 14)
        {
            recommendations.Add("Tuần thứ 2: sữa đang chuyển sang sữa chính. Nếu căng sữa hoặc đau, nên nhờ hỗ trợ chuyên môn.");
        }
        else if (postpartumDay is > 14 and <= 42)
        {
            recommendations.Add("Giai đoạn 2-6 tuần: theo dõi tâm trạng và giấc ngủ để phát hiện baby blues hoặc kiệt sức sớm.");
        }

        if (warningLevel is "Emergency" or "Red")
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

        if (warningLevel is "Emergency" or "Red")
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
        var repeatedFeedingConcern = HasActiveRepeatedFeedingConcern(currentCheckIn, recentHistory);
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

    private static string BuildUrgencyAction(string warningLevel)
    {
        return warningLevel switch
        {
            "Emergency" => "Gọi cấp cứu 115 hoặc đến cơ sở y tế gần nhất ngay.",
            "Red" => "Liên hệ bác sĩ hoặc cơ sở y tế trong ngày hôm nay.",
            "Yellow" => "Theo dõi sát trong 24-48 giờ và check-in lại nếu triệu chứng tăng.",
            _ => "Tiếp tục check-in hằng ngày và duy trì chăm sóc cơ bản."
        };
    }

    private static string BuildWeeklySummary(HealthCheckIn currentCheckIn, List<HealthCheckIn> recentHistory)
    {
        var week = recentHistory
            .Where(x => x.CreatedAt >= DateTime.UtcNow.AddDays(-7))
            .OrderBy(x => x.CreatedAt)
            .ToList();

        if (week.Count < 2)
        {
            return "Chưa đủ dữ liệu trong 7 ngày để tổng hợp tuần. Hãy tiếp tục check-in để CareMate Engine nhận diện xu hướng rõ hơn.";
        }

        var avgSleep = week.Average(x => x.SleepHours);
        var avgPain = week.Average(x => x.PainLevel);
        var stressDays = week.Count(x => IsStressMood(x.Mood));
        var feedingConcernDays = week.Count(x => IsFeedingConcern(x.BabyFeeding));
        var newest = week.Last();
        var oldest = week.First();
        var painChange = newest.PainLevel - oldest.PainLevel;
        var sleepChange = newest.SleepHours - oldest.SleepHours;

        var trend = painChange >= 2
            ? "mức đau có xu hướng tăng"
            : painChange <= -2
                ? "mức đau có xu hướng giảm"
                : "mức đau khá ổn định";

        var sleepTrend = sleepChange <= -1
            ? "giấc ngủ giảm so với đầu tuần"
            : sleepChange >= 1
                ? "giấc ngủ cải thiện so với đầu tuần"
                : "giấc ngủ chưa thay đổi nhiều";

        return $"Trong 7 ngày gần đây, mẹ ngủ trung bình {avgSleep:0.0} giờ, đau trung bình {avgPain:0.0}/10; {trend}, {sleepTrend}. Có {stressDays} ngày tâm trạng căng thẳng và {feedingConcernDays} ngày bé bú bất thường.";
    }

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

    private static bool HasActiveRepeatedFeedingConcern(HealthCheckIn currentCheckIn, List<HealthCheckIn> recentHistory)
    {
        if (!IsFeedingConcern(currentCheckIn.BabyFeeding))
        {
            return false;
        }

        return recentHistory
            .OrderByDescending(x => x.CreatedAt)
            .Take(3)
            .Count(x => IsFeedingConcern(x.BabyFeeding)) >= 2;
    }

    private static bool IsBabySleepConcern(string babySleep)
    {
        return babySleep.Equals("CryingOften", StringComparison.OrdinalIgnoreCase)
            || babySleep.Equals("WakingFrequently", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDangerKeyword(string? note)
    {
        return VietnameseTextHelper.ContainsAny(note, DangerousKeywords);
    }

    private static bool HasEmergencySymptom(HealthCheckIn checkIn)
    {
        return HasSymptom(checkIn, "khó thở", "kho tho", "đau ngực", "dau nguc", "ngất", "ngat", "co giật", "co giat")
            || HasSymptom(checkIn, "chảy máu nhiều", "chay mau nhieu")
            || (checkIn.TemperatureCelsius >= 39.5);
    }

    private static bool HasRedFlagSymptom(HealthCheckIn checkIn)
    {
        return HasSymptom(checkIn, "sốt", "sot", "mờ mắt", "mo mat", "chóng mặt", "chong mat", "vết mổ chảy dịch", "vet mo chay dich", "sưng đỏ", "sung do")
            || HasSymptom(checkIn, "đau đầu dữ dội", "dau dau du doi", "ra máu bất thường", "ra mau bat thuong");
    }

    private static bool HasChestPainBreathingCombo(HealthCheckIn checkIn)
    {
        var hasChestPain = ContainsAny(checkIn.PainLocation, "ngực", "nguc")
            || HasSymptom(checkIn, "đau ngực", "dau nguc");
        var hasBreathing = HasSymptom(checkIn, "khó thở", "kho tho");
        return hasChestPain && hasBreathing;
    }

    private static bool IsWorseningPain(HealthCheckIn checkIn)
    {
        return checkIn.PainTrend?.Equals("Worse", StringComparison.OrdinalIgnoreCase) == true
            || checkIn.PainTrend?.Contains("tăng", StringComparison.OrdinalIgnoreCase) == true
            || checkIn.PainTrend?.Contains("tang", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool HasMedicalHistory(HealthCheckIn checkIn, params string[] keywords)
    {
        return DeserializeStringList(checkIn.MedicalHistoryJson)
            .Any(item => VietnameseTextHelper.ContainsAny(item, keywords));
    }

    private static bool HasContextValue(HealthCheckIn checkIn, string key, params string[] expectedValues)
    {
        var context = DeserializeStringDictionary(checkIn.ContextDataJson);
        return context.TryGetValue(key, out var value)
            && expectedValues.Any(expected => value.Equals(expected, StringComparison.OrdinalIgnoreCase));
    }

    private static double? GetContextNumber(HealthCheckIn checkIn, string key)
    {
        var context = DeserializeStringDictionary(checkIn.ContextDataJson);
        return context.TryGetValue(key, out var value) && double.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool HasSymptom(HealthCheckIn checkIn, params string[] keywords)
    {
        return DeserializeStringList(checkIn.SymptomsJson)
            .Any(item => VietnameseTextHelper.ContainsAny(item, keywords));
    }

    private static bool ContainsAny(string? value, params string[] keywords)
    {
        return VietnameseTextHelper.ContainsAny(value, keywords);
    }

    private static List<string> DeserializeStringList(string json)
    {
        return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
    }

    private static Dictionary<string, string> DeserializeStringDictionary(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? [];
    }

    private static bool IsDeterioration(List<HealthCheckIn> history, Func<HealthCheckIn, double> selector, int minConsecutive = 3)
    {
        var ordered = history.OrderBy(x => x.CreatedAt).ToList();
        if (ordered.Count < minConsecutive)
        {
            return false;
        }

        var recent = ordered.TakeLast(minConsecutive).Select(selector).ToList();
        return recent.Zip(recent.Skip(1)).All(pair => pair.Second >= pair.First);
    }

    private static (int Score, string Label) CalculateConfidence(HealthCheckIn checkIn, List<HealthCheckIn> history)
    {
        var points = 0;
        points += Math.Min(25, history.Count * 5);

        if (checkIn.SystolicBloodPressure.HasValue) points += 10;
        if (checkIn.TemperatureCelsius.HasValue) points += 10;
        if (checkIn.MotherAge.HasValue) points += 5;

        var context = DeserializeStringDictionary(checkIn.ContextDataJson);
        points += Math.Min(20, context.Count * 4);

        if (!string.IsNullOrWhiteSpace(checkIn.Note)) points += 5;

        var symptoms = DeserializeStringList(checkIn.SymptomsJson);
        points += Math.Min(15, symptoms.Count * 3);

        var medicalHistory = DeserializeStringList(checkIn.MedicalHistoryJson);
        if (medicalHistory.Count > 0) points += 10;

        var score = Math.Min(95, points);
        var label = score switch
        {
            >= 70 => "Cao",
            >= 40 => "Trung bình",
            _ => "Thấp - nhập thêm chỉ số để phân tích chính xác hơn"
        };

        return (score, label);
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
