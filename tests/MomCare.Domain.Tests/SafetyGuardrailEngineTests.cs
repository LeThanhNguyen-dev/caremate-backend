using System.Text.Json;
using MomCare.Models;
using MomCare.Services;

namespace MomCare.Domain.Tests;

public class SafetyGuardrailEngineTests
{
    [Fact]
    public void Evaluate_ReturnsNormal_WhenNoSafetyTriggerExists()
    {
        var result = SafetyGuardrailEngine.Evaluate(CreateCheckIn());

        Assert.Equal("normal", result.SafetyLevel);
        Assert.Empty(result.Triggers);
    }

    [Fact]
    public void Evaluate_ReturnsUrgent_ForSevereBloodPressure()
    {
        var result = SafetyGuardrailEngine.Evaluate(CreateCheckIn(systolic: 165, diastolic: 112));

        Assert.Equal("urgent", result.SafetyLevel);
        Assert.Contains(result.Triggers, item => item.Contains("Huyết áp", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(result.Notice);
    }

    [Fact]
    public void Evaluate_ReturnsWatch_ForMultipleWatchTriggers()
    {
        var result = SafetyGuardrailEngine.Evaluate(CreateCheckIn(temperature: 38.3, sleepHours: 3));

        Assert.Equal("watch", result.SafetyLevel);
        Assert.True(result.Triggers.Count >= 2);
    }

    [Theory]
    [InlineData("Mẹ bị khó thở và tức ngực")]
    [InlineData("Bé tím tái và lạnh người")]
    [InlineData("Huyet ap cao 170/110")]
    public void EvaluateText_ReturnsUrgent_ForDangerousChatMessage(string content)
    {
        var result = SafetyGuardrailEngine.EvaluateText(content);

        Assert.Equal("urgent", result.SafetyLevel);
        Assert.NotEmpty(result.Triggers);
    }

    private static HealthCheckIn CreateCheckIn(
        double? temperature = null,
        int? systolic = null,
        int? diastolic = null,
        double sleepHours = 6)
    {
        return new HealthCheckIn
        {
            Id = Guid.NewGuid(),
            UserId = 1,
            SleepHours = sleepHours,
            PainLevel = 2,
            SymptomsJson = "[]",
            MedicalHistoryJson = "[]",
            ContextDataJson = JsonSerializer.Serialize(new Dictionary<string, string>()),
            TemperatureCelsius = temperature,
            SystolicBloodPressure = systolic,
            DiastolicBloodPressure = diastolic,
            TookMedicationToday = false,
            Mood = "Calm",
            MilkStatus = "Normal",
            BabyFeeding = "Normal",
            BabySleep = "Normal",
            CreatedAt = DateTime.UtcNow
        };
    }
}
