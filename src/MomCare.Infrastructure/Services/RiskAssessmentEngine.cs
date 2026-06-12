using System.Globalization;
using System.Text;
using System.Text.Json;
using MomCare.Dto;
using MomCare.Models;

namespace MomCare.Services;

public static class RiskAssessmentEngine
{
    public const string EngineVersion = "rule-v3.0";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<string, double> CategoryWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VitalSigns"] = 1.3,
        ["Pain"] = 1.0,
        ["Baby"] = 1.2,
        ["Mental"] = 0.9,
        ["Wound"] = 1.1,
        ["Feeding"] = 1.0,
        ["Bleeding"] = 1.4,
        ["Medication"] = 0.7,
        ["General"] = 1.0
    };
    private static readonly string[] DangerousKeywords =
    [
        "sốt cao", "khó thở", "chảy máu nhiều", "đau dữ dội", "vết mổ sưng đỏ", "vết mổ chảy dịch"
    ];
    public static HealthAnalysisResult Analyze(
        HealthCheckIn currentCheckIn,
        List<HealthCheckIn> recentHistory,
        IReadOnlyList<SuggestedServiceDto> availableServices)
    {
        var historyBeforeCurrent = recentHistory
            .Where(x => x.Id != currentCheckIn.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
        var factors = BuildRiskFactors(currentCheckIn, historyBeforeCurrent);
        var previousScore = historyBeforeCurrent.Count > 0
            ? CalculateWeightedRiskScore(BuildRiskFactors(historyBeforeCurrent[0], historyBeforeCurrent.Skip(1).ToList()), GetPostpartumDay(historyBeforeCurrent[0]))
            : 0;
        var riskScore = CalibrateRiskScore(
            CalculateWeightedRiskScore(factors, GetPostpartumDay(currentCheckIn)),
            currentCheckIn);
        if (previousScore > 0 && riskScore - previousScore >= 30)
        {
            AddFactorIf(factors, true, "rapid_deterioration", "Điểm rủi ro tăng nhanh so với lần check-in trước", 12, "VitalSigns");
            riskScore = CalibrateRiskScore(
                CalculateWeightedRiskScore(factors, GetPostpartumDay(currentCheckIn)),
                currentCheckIn);
        }

        var warningLevel = DetermineWarningLevel(riskScore, currentCheckIn, recentHistory);
        var trendSignals = BuildTrendSignals(recentHistory);
        var confidence = CalculateConfidence(currentCheckIn, recentHistory);
        var coverage = CalculateDataCoverage(currentCheckIn);
        var ppd = ScreenPostpartumDepression(currentCheckIn, recentHistory);
        var nutrition = BuildNutritionGuidance(currentCheckIn, recentHistory);
        var recommendations = BuildRecommendations(currentCheckIn, recentHistory, factors, warningLevel);
        var carePlan = BuildCarePlan(warningLevel, currentCheckIn, recentHistory);

        var result = new HealthAnalysisResult
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
            Recommendations = recommendations,
            CarePlan = carePlan,
            SuggestedServices = BuildSuggestedServices(currentCheckIn, recentHistory, availableServices),
            PpdScreeningScore = ppd.Score,
            PpdScreeningLevel = ppd.Level,
            PpdScreeningNote = ppd.Note,
            NutritionGuidance = nutrition,
            DataCoveragePercent = coverage.Percent,
            DataCoverageItems = coverage.FilledItems,
            MissingDataItems = coverage.MissingItems,
            FollowUpQuestions = BuildFollowUpQuestions(coverage.MissingItems)
        };

        result.NarrativeSummary = NarrativeSummaryBuilder.Build(result, currentCheckIn, recentHistory);
        return result;
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
        AddFactorIf(factors, currentCheckIn.PainLevel >= 9, "severe_pain", "Mức đau rất cao", 35);
        AddFactorIf(factors, currentCheckIn.PainLevel == 8, "high_pain", "Mức đau cao", 28);
        AddFactorIf(factors, currentCheckIn.PainLevel is >= 6 and <= 7, "elevated_pain", "Mức đau cần theo dõi", 16);
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
        AddFactorIf(factors, HasActiveRepeatedFeedingConcern(currentCheckIn, recentHistory), "repeated_feeding_concern", "Tình trạng bú của bé bất thường lặp lại trong các lần gần đây", 16);
        AddFactorIf(factors, IsPainIncreasing(recentHistory), "pain_increasing", "Mức đau có xu hướng tăng", 15);
        AddFactorIf(factors, IsDeterioration(recentHistory, x => x.PainLevel, 3), "pain_deterioration", "Mức đau tăng liên tục 3 lần check-in gần đây", 30);

        var postpartumDay = GetPostpartumDay(currentCheckIn);
        AddFactorIf(factors, HasContextValue(currentCheckIn, "swellingLevel", "Severe") && ContainsAny(currentCheckIn.PainLocation, "chân", "chan", "bắp chân", "bap chan") && postpartumDay is <= 21, "dvt_signs", "Phù chân nặng kèm đau bắp chân trong giai đoạn sớm sau sinh", 70, "VitalSigns");
        AddFactorIf(factors, ContainsAny(currentCheckIn.PainLocation, "ngực/sữa", "nguc", "sữa", "sua", "vú", "vu") && currentCheckIn.TemperatureCelsius >= 38 && (HasContextValue(currentCheckIn, "incisionStatus", "RedSwollen") || HasSymptom(currentCheckIn, "sưng đỏ", "sung do")), "mastitis_signs", "Đau vùng ngực/sữa kèm sốt và sưng đỏ, nghi viêm vú", 55, "Feeding");
        AddFactorIf(factors, HasContextValue(currentCheckIn, "bleedingLevel", "Heavy") && HasSymptom(currentCheckIn, "chóng mặt", "chong mat") && postpartumDay is >= 1 and <= 42, "late_pph_signs", "Ra máu nhiều kèm chóng mặt trong 42 ngày sau sinh", 75, "Bleeding");
        AddFactorIf(factors, HasContextValue(currentCheckIn, "babyActivity", "Lethargic") && IsFeedingConcern(currentCheckIn.BabyFeeding) && postpartumDay is <= 14, "neonatal_jaundice_signs", "Bé lừ đừ và bú ít trong 14 ngày đầu, cần sàng lọc vàng da sơ sinh", 60, "Baby");
        AddFactorIf(factors, HasContextValue(currentCheckIn, "urinationIssue", "true") && currentCheckIn.TemperatureCelsius >= 38 && ContainsAny(currentCheckIn.PainLocation, "bụng dưới", "bung duoi"), "uti_signs", "Khó tiểu kèm sốt và đau bụng dưới, nghi nhiễm trùng tiết niệu", 45, "VitalSigns");
        AddFactorIf(factors, (currentCheckIn.SystolicBloodPressure >= 140 || currentCheckIn.DiastolicBloodPressure >= 90) && ContainsAny(currentCheckIn.PainLocation, "bụng trên", "bung tren") && HasSymptom(currentCheckIn, "buồn nôn", "buon non"), "late_hellp_signs", "Huyết áp cao kèm đau bụng trên và buồn nôn, cần loại trừ HELLP muộn", 85, "VitalSigns");
        AddFactorIf(factors, HasSymptom(currentCheckIn, "chóng mặt", "chong mat", "mệt", "met") && ContainsAny(currentCheckIn.Note, "tim nhanh", "mạch nhanh", "mach nhanh", "hồi hộp", "hoi hop"), "severe_anemia_signs", "Chóng mặt, mệt và hồi hộp có thể gợi ý thiếu máu cần theo dõi", 40, "VitalSigns");
        AddFactorIf(factors, currentCheckIn.TemperatureCelsius >= 38 && ContainsAny(currentCheckIn.Note, "tim nhanh", "mạch nhanh", "mach nhanh", "hồi hộp", "hoi hop"), "tachycardia_fever", "Sốt kèm dấu hiệu nhịp tim nhanh cần theo dõi nhiễm trùng nặng", 50, "VitalSigns");
        AddFactorIf(
            factors,
            HasMedicationPlan(currentCheckIn) &&
                currentCheckIn.TookMedicationToday == false &&
                recentHistory.Take(5).Count(x => HasMedicationPlan(x) && x.TookMedicationToday == false) >= 2,
            "repeated_medication_skip",
            "Không dùng thuốc theo dặn dò lặp lại nhiều lần gần đây",
            15,
            "Medication");
        AddFactorIf(factors, ContainsAny(currentCheckIn.Note, "ban đêm", "ban dem", "về đêm", "ve dem"), "night_only_symptoms", "Triệu chứng có xu hướng xuất hiện về đêm", 10, "General");
        AddFactorIf(factors, postpartumDay is >= 0 and <= 7 && factors.Any(x => x.Points >= 55), "first_week_critical", "Tuần đầu sau sinh có dấu hiệu cảnh báo cần nhạy hơn", 15, "General");
        AddFactorIf(factors, CountPainLocations(currentCheckIn) >= 3, "multiple_pain_sites", "Có từ 3 vùng đau trở lên cùng lúc", 12, "Pain");

        var categoryCount = factors
            .Select(x => x.Category)
            .Where(x => !string.IsNullOrWhiteSpace(x) && x != "General")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        AddFactorIf(factors, categoryCount >= 2, "category_synergy_2", "Có từ 2 nhóm cần theo dõi cùng xuất hiện", 8, "General");
        AddFactorIf(factors, categoryCount >= 3, "category_synergy_3", "Có từ 3 nhóm cần theo dõi cùng xuất hiện", 10, "General");
        AddFactorIf(factors, categoryCount >= 4, "category_synergy_4", "Có nhiều nhóm dấu hiệu cùng xuất hiện", 12, "General");

        return factors
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    private static void AddFactorIf(List<RiskFactorDto> factors, bool condition, string code, string label, int points, string? category = null)
    {
        if (condition)
        {
            factors.Add(new RiskFactorDto { Code = code, Label = label, Points = points, Category = category ?? InferCategory(code) });
        }
    }

    private static string DetermineWarningLevel(int riskScore, HealthCheckIn currentCheckIn, List<HealthCheckIn> recentHistory)
    {
        if (HasEmergencyCondition(currentCheckIn))
        {
            return "Emergency";
        }

        if (HasStrongRedFlag(currentCheckIn))
        {
            return "Red";
        }

        var thresholds = GetStageThresholds(GetPostpartumDay(currentCheckIn));
        if (currentCheckIn.PainLevel >= 8
            || riskScore >= thresholds.Yellow
            || recentHistory.Take(3).Count(x => x.SleepHours < 5) >= 3)
        {
            return "Yellow";
        }

        return "Green";
    }

    private static int CalibrateRiskScore(int rawScore, HealthCheckIn checkIn)
    {
        if (HasEmergencyCondition(checkIn))
        {
            return Math.Clamp(rawScore, 85, 100);
        }

        if (HasStrongRedFlag(checkIn))
        {
            return Math.Clamp(rawScore, 55, 84);
        }

        if (HasModerateConcern(checkIn))
        {
            return Math.Clamp(rawScore, 25, 60);
        }

        return Math.Clamp(rawScore, 0, 45);
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
            "Emergency" => $"Điểm rủi ro hiện tại là {riskScore}/100, thuộc mức đỏ khẩn cấp vì có dấu hiệu cần xử lý ngay. Nên liên hệ cấp cứu hoặc cơ sở y tế gần nhất.{reason}",
            "Red" => $"Điểm rủi ro hiện tại là {riskScore}/100, thuộc mức đỏ. Nên liên hệ bác sĩ hoặc cơ sở y tế trong ngày để được hướng dẫn cụ thể.{reason}",
            "Yellow" => $"Điểm rủi ro hiện tại là {riskScore}/100, thuộc mức cần theo dõi. Chưa ghi nhận dấu hiệu khẩn cấp rõ, nhưng mẹ nên quan sát sát trong 24-48 giờ tới.{reason}",
            _ => $"Điểm rủi ro hiện tại là {riskScore}/100, thuộc mức thấp. Tình trạng tương đối ổn, tiếp tục check-in hằng ngày để phát hiện thay đổi sớm.{reason}"
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

    private static bool HasMedicationPlan(HealthCheckIn checkIn)
    {
        return !string.IsNullOrWhiteSpace(checkIn.MedicationNote)
            || checkIn.TookMedicationToday;
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

    private static bool HasEmergencyCondition(HealthCheckIn checkIn)
    {
        return HasEmergencySymptom(checkIn)
            || HasChestPainBreathingCombo(checkIn)
            || checkIn.SystolicBloodPressure >= 180
            || checkIn.DiastolicBloodPressure >= 120;
    }

    private static bool HasStrongRedFlag(HealthCheckIn checkIn)
    {
        return HasRedFlagSymptom(checkIn)
            || HasContextValue(checkIn, "bleedingLevel", "Heavy")
            || HasContextValue(checkIn, "babyActivity", "Lethargic")
            || checkIn.PainLevel >= 9
            || checkIn.BabyFeeding.Equals("RefusesFeeding", StringComparison.OrdinalIgnoreCase)
            || checkIn.SystolicBloodPressure >= 160
            || checkIn.DiastolicBloodPressure >= 110
            || checkIn.TemperatureCelsius >= 38.5;
    }

    private static bool HasModerateConcern(HealthCheckIn checkIn)
    {
        return checkIn.PainLevel >= 6
            || IsLowMilk(checkIn.MilkStatus)
            || IsFeedingConcern(checkIn.BabyFeeding)
            || IsStressMood(checkIn.Mood)
            || checkIn.SleepHours < 5
            || HasContextValue(checkIn, "incisionStatus", "RedSwollen", "Discharge");
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

    private static int CalculateWeightedRiskScore(List<RiskFactorDto> factors, double? postpartumDay)
    {
        var weighted = factors.Sum(factor =>
        {
            var category = string.IsNullOrWhiteSpace(factor.Category) ? InferCategory(factor.Code) : factor.Category;
            var weight = CategoryWeights.TryGetValue(category, out var value) ? value : 1.0;
            return factor.Points * weight;
        });

        if (postpartumDay is >= 0 and <= 7)
        {
            weighted *= 1.15;
        }
        else if (postpartumDay is >= 15 and <= 42)
        {
            weighted *= 0.9;
        }

        return Math.Clamp((int)Math.Round(weighted), 0, 100);
    }

    private static (int Yellow, int Red, int Emergency) GetStageThresholds(double? postpartumDay)
    {
        var multiplier = postpartumDay switch
        {
            >= 0 and <= 7 => 0.85,
            >= 15 and <= 42 => 1.1,
            _ => 1.0
        };

        return ((int)Math.Round(25 * multiplier), (int)Math.Round(55 * multiplier), (int)Math.Round(85 * multiplier));
    }

    private static string InferCategory(string code)
    {
        if (code.Contains("baby", StringComparison.OrdinalIgnoreCase) || code.Contains("jaundice", StringComparison.OrdinalIgnoreCase)) return "Baby";
        if (code.Contains("sleep", StringComparison.OrdinalIgnoreCase) || code.Contains("stress", StringComparison.OrdinalIgnoreCase)) return "Mental";
        if (code.Contains("pain", StringComparison.OrdinalIgnoreCase)) return "Pain";
        if (code.Contains("wound", StringComparison.OrdinalIgnoreCase) || code.Contains("incision", StringComparison.OrdinalIgnoreCase)) return "Wound";
        if (code.Contains("milk", StringComparison.OrdinalIgnoreCase) || code.Contains("feeding", StringComparison.OrdinalIgnoreCase) || code.Contains("mastitis", StringComparison.OrdinalIgnoreCase)) return "Feeding";
        if (code.Contains("bleeding", StringComparison.OrdinalIgnoreCase) || code.Contains("pph", StringComparison.OrdinalIgnoreCase)) return "Bleeding";
        if (code.Contains("medication", StringComparison.OrdinalIgnoreCase)) return "Medication";
        if (code.Contains("bp", StringComparison.OrdinalIgnoreCase) || code.Contains("temperature", StringComparison.OrdinalIgnoreCase) || code.Contains("fever", StringComparison.OrdinalIgnoreCase) || code.Contains("hellp", StringComparison.OrdinalIgnoreCase) || code.Contains("dvt", StringComparison.OrdinalIgnoreCase)) return "VitalSigns";
        return "General";
    }

    private static double? GetPostpartumDay(HealthCheckIn checkIn) => GetContextNumber(checkIn, "postpartumDay");

    private static int CountPainLocations(HealthCheckIn checkIn)
    {
        return (checkIn.PainLocation ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count();
    }

    private static (int Score, string Level, string Note) ScreenPostpartumDepression(HealthCheckIn currentCheckIn, List<HealthCheckIn> recentHistory)
    {
        var recent = recentHistory
            .Where(x => x.Id != currentCheckIn.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(6)
            .Prepend(currentCheckIn)
            .ToList();

        var score = Math.Min(15, recent.Count(x => IsStressMood(x.Mood)) * 5);
        if (recent.Take(3).Count(x => x.SleepHours < 4) >= 3) score += 4;
        else if (recent.Take(5).Count(x => x.SleepHours < 5) >= 5) score += 3;
        if (recent.Take(3).Count(x => IsFeedingConcern(x.BabyFeeding)) >= 2) score += 2;
        if (IsPainIncreasing(recent) || IsWorseningPain(currentCheckIn)) score += 2;
        if (HasMedicationPlan(currentCheckIn) && currentCheckIn.TookMedicationToday == false && recent.Take(3).Count(x => HasMedicationPlan(x) && x.TookMedicationToday == false) >= 2) score += 2;
        if (recent.Take(3).Count(x => IsLowMilk(x.MilkStatus)) >= 2) score += 1;

        var keywordHits = new[] { "buồn", "buon", "khóc", "khoc", "không muốn", "khong muon", "mệt mỏi", "met moi", "cô đơn", "co don", "sợ", "so" }
            .Count(keyword => VietnameseTextHelper.ContainsAny(currentCheckIn.Note, keyword));
        score += Math.Min(6, keywordHits * 3);
        score = Math.Clamp(score, 0, 30);

        return score switch
        {
            >= 16 => (score, "High", "Khuyến nghị tư vấn chuyên gia tâm lý hoặc bác sĩ nếu cảm giác buồn, sợ hãi, quá tải kéo dài."),
            >= 9 => (score, "Moderate", "Cần theo dõi tâm lý sát hơn và nên có người hỗ trợ chăm mẹ, chăm bé trong vài ngày tới."),
            _ => (score, "Low", "Tâm lý hiện tương đối ổn định, tiếp tục quan sát giấc ngủ và mức căng thẳng hằng ngày.")
        };
    }

    private static List<NutritionTipDto> BuildNutritionGuidance(HealthCheckIn currentCheckIn, List<HealthCheckIn> recentHistory)
    {
        var tips = new List<NutritionTipDto>();
        var postpartumDay = GetPostpartumDay(currentCheckIn);

        if (postpartumDay is null or <= 7)
        {
            tips.Add(new() { Category = "Phục hồi", Tip = "Ưu tiên protein, sắt và 2-2.5L nước mỗi ngày.", Reason = "Tuần đầu cần phục hồi mô, bù dịch và hỗ trợ tạo sữa.", Icon = "💪" });
        }
        else if (postpartumDay is <= 14)
        {
            tips.Add(new() { Category = "Sữa chính", Tip = "Tăng omega-3, calcium khoảng 1000mg/ngày và thực phẩm lợi sữa.", Reason = "Giai đoạn sữa chuyển ổn định cần thêm vi chất và năng lượng.", Icon = "🥛" });
        }
        else if (postpartumDay is <= 42)
        {
            tips.Add(new() { Category = "Phục hồi dài hơn", Tip = "Ăn cân bằng, bổ sung vitamin D và bắt đầu vận động nhẹ nếu bác sĩ cho phép.", Reason = "Cơ thể đang ổn định dần nhưng vẫn cần nền dinh dưỡng đều.", Icon = "🌿" });
        }

        if (IsLowMilk(currentCheckIn.MilkStatus) || IsFeedingConcern(currentCheckIn.BabyFeeding))
        {
            tips.Add(new() { Category = "Cho bú", Tip = "Thêm rau ngót, đu đủ xanh, hạt, cá và uống nước đều trong ngày.", Reason = "Có thể hỗ trợ năng lượng và nguồn chất lỏng cho quá trình tạo sữa.", Icon = "🍼" });
        }

        if (currentCheckIn.SleepHours < 5 || recentHistory.Take(3).Count(x => x.SleepHours < 5) >= 2)
        {
            tips.Add(new() { Category = "Giấc ngủ", Tip = "Dùng bữa nhẹ giàu tryptophan hoặc magnesium như sữa ấm, chuối, hạt.", Reason = "Dinh dưỡng nhẹ buổi tối có thể hỗ trợ thư giãn.", Icon = "🌙" });
        }

        if (IsStressMood(currentCheckIn.Mood))
        {
            tips.Add(new() { Category = "Tâm trạng", Tip = "Bổ sung omega-3, B6, folate từ cá, trứng, rau xanh đậm và đậu.", Reason = "Các vi chất này liên quan tới năng lượng và ổn định tâm trạng.", Icon = "🧠" });
        }

        if (currentCheckIn.PainLevel >= 6 || IsWorseningPain(currentCheckIn))
        {
            tips.Add(new() { Category = "Kháng viêm", Tip = "Tăng cá béo, gừng, nghệ và rau lá xanh trong bữa ăn.", Reason = "Nhóm thực phẩm này hỗ trợ nền dinh dưỡng chống viêm tự nhiên.", Icon = "🔥" });
        }

        return tips
            .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .Take(6)
            .ToList();
    }

    private static (int Percent, List<string> FilledItems, List<string> MissingItems) CalculateDataCoverage(HealthCheckIn checkIn)
    {
        var context = DeserializeStringDictionary(checkIn.ContextDataJson);
        var checks = new List<(string Label, bool Filled)>
        {
            ("Giấc ngủ", true),
            ("Mức đau", checkIn.PainLevel > 0),
            ("Vị trí đau", !string.IsNullOrWhiteSpace(checkIn.PainLocation)),
            ("Kiểu đau", !string.IsNullOrWhiteSpace(checkIn.PainType)),
            ("Diễn tiến đau", !string.IsNullOrWhiteSpace(checkIn.PainTrend)),
            ("Triệu chứng", DeserializeStringList(checkIn.SymptomsJson).Count > 0),
            ("Tiền sử", DeserializeStringList(checkIn.MedicalHistoryJson).Count > 0),
            ("Ngày sau sinh", context.ContainsKey("postpartumDay")),
            ("Kiểu sinh", context.ContainsKey("deliveryMethod")),
            ("Sản dịch", context.ContainsKey("bleedingLevel")),
            ("Vết mổ/khâu", context.ContainsKey("incisionStatus")),
            ("Phù chân", context.ContainsKey("swellingLevel")),
            ("Khó tiểu", context.ContainsKey("urinationIssue")),
            ("Tã ướt của bé", context.ContainsKey("babyWetDiapers")),
            ("Hoạt động của bé", context.ContainsKey("babyActivity")),
            ("Tuổi mẹ", checkIn.MotherAge.HasValue),
            ("Huyết áp", checkIn.SystolicBloodPressure.HasValue && checkIn.DiastolicBloodPressure.HasValue),
            ("Nhiệt độ", checkIn.TemperatureCelsius.HasValue),
            ("Tình trạng thuốc", true),
            ("Ghi chú", !string.IsNullOrWhiteSpace(checkIn.Note))
        };

        var filled = checks.Where(x => x.Filled).Select(x => x.Label).ToList();
        var missing = checks.Where(x => !x.Filled).Select(x => x.Label).ToList();
        var percent = (int)Math.Round(filled.Count * 100.0 / checks.Count);
        return (percent, filled, missing);
    }

    private static List<FollowUpQuestionDto> BuildFollowUpQuestions(List<string> missingItems)
    {
        var questions = new List<FollowUpQuestionDto>();

        foreach (var item in missingItems.Take(6))
        {
            questions.Add(item switch
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
                _ => new FollowUpQuestionDto { Key = ToCamelKey(item), QuestionVi = $"Bổ sung thông tin: {item}", InputType = "text" }
            });
        }

        return questions;
    }

    private static string ToCamelKey(string value)
    {
        var words = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(VietnameseTextHelper.RemoveDiacritics)
            .Select(word => new string(word.Where(char.IsLetterOrDigit).ToArray()))
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .ToList();

        if (words.Count == 0) return "missingData";
        return string.Concat(words.Select((word, index) =>
            index == 0
                ? char.ToLowerInvariant(word[0]) + word[1..]
                : char.ToUpperInvariant(word[0]) + word[1..]));
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
