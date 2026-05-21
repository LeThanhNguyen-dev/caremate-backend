using MomCare.Dto;

namespace MomCare.Interfaces;

public interface ICommunityService
{
    Task<IEnumerable<CommunityPostDto>> GetPostsAsync(int? viewerId, string? search);
    Task<CommunityPostDto?> CreatePostAsync(int authorId, CreateCommunityPostDto dto);
    Task<CommunityPostDto?> ToggleLikeAsync(int userId, int postId);
    Task<CommunityCommentDto?> CreateCommentAsync(int authorId, int postId, CreateCommunityCommentDto dto);
}
