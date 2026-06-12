namespace MomCare.Dto;

public class FollowUpQuestionDto
{
    public string Key { get; set; } = string.Empty;
    public string QuestionVi { get; set; } = string.Empty;
    public string InputType { get; set; } = "text";
    public string? Unit { get; set; }
}
