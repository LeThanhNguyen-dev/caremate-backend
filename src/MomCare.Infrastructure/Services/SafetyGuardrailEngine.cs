using System.Globalization;
using System.Text;
using System.Text.Json;
using MomCare.Dto;
using MomCare.Models;

namespace MomCare.Services;

public static class SafetyGuardrailEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] UrgentKeywords =
    [
        "kho tho", "tho khong duoc", "tuc nguc", "dau nguc",
        "ngat", "xiu", "co giat", "giat nguoi",
        "sot cao", "sot 39", "sot 40",
        "ra mau nhieu", "chay mau nhieu", "bang nhieu",
        "be khong tho", "be tim tai", "be lanh nguoi",
        "huyet ap cao", "160/", "170/", "180/"
    ];

    public static SafetyEvaluationDto Evaluate(HealthCheckIn checkIn)
    {
        var triggers = new List<string>();
        var watchTriggers = new List<string>();
        var symptoms = ReadList(checkIn.SymptomsJson);
        var context = ReadDictionary(checkIn.ContextDataJson);
        var haystack = Normalize(string.Join(" ", symptoms.Concat([checkIn.Note ?? string.Empty, checkIn.PainLocation ?? string.Empty])));

        AddIf(triggers, ContainsAny(haystack, "kho tho", "tuc nguc", "dau nguc"), "Khó thở hoặc đau/tức ngực");
        AddIf(triggers, ContainsAny(haystack, "ngat", "xiu", "co giat"), "Ngất hoặc co giật");
        AddIf(triggers, checkIn.TemperatureCelsius >= 39, "Sốt từ 39°C trở lên");
        AddIf(triggers, checkIn.SystolicBloodPressure >= 160 || checkIn.DiastolicBloodPressure >= 110, "Huyết áp rất cao");
        AddIf(triggers, HasContext(context, "bleedingLevel", "Heavy"), "Ra máu hoặc sản dịch nhiều bất thường");
        AddIf(triggers, HasContext(context, "babyActivity", "Lethargic") && checkIn.BabyFeeding.Equals("RefusesFeeding", StringComparison.OrdinalIgnoreCase), "Bé lừ đừ và bỏ bú");

        AddIf(watchTriggers, checkIn.TemperatureCelsius is >= 38 and < 39, "Sốt 38-39°C");
        AddIf(watchTriggers, checkIn.SystolicBloodPressure is >= 140 and < 160 || checkIn.DiastolicBloodPressure is >= 90 and < 110, "Huyết áp cao cần theo dõi");
        AddIf(watchTriggers, ContainsAny(haystack, "dau dau", "mo mat", "chong mat"), "Đau đầu, mờ mắt hoặc chóng mặt");
        AddIf(watchTriggers, checkIn.SleepHours < 4 || ContainsAny(haystack, "met moi cuc do", "qua tai"), "Mệt mỏi nhiều hoặc ngủ quá ít");
        AddIf(watchTriggers, HasContext(context, "incisionStatus", "Discharge", "RedSwollen"), "Vết mổ/vết khâu có dấu hiệu bất thường");
        AddIf(watchTriggers, HasContext(context, "babyActivity", "Irritable") || ContainsAny(haystack, "khoc lien tuc"), "Bé quấy khóc bất thường");

        if (triggers.Count > 0)
        {
            return new SafetyEvaluationDto
            {
                SafetyLevel = "urgent",
                Triggers = triggers,
                Notice = "Dấu hiệu bạn mô tả cần được đánh giá trực tiếp bởi nhân viên y tế. Hãy liên hệ ngay với bác sĩ, y tá của bạn, hoặc đến cơ sở y tế gần nhất."
            };
        }

        if (watchTriggers.Count >= 2 || watchTriggers.Any(x => x.Contains("Vết mổ", StringComparison.OrdinalIgnoreCase)))
        {
            return new SafetyEvaluationDto
            {
                SafetyLevel = "watch",
                Triggers = watchTriggers,
                Notice = "Có một số dấu hiệu cần theo dõi sát hơn. Hãy báo y tá trong buổi chăm sóc tới hoặc liên hệ cơ sở y tế nếu triệu chứng tăng lên."
            };
        }

        return new SafetyEvaluationDto
        {
            SafetyLevel = "normal",
            Triggers = watchTriggers
        };
    }

    public static SafetyEvaluationDto EvaluateText(string content)
    {
        var normalized = Normalize(content);
        var matched = UrgentKeywords.FirstOrDefault(keyword => normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        if (matched is null)
        {
            return new SafetyEvaluationDto { SafetyLevel = "normal" };
        }

        return new SafetyEvaluationDto
        {
            SafetyLevel = "urgent",
            Triggers = [matched],
            Notice = "Dấu hiệu bạn mô tả cần được đánh giá trực tiếp bởi nhân viên y tế. Hãy liên hệ ngay với bác sĩ, y tá của bạn, hoặc đến cơ sở y tế gần nhất."
        };
    }

    private static bool HasContext(Dictionary<string, string> context, string key, params string[] values)
    {
        return context.TryGetValue(key, out var value) &&
            values.Any(expected => value.Equals(expected, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddIf(List<string> target, bool condition, string label)
    {
        if (condition)
        {
            target.Add(label);
        }
    }

    private static bool ContainsAny(string value, params string[] keywords) =>
        keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static List<string> ReadList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? []; }
        catch { return []; }
    }

    private static Dictionary<string, string> ReadDictionary(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? []; }
        catch { return []; }
    }

    private static string Normalize(string value)
    {
        var formD = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
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
}
