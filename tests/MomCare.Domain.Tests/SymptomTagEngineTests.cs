using System.Text.Json;
using MomCare.Models;
using MomCare.Services;

namespace MomCare.Domain.Tests;

public class SymptomTagEngineTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SymptomTagEngine _engine = new();

    [Fact]
    public void Extract_PrefersWoundCare_WhenLowGradeFeverAndNoteCentersOnIncision()
    {
        var result = _engine.Extract(BuildCheckIn(
            temperatureCelsius: 38.2,
            note: "Toi bi dau vet mo sau sinh, vet mo cang va can ho tro cham soc.",
            painLocation: "vet mo/khau",
            contextData: new Dictionary<string, string>
            {
                ["deliveryMethod"] = "CSection",
                ["postpartumDay"] = "5",
                ["incisionStatus"] = "Painful"
            }));

        Assert.Equal("wound_care", result.PrimaryConcern);
        Assert.Contains("vet mo", result.RelevantContextTokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("sot", result.RelevantContextTokens, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extract_PrefersFeverMonitoring_WhenFeverIsClinicallySignificant()
    {
        var result = _engine.Extract(BuildCheckIn(
            temperatureCelsius: 38.7,
            note: "Toi dau vet mo va thay nguoi nong sot.",
            painLocation: "vet mo/khau",
            contextData: new Dictionary<string, string>
            {
                ["deliveryMethod"] = "CSection",
                ["postpartumDay"] = "4",
                ["incisionStatus"] = "Painful"
            }));

        Assert.Equal("fever_monitoring", result.PrimaryConcern);
        Assert.Contains("sot", result.RelevantContextTokens, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extract_CapturesBreastfeedingAndMoodTokens_FromCommonUserPhrases()
    {
        var result = _engine.Extract(BuildCheckIn(
            note: "Toi dang tac tia sua, dau num vu, rat cang thang va khong muon an.",
            milkStatus: "Painful",
            mood: "Stressed"));

        Assert.Contains("tac tia sua", result.RelevantContextTokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("dau num vu", result.RelevantContextTokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("cang thang", result.RelevantContextTokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("khong muon an", result.RelevantContextTokens, StringComparer.OrdinalIgnoreCase);
        Assert.True(result.HasBreastfeedingConcern);
    }

    [Fact]
    public void Extract_DoesNotFlagBabyConcern_WhenBabySignalsAreNormal()
    {
        var result = _engine.Extract(BuildCheckIn(
            babyFeeding: "Normal",
            babySleep: "Normal",
            contextData: new Dictionary<string, string>
            {
                ["babyActivity"] = "Normal",
                ["babyWetDiapers"] = "8"
            }));

        Assert.False(result.HasBabyConcern);
    }

    [Fact]
    public void Extract_FlagsBabyConcern_WhenBabySignalsAreAbnormal()
    {
        var result = _engine.Extract(BuildCheckIn(
            babyFeeding: "LessThanUsual",
            babySleep: "CryingOften",
            contextData: new Dictionary<string, string>
            {
                ["babyActivity"] = "Lethargic",
                ["babyWetDiapers"] = "3"
            }));

        Assert.True(result.HasBabyConcern);
    }

    private static HealthCheckIn BuildCheckIn(
        double sleepHours = 6,
        int painLevel = 5,
        string? painLocation = null,
        double? temperatureCelsius = null,
        string mood = "Tired",
        string milkStatus = "Normal",
        string babyFeeding = "Normal",
        string babySleep = "Normal",
        string? note = null,
        Dictionary<string, string>? contextData = null)
    {
        return new HealthCheckIn
        {
            Id = Guid.NewGuid(),
            UserId = 1,
            SleepHours = sleepHours,
            PainLevel = painLevel,
            PainLocation = painLocation,
            SymptomsJson = "[]",
            MedicalHistoryJson = "[]",
            ContextDataJson = JsonSerializer.Serialize(contextData ?? new Dictionary<string, string>(), JsonOptions),
            TemperatureCelsius = temperatureCelsius,
            TookMedicationToday = false,
            Mood = mood,
            MilkStatus = milkStatus,
            BabyFeeding = babyFeeding,
            BabySleep = babySleep,
            Note = note,
            CreatedAt = DateTime.UtcNow
        };
    }
}
