using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class CommunityPostDto
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Role { get; set; } = "Thanh vien CareMate";
    public string? Avatar { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
    public string? ImageUrl { get; set; }
    public int Likes { get; set; }
    public bool LikedByMe { get; set; }
    public DateTime CreatedAt { get; set; }
    public IEnumerable<CommunityCommentDto> Comments { get; set; } = [];
}

public class CommunityCommentDto
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public int? ParentCommentId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Likes { get; set; }
    public bool LikedByMe { get; set; }
    public DateTime CreatedAt { get; set; }
    public IEnumerable<CommunityCommentDto> Replies { get; set; } = [];
}

public class CommunityCommentLikerDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Avatar { get; set; }
}

public class CreateCommunityPostDto
{
    [MaxLength(180)]
    public string? Title { get; set; }

    [MaxLength(4000)]
    public string? Content { get; set; }

    public string[] Tags { get; set; } = [];

    public IFormFile? Image { get; set; }
}

public class UpdateCommunityPostDto
{
    [MaxLength(180)]
    public string? Title { get; set; }

    [MaxLength(4000)]
    public string? Content { get; set; }

    public string[] Tags { get; set; } = [];
}

public class CreateCommunityCommentDto
{
    [Required]
    [MaxLength(1200)]
    public string Content { get; set; } = string.Empty;

    public int? ParentCommentId { get; set; }
}
