using MomCare.Dto;
using MomCare.Services;

namespace MomCare.Domain.Tests;

public class CarePlanPromptBuilderTests
{
    [Fact]
    public void RecommendationPrompt_UsesAiOnlyContract_AndOmitsPlanItems()
    {
        var builder = new GeminiPromptBuilder();
        var tags = new SymptomTagResult
        {
            PostpartumStage = "early",
            DeliveryType = "cesarean",
            Tags = ["sinh_mo", "dau_vet_mo_vua"],
            PrimaryNeeds = ["cham_soc_vet_mo"],
            PrimaryConcern = "wound_care",
            RelevantContextTokens = ["vet mo", "dau", "sau sinh"]
        };

        List<ServiceSummaryForAi> services =
        [
            new ServiceSummaryForAi
            {
                Id = "12",
                Name = "Cham soc vet mo sau sinh",
                ShortDescription = "Theo doi va cham soc vet mo",
                Tags = ["hau san"],
                Price = 500000,
                IsPackage = false
            }
        ];

        var prompt = builder.BuildReasoningPrompt(tags, services, booking: null);

        Assert.Contains("CHON 4 dich vu", prompt, StringComparison.Ordinal);
        Assert.Contains("Be khong co van de", prompt, StringComparison.Ordinal);
        Assert.Contains("KHONG dung snake_case", prompt, StringComparison.Ordinal);
        Assert.Contains("CHI 2 truong: serviceScores va reasoning", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("\"planItems\"", prompt, StringComparison.Ordinal);
        Assert.Contains("\"score\": 0.86", prompt, StringComparison.Ordinal);
        Assert.Contains("\"score\": 0.71", prompt, StringComparison.Ordinal);
        Assert.Contains("\"score\": 0.56", prompt, StringComparison.Ordinal);
    }
}
