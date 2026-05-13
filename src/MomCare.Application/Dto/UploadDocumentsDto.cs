using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MomCare.Dto;

public class UploadDocumentsDto
{
    [Required]
    public string Type { get; set; } = null!;

    [Required]
    public List<IFormFile> Files { get; set; } = new();
}
