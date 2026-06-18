using MomCare.Dto;
using MomCare.Services;

namespace MomCare.Domain.Tests;

public class PlanValidatorEngineTests
{
    private readonly PlanValidatorEngine _engine = new();

    [Fact]
    public void Validate_StrictRecommendation_KeepsTopFourAiServices_WhenAtLeastOneReasonIsGrounded()
    {
        var raw = new GeminiReasoningResult
        {
            IsFromAi = true,
            ServiceScores =
            [
                new ServiceScore
                {
                    ServiceId = "12",
                    Score = 0.92,
                    Reason = "Khach dang dau vet mo sau sinh, can theo doi vet mo va ho tro hoi phuc som."
                },
                new ServiceScore
                {
                    ServiceId = "7",
                    Score = 0.61,
                    Reason = "Phu hop voi tinh trang cua ban"
                },
                new ServiceScore
                {
                    ServiceId = "3",
                    Score = 0.58,
                    Reason = "Khach dang kho ngu va met."
                },
                new ServiceScore
                {
                    ServiceId = "15",
                    Score = 0.57,
                    Reason = "Khach dang tac tia sua sau sinh nen can them ho tro cho bu va giam dau nguc."
                },
                new ServiceScore
                {
                    ServiceId = "18",
                    Score = 0.56,
                    Reason = "Khach can phuc hoi the chat sau sinh sau khi da xu ly van de chinh."
                }
            ]
        };

        var result = _engine.Validate(raw, BuildServices(), allowServiceFallback: false, tags: BuildTags());

        Assert.True(result.IsFromAi);
        Assert.Equal(4, result.ServiceScores.Count);
        Assert.Equal(["12", "7", "3", "15"], result.ServiceScores.Select(score => score.ServiceId).ToArray());
    }

    [Fact]
    public void Validate_StrictRecommendation_Rejects_WhenAllReasonsAreGeneric()
    {
        var raw = new GeminiReasoningResult
        {
            IsFromAi = true,
            ServiceScores =
            [
                new ServiceScore
                {
                    ServiceId = "12",
                    Score = 0.91,
                    Reason = "Phu hop voi tinh trang cua ban"
                },
                new ServiceScore
                {
                    ServiceId = "7",
                    Score = 0.67,
                    Reason = "Dich vu nay co the ho tro ban rat tot trong giai doan hien tai cua ban."
                }
            ]
        };

        var result = _engine.Validate(raw, BuildServices(), allowServiceFallback: false, tags: BuildTags());

        Assert.False(result.IsFromAi);
        Assert.Empty(result.ServiceScores);
    }

    [Fact]
    public void Validate_StrictRecommendation_Rejects_WhenNoServiceMeetsAcceptedThreshold()
    {
        var raw = new GeminiReasoningResult
        {
            IsFromAi = true,
            ServiceScores =
            [
                new ServiceScore
                {
                    ServiceId = "12",
                    Score = 0.54,
                    Reason = "Khach dang dau vet mo sau sinh va can ho tro cham soc vet mo tai nha."
                },
                new ServiceScore
                {
                    ServiceId = "7",
                    Score = 0.41,
                    Reason = "Khach met va kho ngu sau sinh nen can them ho tro hoi phuc."
                }
            ]
        };

        var result = _engine.Validate(raw, BuildServices(), allowServiceFallback: false, tags: BuildTags());

        Assert.False(result.IsFromAi);
        Assert.Empty(result.ServiceScores);
    }

    [Fact]
    public void Validate_NormalizesPercentageScores_BeforeThresholdChecks()
    {
        var raw = new GeminiReasoningResult
        {
            IsFromAi = true,
            ServiceScores =
            [
                new ServiceScore
                {
                    ServiceId = "12",
                    Score = 86,
                    Reason = "Khach dang dau vet mo sau sinh, can theo doi vet mo va ho tro hoi phuc som."
                }
            ]
        };

        var result = _engine.Validate(raw, BuildServices(), allowServiceFallback: false, tags: BuildTags());

        Assert.True(result.IsFromAi);
        Assert.Single(result.ServiceScores);
        Assert.Equal(0.86, result.ServiceScores[0].Score, 3);
    }

    private static List<ServiceSummaryForAi> BuildServices() =>
    [
        new ServiceSummaryForAi { Id = "12", Name = "Cham soc vet mo", Price = 500000, IsPackage = false },
        new ServiceSummaryForAi { Id = "7", Name = "Hoi phuc sau sinh", Price = 600000, IsPackage = false },
        new ServiceSummaryForAi { Id = "3", Name = "Ho tro tam ly", Price = 450000, IsPackage = false },
        new ServiceSummaryForAi { Id = "15", Name = "Ho tro cho bu", Price = 550000, IsPackage = false },
        new ServiceSummaryForAi { Id = "18", Name = "Phuc hoi the chat", Price = 650000, IsPackage = false }
    ];

    private static SymptomTagResult BuildTags() =>
        new()
        {
            PrimaryConcern = "wound_care",
            RelevantContextTokens = ["vet mo", "dau", "sau sinh"]
        };
}
