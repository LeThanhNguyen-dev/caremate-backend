namespace MomCare.Dto;

public class NurseDocumentDto
{
    public int Id { get; set; }
    public string Type { get; set; } = null!;
    public string FileUrl { get; set; } = null!;
    public string Status { get; set; } = null!;
}
