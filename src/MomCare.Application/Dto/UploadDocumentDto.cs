namespace MomCare.Dto;

public class UploadDocumentDto
{
    public string Type { get; set; } = null!; // id_card, hospital_certificate, etc.
    public string FileUrl { get; set; } = null!;
}
