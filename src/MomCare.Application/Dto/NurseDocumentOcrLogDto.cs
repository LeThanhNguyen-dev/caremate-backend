namespace MomCare.Dto;

public class NurseDocumentOcrLogDto
{
    public Guid Id { get; set; }
    public int NurseDocumentId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string OcrStatus { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = [];
    public int AttemptCount { get; set; }
    public string ProcessedBy { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public CccdOcrResultDto? Result { get; set; }
}
