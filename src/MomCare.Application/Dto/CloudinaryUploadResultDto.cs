namespace MomCare.Dto;

/// <summary>
/// Response DTO for Cloudinary upload results.
/// </summary>
public class CloudinaryUploadResultDto
{
    public string Url { get; set; } = null!;
    public string PublicId { get; set; } = null!;
}
