using System.Globalization;
using System.Text;
using System.Text.Json;
using MomCare.Dto;
using MomCare.Models;

namespace MomCare.Services;

/// <summary>
/// Extracts normalized symptom tags from a health check-in for safe AI prompting.
/// </summary>
public class SymptomTagEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Extracts postpartum stage, delivery type, care needs, and a non-diagnostic risk score.
    /// </summary>
    public SymptomTagResult Extract(HealthCheckIn? checkIn)
    {
        var result = new SymptomTagResult();
        if (checkIn is null)
        {
            return result;
        }

        var context = ReadContext(checkIn.ContextDataJson);
        var deliveryMethod = GetContext(context, "deliveryMethod");
        var deliveryNormalized = NormalizeSearchText(deliveryMethod);
        var isCesarean = deliveryNormalized.Contains("mo") ||
            deliveryNormalized.Contains("caesar") ||
            deliveryNormalized.Contains("csection");

        result.DeliveryType = isCesarean ? "cesarean" : "vaginal";
        AddTag(result, isCesarean ? "sinh_mo" : "sinh_thuong");

        var postpartumDay = ParseInt(GetContext(context, "postpartumDay"));
        if (postpartumDay.HasValue)
        {
            result.PostpartumStage = postpartumDay.Value switch
            {
                <= 3 => "early",
                <= 14 => "mid",
                <= 42 => "late",
                _ => "late"
            };
            AddTag(result, $"ngay_hau_san_{Math.Max(postpartumDay.Value, 0)}");
        }

        if (result.PostpartumStage == "early" && result.DeliveryType == "cesarean")
        {
            AddNeed(result, "cham_soc_vet_mo");
            result.OverallRiskScore += 20;
        }

        AddPainTags(checkIn, result);
        AddMilkTags(checkIn, result);
        AddMoodTags(checkIn, result);
        AddBabyFeedingTags(checkIn, result);
        AddBabySleepTags(checkIn, result);
        AddBabyContextTags(context, checkIn, result);
        AddWoundTags(context, result);
        AddVitalSignsTags(checkIn, result);
        AddSymptomsTags(checkIn, result);
        AddNoteTags(checkIn, result);
        AddSleepTags(checkIn, result);
        PopulatePrimaryConcern(checkIn, context, result);
        PopulateRelevantContextTokens(checkIn, context, result);

        result.RawCheckinSummary = BuildRawSummary(checkIn, context);
        result.OverallRiskScore = Math.Clamp(result.OverallRiskScore, 0, 100);
        return result;
    }

    private static void PopulatePrimaryConcern(
        HealthCheckIn checkIn,
        IReadOnlyDictionary<string, string> context,
        SymptomTagResult result)
    {
        var note = NormalizeSearchText(checkIn.Note);
        var woundMentionedInNote = ContainsAny(note, "vet mo", "vet thuong", "ri dich", "chay mau vet mo", "vet khau", "dau vet mo");
        var hasWoundConcern =
            ContainsAny(ToSnakeCase(checkIn.PainLocation), "vet_mo/khau", "vet_mo", "vet_khau") ||
            result.Tags.Any(tag => tag.Contains("vet_mo", StringComparison.OrdinalIgnoreCase)) ||
            result.PrimaryNeeds.Contains("cham_soc_vet_mo", StringComparer.OrdinalIgnoreCase);

        var feverScore = GetFeverPriority(checkIn, note);
        if (hasWoundConcern && feverScore > 0)
        {
            result.PrimaryConcern = feverScore >= 2 || !woundMentionedInNote
                ? "fever_monitoring"
                : "wound_care";
            return;
        }

        var candidates = new List<ConcernCandidate>();

        if (hasWoundConcern)
        {
            candidates.Add(new ConcernCandidate("wound_care", 2, woundMentionedInNote ? 2 : 1));
        }

        if (feverScore > 0)
        {
            candidates.Add(new ConcernCandidate("fever_monitoring", 3, feverScore));
        }

        var bleedingNote = ContainsAny(note, "mau", "chay mau", "ra mau");
        var bleedingLevel = NormalizeSearchText(GetContext(context, "bleedingLevel"));
        if (bleedingNote || ContainsAny(bleedingLevel, "heavy", "nhieu", "bat thuong"))
        {
            candidates.Add(new ConcernCandidate("bleeding_monitoring", 3, bleedingNote ? 3 : 2));
        }

        if (result.PrimaryNeeds.Contains("theo_doi_huyet_ap", StringComparer.OrdinalIgnoreCase))
        {
            var bpMentioned = ContainsAny(note, "huyet ap", "chong mat", "hoa mat");
            candidates.Add(new ConcernCandidate("blood_pressure_monitoring", 3, bpMentioned ? 2 : 1));
        }

        if (result.PrimaryNeeds.Contains("ho_tro_cho_bu", StringComparer.OrdinalIgnoreCase))
        {
            var breastfeedingMentioned = ContainsAny(note, "sua", "cho bu", "tac tia sua", "dau num vu");
            candidates.Add(new ConcernCandidate("breastfeeding_support", 4, breastfeedingMentioned ? 2 : 1));
        }

        if (result.PrimaryNeeds.Contains("tu_van_tam_ly", StringComparer.OrdinalIgnoreCase) ||
            result.PrimaryNeeds.Contains("ho_tro_giao_suc", StringComparer.OrdinalIgnoreCase))
        {
            var moodMentioned = ContainsAny(note, "lo au", "cang thang", "buon", "met", "khong muon an", "kho ngu");
            candidates.Add(new ConcernCandidate("mood_sleep_support", 5, moodMentioned ? 2 : 1));
        }

        result.PrimaryConcern = candidates
            .OrderBy(candidate => candidate.Tier)
            .ThenByDescending(candidate => candidate.Priority)
            .Select(candidate => candidate.Key)
            .FirstOrDefault() ?? "general_postpartum_support";
    }

    private static int GetFeverPriority(HealthCheckIn checkIn, string normalizedNote)
    {
        var noteMentioned = ContainsAny(normalizedNote, "sot", "nong", "fever");
        if (checkIn.TemperatureCelsius is >= 38.5)
        {
            return noteMentioned ? 3 : 2;
        }

        if (checkIn.TemperatureCelsius is > 37.5 || noteMentioned)
        {
            return 1;
        }

        return 0;
    }

    private static void PopulateRelevantContextTokens(
        HealthCheckIn checkIn,
        IReadOnlyDictionary<string, string> context,
        SymptomTagResult result)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sau sinh",
            "hau san"
        };

        var note = NormalizeSearchText(checkIn.Note);
        var mood = NormalizeSearchText(checkIn.Mood);
        var milk = NormalizeSearchText(checkIn.MilkStatus);
        var painLocation = NormalizeSearchText(checkIn.PainLocation);
        var bleedingLevel = NormalizeSearchText(GetContext(context, "bleedingLevel"));
        var incision = NormalizeSearchText(GetContext(context, "incisionStatus"));

        AddTokensIf(tokens, ContainsAny(note, "vet mo", "vet thuong", "ri dich", "chay mau vet mo", "bang", "sung", "do") ||
            ContainsAny(painLocation, "vet mo", "vet khau") ||
            ContainsAny(incision, "painful", "red", "swollen", "discharge", "sung", "do", "chay dich"),
            "vet mo", "vet thuong", "dau", "chay mau", "ri dich", "bang", "sung", "do");

        AddTokensIf(tokens, checkIn.TemperatureCelsius is > 37.5 || ContainsAny(note, "sot", "nong", "fever"),
            "sot", "nhiet do");

        AddTokensIf(tokens, result.PrimaryNeeds.Contains("theo_doi_huyet_ap", StringComparer.OrdinalIgnoreCase) ||
            ContainsAny(note, "huyet ap", "chong mat", "hoa mat"),
            "huyet ap");

        AddTokensIf(tokens, ContainsAny(milk, "it", "painful", "khong co", "chua co") ||
            ContainsAny(note, "sua", "cho bu", "tac tia sua", "dau num vu"),
            "sua", "cho bu", "tac tia sua", "dau num vu");

        AddTokensIf(tokens, ContainsAny(mood, "lo", "cang thang", "buon", "met", "stress", "anxious") ||
            ContainsAny(note, "lo au", "cang thang", "buon", "met", "khong muon an", "kho ngu"),
            "lo au", "cang thang", "buon", "met", "kho ngu", "khong muon an");

        AddTokensIf(tokens, ContainsAny(bleedingLevel, "heavy", "nhieu", "bat thuong") || ContainsAny(note, "chay mau", "ra mau"),
            "chay mau");

        result.RelevantContextTokens = tokens.ToList();
    }

    private static void AddPainTags(HealthCheckIn checkIn, SymptomTagResult result)
    {
        if (checkIn.PainLevel <= 0)
        {
            return;
        }

        var location = ToSnakeCase(checkIn.PainLocation) ?? "khong_ro";
        var severity = checkIn.PainLevel <= 3 ? "nhe" : checkIn.PainLevel <= 6 ? "vua" : "nhieu";
        AddTag(result, $"dau_{location}_{severity}");

        if (checkIn.PainLevel >= 7)
        {
            result.OverallRiskScore += 30;
        }
    }

    private static void AddMilkTags(HealthCheckIn checkIn, SymptomTagResult result)
    {
        var milk = NormalizeSearchText(checkIn.MilkStatus);
        if (milk.Contains("it") || milk.Contains("low") || milk.Contains("chua co") || milk.Contains("khong co") || milk.Contains("painful"))
        {
            AddTag(result, "sua_it");
            AddNeed(result, "ho_tro_cho_bu");
            result.HasBreastfeedingConcern = true;
            return;
        }

        AddTag(result, "sua_du");
    }

    private static void AddMoodTags(HealthCheckIn checkIn, SymptomTagResult result)
    {
        var mood = NormalizeSearchText(checkIn.Mood);
        var negative = new[] { "lo", "buon", "met", "cang thang", "tram", "anxious", "tired", "stress", "overwhelmed" };
        if (negative.Any(mood.Contains))
        {
            AddTag(result, "tam_trang_tieu_cuc");
            AddNeed(result, "tu_van_tam_ly");
            result.OverallRiskScore += 10;
        }
    }

    private static void AddBabyFeedingTags(HealthCheckIn checkIn, SymptomTagResult result)
    {
        var feeding = NormalizeSearchText(checkIn.BabyFeeding);
        if (feeding.Contains("normal") || feeding.Contains("tot") || feeding.Contains("frequent"))
        {
            AddTag(result, "be_bu_tot");
            return;
        }

        AddTag(result, "be_bu_kem");
        result.HasBabyConcern = true;
        result.OverallRiskScore += 15;
    }

    private static void AddBabySleepTags(HealthCheckIn checkIn, SymptomTagResult result)
    {
        var sleep = NormalizeSearchText(checkIn.BabySleep);
        if (sleep.Contains("normal") || sleep.Contains("tot") || sleep.Contains("good"))
        {
            AddTag(result, "be_ngu_tot");
            return;
        }

        AddTag(result, "be_ngu_kem");
        AddNeed(result, "ho_tro_giac_ngu_be");
        result.HasBabyConcern = true;
        result.OverallRiskScore += 10;
    }

    private static void AddBabyContextTags(
        IReadOnlyDictionary<string, string> context,
        HealthCheckIn checkIn,
        SymptomTagResult result)
    {
        var babyActivity = NormalizeSearchText(GetContext(context, "babyActivity"));
        if (babyActivity is "sleepy" or "lethargic" or "irritable")
        {
            AddTag(result, "be_hoat_dong_bat_thuong");
            result.HasBabyConcern = true;
            result.OverallRiskScore += 10;
        }

        var babyWetDiapers = ParseInt(GetContext(context, "babyWetDiapers"));
        if (babyWetDiapers.HasValue && babyWetDiapers.Value > 0 && babyWetDiapers.Value < 6)
        {
            AddTag(result, "be_ta_uot_it");
            result.HasBabyConcern = true;
            result.OverallRiskScore += 10;
        }

        var normalizedNote = NormalizeSearchText(checkIn.Note);
        if (ContainsAny(normalizedNote,
                "be bu it",
                "be bo bu",
                "be tu choi bu",
                "be ngu it",
                "be ngu nhieu bat thuong",
                "be lu du",
                "be quay khoc",
                "be sot",
                "be kho chiu"))
        {
            result.HasBabyConcern = true;
        }
    }

    private static void AddVitalSignsTags(HealthCheckIn checkIn, SymptomTagResult result)
    {
        if (checkIn.TemperatureCelsius is >= 39)
        {
            AddTag(result, "sot_cao");
            AddNeed(result, "theo_doi_sot");
            result.OverallRiskScore += 30;
        }
        else if (checkIn.TemperatureCelsius is > 37.5)
        {
            AddTag(result, "sot_nhe");
            AddNeed(result, "theo_doi_sot");
            result.OverallRiskScore += 20;
        }

        if (checkIn.SystolicBloodPressure is >= 140 || checkIn.DiastolicBloodPressure is >= 90)
        {
            AddTag(result, "huyet_ap_cao");
            AddNeed(result, "theo_doi_huyet_ap");
            result.OverallRiskScore += 20;
        }
        else if (checkIn.SystolicBloodPressure is <= 90 && checkIn.SystolicBloodPressure is not null)
        {
            AddTag(result, "huyet_ap_thap");
            AddNeed(result, "theo_doi_huyet_ap");
            result.OverallRiskScore += 15;
        }
    }

    private static void AddSymptomsTags(HealthCheckIn checkIn, SymptomTagResult result)
    {
        try
        {
            var symptoms = JsonSerializer.Deserialize<List<string>>(checkIn.SymptomsJson, JsonOptions);
            if (symptoms is null) return;

            foreach (var symptom in symptoms)
            {
                var normalized = NormalizeSearchText(symptom);
                if (normalized.Contains("sot") || normalized.Contains("fever") || normalized.Contains("nong"))
                {
                    AddTag(result, "trieu_chung_sot");
                    AddNeed(result, "theo_doi_sot");
                }
                if (normalized.Contains("chong mat") || normalized.Contains("dizzy") || normalized.Contains("hoa mat"))
                {
                    AddTag(result, "trieu_chung_chong_mat");
                    AddNeed(result, "theo_doi_huyet_ap");
                }
                if (normalized.Contains("sua") || normalized.Contains("milk"))
                {
                    AddTag(result, "trieu_chung_sua");
                }
                if (normalized.Contains("bui") || normalized.Contains("bau") || normalized.Contains("bloat"))
                {
                    AddTag(result, "trieu_chung_day_bung");
                    AddNeed(result, "ho_tro_tieu_hoa");
                }
                if (normalized.Contains("ton") || normalized.Contains("constip"))
                {
                    AddTag(result, "trieu_chung_tao_bon");
                    AddNeed(result, "ho_tro_tieu_hoa");
                }
            }
        }
        catch
        {
        }
    }

    private static void AddSleepTags(HealthCheckIn checkIn, SymptomTagResult result)
    {
        if (checkIn.SleepHours < 4)
        {
            AddTag(result, "mat_ngu_nang");
            AddNeed(result, "ho_tro_giao_suc");
            result.OverallRiskScore += 15;
        }
        else if (checkIn.SleepHours < 6)
        {
            AddTag(result, "mat_ngu_nhe");
            AddNeed(result, "ho_tro_giao_suc");
            result.OverallRiskScore += 5;
        }
    }

    private static void AddNoteTags(HealthCheckIn checkIn, SymptomTagResult result)
    {
        var note = NormalizeSearchText(checkIn.Note);
        if (string.IsNullOrWhiteSpace(note)) return;

        var noteKeywords = new Dictionary<string, (string tag, string? need, int risk)>
        {
            ["sot"] = ("note_sot", "theo_doi_sot", 15),
            ["dau"] = ("note_dau", "giam_dau", 10),
            ["met"] = ("note_met_moi", "ho_tro_giao_suc", 5),
            ["chong mat"] = ("note_chong_mat", "theo_doi_huyet_ap", 10),
            ["mau"] = ("note_chay_mau", "theo_doi_chay_mau", 25),
            ["nhieu sua"] = ("note_nhieu_sua", null, 0),
            ["it sua"] = ("note_it_sua", "ho_tro_cho_bu", 5),
        };

        foreach (var kv in noteKeywords)
        {
            if (note.Contains(kv.Key))
            {
                AddTag(result, kv.Value.tag);
                if (kv.Value.need is not null) AddNeed(result, kv.Value.need);
                result.OverallRiskScore += kv.Value.risk;
            }
        }
    }

    private static void AddWoundTags(IReadOnlyDictionary<string, string> context, SymptomTagResult result)
    {
        var incision = NormalizeSearchText(GetContext(context, "incisionStatus"));
        if (incision.Contains("red") || incision.Contains("swollen") || incision.Contains("discharge") ||
            incision.Contains("sung") || incision.Contains("do") || incision.Contains("chay dich") || incision.Contains("painful"))
        {
            AddTag(result, "vet_mo_bat_thuong");
            AddNeed(result, "cham_soc_vet_mo");
            result.OverallRiskScore += 25;
        }
    }

    private static Dictionary<string, string> ReadContext(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string? GetContext(IReadOnlyDictionary<string, string> context, string key) =>
        context.TryGetValue(key, out var value) ? value : null;

    private static int? ParseInt(string? value) => int.TryParse(value, out var parsed) ? parsed : null;

    private static void AddTag(SymptomTagResult result, string tag)
    {
        if (!result.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            result.Tags.Add(tag);
        }
    }

    private static void AddNeed(SymptomTagResult result, string need)
    {
        if (!result.PrimaryNeeds.Contains(need, StringComparer.OrdinalIgnoreCase))
        {
            result.PrimaryNeeds.Add(need);
        }
    }

    private static string? ToSnakeCase(string? value)
    {
        var normalized = NormalizeSearchText(value);
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : string.Join('_', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant()
            .Replace("-", " ")
            .Replace("_", " ");
        var formD = normalized.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool ContainsAny(string? text, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddTokensIf(HashSet<string> tokens, bool condition, params string[] values)
    {
        if (!condition)
        {
            return;
        }

        foreach (var value in values)
        {
            tokens.Add(value);
        }
    }

    private static string BuildRawSummary(HealthCheckIn checkIn, IReadOnlyDictionary<string, string> context)
    {
        var lines = new List<string>();

        if (context.TryGetValue("deliveryMethod", out var delivery))
            lines.Add($"Kieu sinh: {delivery}");
        if (int.TryParse(GetContext(context, "postpartumDay"), out var day))
            lines.Add($"Ngay hau san thu: {day}");
        if (checkIn.PainLevel > 0)
            lines.Add($"Muc do dau: {checkIn.PainLevel}/10, vi tri: {checkIn.PainLocation ?? "Khong ro"}");
        if (checkIn.TemperatureCelsius.HasValue)
            lines.Add($"Nhiet do: {checkIn.TemperatureCelsius:N1}°C");
        if (checkIn.SystolicBloodPressure.HasValue)
            lines.Add($"Huyet ap: {checkIn.SystolicBloodPressure}/{checkIn.DiastolicBloodPressure} mmHg");
        if (checkIn.SleepHours > 0)
            lines.Add($"Ngu: {checkIn.SleepHours}h/ngay");
        lines.Add($"Tam trang: {checkIn.Mood}");
        lines.Add($"Tinh trang sua: {checkIn.MilkStatus}");
        lines.Add($"Be bu: {checkIn.BabyFeeding}");
        lines.Add($"Be ngu: {checkIn.BabySleep}");

        if (context.TryGetValue("incisionStatus", out var incision))
            lines.Add($"Tinh trang vet mo: {incision}");
        if (context.TryGetValue("bleedingLevel", out var bleeding))
            lines.Add($"Muc do ra mau: {bleeding}");
        if (context.TryGetValue("babyActivity", out var activity))
            lines.Add($"Be hoat dong: {activity}");
        if (context.TryGetValue("babyWetDiapers", out var diapers))
            lines.Add($"Be tat uot: {diapers} lan/ngay");
        if (!string.IsNullOrWhiteSpace(checkIn.Note))
            lines.Add($"Ghi chu: {checkIn.Note}");

        return string.Join("\n", lines);
    }

    private sealed record ConcernCandidate(string Key, int Tier, int Priority);
}
