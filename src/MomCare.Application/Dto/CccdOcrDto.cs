using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MomCare.Dto;

public class CccdOcrRequestDto
{
    [Required]
    public string Type { get; set; } = null!;

    [Required]
    public IFormFile File { get; set; } = null!;
}

public class CccdOcrResultDto
{
    public bool IsIdentityCard { get; set; }
    public string Side { get; set; } = string.Empty;
    public string? IdNumber { get; set; }
    public string? FullName { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Nationality { get; set; }
    public string? PlaceOfOrigin { get; set; }
    public string? PlaceOfResidence { get; set; }
    public string? DateOfIssue { get; set; }
    public string? DateOfExpiry { get; set; }
    public string? IssuingAuthority { get; set; }
    public int ConfidenceScore { get; set; }
    public string? Warning { get; set; }
    public string RawText { get; set; } = string.Empty;
}
