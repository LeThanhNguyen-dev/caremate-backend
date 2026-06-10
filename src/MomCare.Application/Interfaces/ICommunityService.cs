using MomCare.Dto;

namespace MomCare.Interfaces;

public interface ICommunityService
{
    Task<IEnumerable<CommunityPostDto>> GetPostsAsync(int? viewerId, string? search);
    Task<CommunityPostDto?> CreatePostAsync(int authorId, CreateCommunityPostDto dto);
    Task<(CommunityPostDto? Post, bool Forbidden)> UpdatePostAsync(int actorId, bool actorIsAdmin, int postId, UpdateCommunityPostDto dto);
    Task<bool?> DeletePostAsync(int actorId, bool actorIsAdmin, int postId);
    Task<CommunityPostDto?> ToggleLikeAsync(int userId, int postId);
    Task<CommunityCommentDto?> ToggleCommentLikeAsync(int userId, int postId, int commentId);
    Task<IEnumerable<CommunityCommentLikerDto>?> GetCommentLikersAsync(int postId, int commentId);
    Task<CommunityCommentDto?> CreateCommentAsync(int authorId, int postId, CreateCommunityCommentDto dto);
}
