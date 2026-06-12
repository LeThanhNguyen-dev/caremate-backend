using System.Reflection;
using MomCare.Services;

namespace MomCare.Domain.Tests;

public class HealthCheckInTextInferenceTests
{
    [Fact]
    public void InferContextFromNote_UnderstandsSevereLowerAbdominalPain()
    {
        var method = typeof(HealthCheckInService).GetMethod(
            "InferContextFromNote",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var inferred = method.Invoke(null, ["đau bụng dưới rất nhiều"]);

        Assert.NotNull(inferred);
        Assert.Equal(8, GetProperty<int>(inferred, "PainLevel"));
        Assert.Equal("bụng dưới", GetProperty<string?>(inferred, "PainLocation"));
        Assert.Contains("đau bụng dưới", GetProperty<List<string>>(inferred, "Symptoms"));
    }

    [Theory]
    [InlineData("đau lưng nhiều quá", 8, "lưng")]
    [InlineData("đau vết mổ tăng lên", 6, "vết mổ/khâu")]
    [InlineData("đau ngực và căng sữa", 5, "ngực/sữa")]
    [InlineData("đau tầng sinh môn âm ỉ", 3, "tầng sinh môn")]
    [InlineData("đau bắp chân rất nhiều", 8, "bắp chân")]
    public void InferContextFromNote_UnderstandsDifferentPainLocations(string note, int expectedPainLevel, string expectedLocation)
    {
        var inferred = InvokeInference(note);

        Assert.Equal(expectedPainLevel, GetProperty<int>(inferred, "PainLevel"));
        Assert.Equal(expectedLocation, GetProperty<string?>(inferred, "PainLocation"));
    }

    private static object InvokeInference(string note)
    {
        var method = typeof(HealthCheckInService).GetMethod(
            "InferContextFromNote",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var inferred = method.Invoke(null, [note]);
        Assert.NotNull(inferred);
        return inferred;
    }

    private static T GetProperty<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(property);
        return Assert.IsType<T>(property.GetValue(instance));
    }
}
