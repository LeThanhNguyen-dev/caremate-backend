using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[ApiController]
[Route("api/community")]
public class CommunityController : ControllerBase
{
    private readonly ICommunityService _communityService;

    public CommunityController(ICommunityService communityService)
    {
        _communityService = communityService;
    }

    [HttpGet("posts")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPosts([FromQuery] string? search)
    {
        var posts = await _communityService.GetPostsAsync(GetOptionalUserId(), search);
        return Ok(posts);
    }

    [HttpPost("posts")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> CreatePost([FromForm] CreateCommunityPostDto dto)
    {
        try
        {
            var post = await _communityService.CreatePostAsync(GetUserId(), dto);
            if (post == null) return BadRequest(new { message = "Vui lòng nhập nội dung hoặc chọn ảnh trước khi đăng." });

            return CreatedAtAction(nameof(GetPosts), new { id = post.Id }, post);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "Tải ảnh lên Cloudinary quá lâu. Vui lòng thử lại hoặc chọn ảnh nhẹ hơn." });
        }
        catch (InvalidOperationException ex) when (ex.InnerException is TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("posts/{postId:int}/like")]
    [Authorize]
    public async Task<IActionResult> ToggleLike(int postId)
    {
        var post = await _communityService.ToggleLikeAsync(GetUserId(), postId);
        if (post == null) return NotFound(new { message = "Post not found." });

        return Ok(post);
    }

    [HttpPost("posts/{postId:int}/comments/{commentId:int}/like")]
    [Authorize]
    public async Task<IActionResult> ToggleCommentLike(int postId, int commentId)
    {
        var comment = await _communityService.ToggleCommentLikeAsync(GetUserId(), postId, commentId);
        if (comment == null) return NotFound(new { message = "Comment not found." });

        return Ok(comment);
    }

    [HttpGet("posts/{postId:int}/comments/{commentId:int}/likes")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCommentLikers(int postId, int commentId)
    {
        var likers = await _communityService.GetCommentLikersAsync(postId, commentId);
        if (likers == null) return NotFound(new { message = "Comment not found." });

        return Ok(likers);
    }

    [HttpPost("posts/{postId:int}/comments")]
    [Authorize]
    public async Task<IActionResult> CreateComment(int postId, [FromBody] CreateCommunityCommentDto dto)
    {
        var comment = await _communityService.CreateCommentAsync(GetUserId(), postId, dto);
        if (comment == null) return BadRequest(new { message = "Comment cannot be empty or post was not found." });

        return Ok(comment);
    }

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.Parse(raw ?? "0");
    }

    private int? GetOptionalUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var userId) && userId > 0 ? userId : null;
    }
}
