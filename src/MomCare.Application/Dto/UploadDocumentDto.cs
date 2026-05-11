using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MomCare.Dto;

public class UploadDocumentDto
{
    /// <summary>
    /// Document type: id_card_front, id_card_back, certificate
    /// </summary>
    [Required]
    public string Type { get; set; } = null!;

    /// <summary>
    /// The image file to upload. Allowed: jpg, png. Max size: 5MB.
    /// </summary>
    [Required]
    public IFormFile File { get; set; } = null!;
}
