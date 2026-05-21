using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;
using System.Collections.Concurrent;

namespace MomCare.Services;

public class CommunityService : ICommunityService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CreatePostLocks = new();
    private readonly MomCareContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly INotificationService _notificationService;

    public CommunityService(
        MomCareContext context,
        UserManager<ApplicationUser> userManager,
        ICloudinaryService cloudinaryService,
        INotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _cloudinaryService = cloudinaryService;
        _notificationService = notificationService;
    }

    public async Task<IEnumerable<CommunityPostDto>> GetPostsAsync(int? viewerId, string? search)
    {
        var query = _context.CommunityPosts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Likes)
            .Include(p => p.Comments.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Author)
            .Where(p => !p.IsDeleted);

        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(p =>
                p.Title.Contains(normalizedSearch) ||
                p.Content.Contains(normalizedSearch) ||
                (p.Tags != null && p.Tags.Contains(normalizedSearch)) ||
                p.Author.FullName.Contains(normalizedSearch));
        }

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(100)
            .ToListAsync();

        var authorIds = posts.Select(p => p.AuthorId)
            .Concat(posts.SelectMany(p => p.Comments.Select(c => c.AuthorId)))
            .Distinct()
            .ToList();
        var roleMap = await GetRoleMapAsync(authorIds);

        return posts.Select(p => ToPostDto(p, viewerId, roleMap));
    }

    public async Task<CommunityPostDto?> CreatePostAsync(int authorId, CreateCommunityPostDto dto)
    {
        var title = dto.Title?.Trim() ?? string.Empty;
        var content = dto.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content) && dto.Image == null) return null;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = string.IsNullOrWhiteSpace(content)
                ? "Bai viet moi"
                : content.Length > 80 ? $"{content[..77]}..." : content;
        }

        var tags = SerializeTags(dto.Tags);
        var duplicateKey = $"{authorId}|{title}|{content}|{tags}|{dto.Image?.FileName}|{dto.Image?.Length}";
        var createLock = CreatePostLocks.GetOrAdd(duplicateKey, _ => new SemaphoreSlim(1, 1));
        await createLock.WaitAsync();

        try
        {
            var duplicateCutoff = DateTime.UtcNow.AddSeconds(-30);
            var duplicatePostId = await _context.CommunityPosts
                .Where(p =>
                    p.AuthorId == authorId &&
                    p.Title == title &&
                    p.Content == content &&
                    p.Tags == tags &&
                    !p.IsDeleted &&
                    p.CreatedAt >= duplicateCutoff)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => p.Id)
                .FirstOrDefaultAsync();

            if (duplicatePostId > 0)
            {
                var duplicate = await GetPostEntityAsync(duplicatePostId);
                if (duplicate != null)
                {
                    var duplicateRoleMap = await GetRoleMapAsync([duplicate.AuthorId]);
                    return ToPostDto(duplicate, authorId, duplicateRoleMap);
                }
            }

            var post = new CommunityPost
            {
                AuthorId = authorId,
                Title = title,
                Content = content,
                Tags = tags,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (dto.Image != null)
            {
                var upload = await _cloudinaryService.UploadPublicAsync(dto.Image, "community/posts");
                post.ImageUrl = upload.Url;
                post.ImagePublicId = upload.PublicId;
            }

            _context.CommunityPosts.Add(post);
            await _context.SaveChangesAsync();

            var created = await GetPostEntityAsync(post.Id);
            if (created == null) return null;

            var roleMap = await GetRoleMapAsync([created.AuthorId]);
            return ToPostDto(created, authorId, roleMap);
        }
        finally
        {
            createLock.Release();
            CreatePostLocks.TryRemove(duplicateKey, out _);
        }
    }

    public async Task<CommunityPostDto?> ToggleLikeAsync(int userId, int postId)
    {
        var targetPost = await _context.CommunityPosts
            .AsNoTracking()
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted);
        if (targetPost == null) return null;

        var existing = await _context.CommunityPostLikes.FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);
        var createdLike = false;
        if (existing == null)
        {
            _context.CommunityPostLikes.Add(new CommunityPostLike
            {
                PostId = postId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
            createdLike = true;
        }
        else
        {
            _context.CommunityPostLikes.Remove(existing);
        }

        await _context.SaveChangesAsync();

        if (createdLike && targetPost.AuthorId != userId)
        {
            var actorName = await GetUserDisplayNameAsync(userId);
            await _notificationService.CreateAsync(
                targetPost.AuthorId,
                "Bài viết có lượt thích mới",
                $"{actorName} đã thích bài viết \"{TruncateForNotification(targetPost.Title)}\".",
                "community_like");
        }

        var post = await GetPostEntityAsync(postId);
        if (post == null) return null;

        var roleMap = await GetRoleMapAsync(
            post.Comments.Select(c => c.AuthorId).Append(post.AuthorId).Distinct().ToList());
        return ToPostDto(post, userId, roleMap);
    }

    public async Task<CommunityCommentDto?> CreateCommentAsync(int authorId, int postId, CreateCommunityCommentDto dto)
    {
        var content = dto.Content.Trim();
        if (string.IsNullOrWhiteSpace(content)) return null;

        var targetPost = await _context.CommunityPosts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted);
        if (targetPost == null) return null;

        var comment = new CommunityComment
        {
            PostId = postId,
            AuthorId = authorId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        _context.CommunityComments.Add(comment);
        await _context.SaveChangesAsync();

        var created = await _context.CommunityComments
            .AsNoTracking()
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == comment.Id);

        if (created != null && targetPost.AuthorId != authorId)
        {
            await _notificationService.CreateAsync(
                targetPost.AuthorId,
                "Bình luận mới",
                $"{created.Author.FullName} đã bình luận về bài viết \"{TruncateForNotification(targetPost.Title)}\".",
                "community_comment");
        }

        return created == null ? null : ToCommentDto(created);
    }

    private async Task<CommunityPost?> GetPostEntityAsync(int postId)
    {
        return await _context.CommunityPosts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Likes)
            .Include(p => p.Comments.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted);
    }

    private async Task<Dictionary<int, string>> GetRoleMapAsync(IEnumerable<int> userIds)
    {
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();

        var result = new Dictionary<int, string>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result[user.Id] = roles.FirstOrDefault() switch
            {
                "nurse_confirmed" => "Chuyen gia CareMate",
                "admin" => "Quan tri vien",
                _ => "Thanh vien CareMate"
            };
        }

        return result;
    }

    private static CommunityPostDto ToPostDto(CommunityPost post, int? viewerId, IReadOnlyDictionary<int, string> roleMap)
    {
        return new CommunityPostDto
        {
            Id = post.Id,
            AuthorId = post.AuthorId,
            Author = post.Author.FullName,
            Role = roleMap.TryGetValue(post.AuthorId, out var role) ? role : "Thanh vien CareMate",
            Avatar = post.Author.Avatar,
            Title = post.Title,
            Content = post.Content,
            Tags = DeserializeTags(post.Tags),
            ImageUrl = post.ImageUrl,
            Likes = post.Likes.Count,
            LikedByMe = viewerId.HasValue && post.Likes.Any(l => l.UserId == viewerId.Value),
            CreatedAt = post.CreatedAt,
            Comments = post.Comments
                .OrderBy(c => c.CreatedAt)
                .Select(ToCommentDto)
                .ToList()
        };
    }

    private static CommunityCommentDto ToCommentDto(CommunityComment comment)
    {
        return new CommunityCommentDto
        {
            Id = comment.Id,
            AuthorId = comment.AuthorId,
            Author = comment.Author.FullName,
            Avatar = comment.Author.Avatar,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt
        };
    }

    private static string SerializeTags(IEnumerable<string> tags)
    {
        var normalized = tags
            .Select(tag => tag.Trim().TrimStart('#'))
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5);

        return string.Join(",", normalized);
    }

    private static string[] DeserializeTags(string? tags)
    {
        return string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private async Task<string> GetUserDisplayNameAsync(int userId)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        return user?.FullName ?? "Một thành viên";
    }

    private static string TruncateForNotification(string value)
    {
        var normalized = value.Trim();
        return normalized.Length <= 60 ? normalized : $"{normalized[..57]}...";
    }

}
