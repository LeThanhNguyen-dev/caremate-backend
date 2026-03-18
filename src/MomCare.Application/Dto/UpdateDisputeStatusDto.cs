namespace MomCare.Dto;

public class UpdateDisputeStatusDto
{
    public string Status { get; set; } = "open";
    public string? AdminNote { get; set; }
}
