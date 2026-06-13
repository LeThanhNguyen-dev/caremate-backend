using System.Reflection;
using System.Text.Json;
using MomCare.Dto;
using MomCare.Models;
using MomCare.Services;

namespace MomCare.Domain.Tests;

public class HealthCheckInAdaptiveFlowTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Analyze_ReturnsEmergency_ForCriticalBloodPressure()
    {
        var result = RiskAssessmentEngine.Analyze(
            BuildCheckIn(systolicBloodPressure: 182, diastolicBloodPressure: 122),
            [],
            []);

        Assert.Equal("Emergency", result.WarningLevel);
        Assert.True(result.RiskScore >= 85);
    }

    [Fact]
    public void Analyze_ReturnsRed_ForHeavyBleeding()
    {
        var result = RiskAssessmentEngine.Analyze(
            BuildCheckIn(contextData: new Dictionary<string, string> { ["bleedingLevel"] = "Heavy" }),
            [],
            []);

        Assert.Equal("Red", result.WarningLevel);
        Assert.Contains(result.RiskFactors, factor => factor.Code == "heavy_bleeding_context");
    }

    [Fact]
    public void Analyze_BuildsCoverageAndFollowUpQuestions_ForMissingClinicalData()
    {
        var result = RiskAssessmentEngine.Analyze(BuildCheckIn(), [], []);

        Assert.True(result.DataCoveragePercent < 50);
        Assert.Contains("Huyết áp", result.MissingDataItems);
        Assert.Contains(result.FollowUpQuestions, question => question.Key == "painLevel");
    }

    [Fact]
    public void StrictGuardrails_KeepRedResultsPointingToMedicalCare()
    {
        var result = new HealthAnalysisResult
        {
            WarningLevel = "Red",
            UrgencyAction = "Nghỉ ngơi tại nhà.",
            Recommendations = ["Uống nước ấm."]
        };

        var method = typeof(HealthCheckInService).GetMethod(
            "ApplyStrictMedicalGuardrails",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        method.Invoke(null, [result]);

        Assert.Contains("bác sĩ", result.UrgencyAction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Recommendations, item => item.Contains("Không tự dùng thuốc", StringComparison.OrdinalIgnoreCase));
    }

    private static HealthCheckIn BuildCheckIn(
        int? systolicBloodPressure = null,
        int? diastolicBloodPressure = null,
        Dictionary<string, string>? contextData = null)
    {
        return new HealthCheckIn
        {
            Id = Guid.NewGuid(),
            UserId = 1,
            SleepHours = 6,
            PainLevel = 0,
            SymptomsJson = "[]",
            MedicalHistoryJson = "[]",
            ContextDataJson = JsonSerializer.Serialize(contextData ?? [], JsonOptions),
            SystolicBloodPressure = systolicBloodPressure,
            DiastolicBloodPressure = diastolicBloodPressure,
            TookMedicationToday = false,
            Mood = "Tired",
            MilkStatus = "Normal",
            BabyFeeding = "Normal",
            BabySleep = "Normal",
            CreatedAt = DateTime.UtcNow
        };
    }
}
