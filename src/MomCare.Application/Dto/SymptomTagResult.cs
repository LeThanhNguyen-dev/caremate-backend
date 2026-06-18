namespace MomCare.Dto;

/// <summary>
/// Contains normalized symptom tags extracted from a health check-in.
/// </summary>
public class SymptomTagResult
{
    /// <summary>Tag list in snake_case, for example "sinh_mo", "ngay_hau_san_3", "dau_bung_vua".</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>"early" = 0-3 days, "mid" = 4-14 days, "late" = 15-42 days.</summary>
    public string PostpartumStage { get; set; } = string.Empty;

    /// <summary>"cesarean" or "vaginal".</summary>
    public string DeliveryType { get; set; } = string.Empty;

    /// <summary>Primary care needs inferred from normalized symptoms.</summary>
    public List<string> PrimaryNeeds { get; set; } = [];

    /// <summary>Highest-priority concern for AI recommendation ranking.</summary>
    public string PrimaryConcern { get; set; } = string.Empty;

    /// <summary>Broad symptom/context tokens derived from the current input for prompt grounding and reason validation.</summary>
    public List<string> RelevantContextTokens { get; set; } = [];

    /// <summary>Whether the baby has any explicit abnormal feeding, sleep, diaper, activity, or note-based signal.</summary>
    public bool HasBabyConcern { get; set; }

    /// <summary>Whether the mother has an explicit breastfeeding-related concern.</summary>
    public bool HasBreastfeedingConcern { get; set; }

    /// <summary>Aggregate 0-100 risk score separate from safety urgent classification.</summary>
    public int OverallRiskScore { get; set; }

    /// <summary>Raw form data as key-value summary for AI to reason from directly.</summary>
    public string RawCheckinSummary { get; set; } = string.Empty;
}
