using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Interfaces;
using System.Security.Claims;

namespace MomCare.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
    {
        var result = await _authService.RegisterAsync(registerDto);
        
        if (result == null)
        {
            return BadRequest(new { message = "Email/phone already exists or invalid role provided" });
        }

        return Ok(result);
    }

    [HttpPost("signup/customer")]
    public async Task<IActionResult> RegisterCustomer([FromBody] RegisterDto registerDto)
    {
        registerDto.Role = "customer";
        var result = await _authService.RegisterAsync(registerDto);
        
        if (result == null)
        {
            return BadRequest(new { message = "Email or phone already exists" });
        }

        return Ok(result);
    }

    [HttpPost("signup/nurse")]
    public async Task<IActionResult> RegisterNurse([FromBody] RegisterNurseDto registerDto)
    {
        var result = await _authService.RegisterNurseAsync(registerDto);
        
        if (result == null)
        {
            return BadRequest(new { message = "Email or phone already exists" });
        }

        return Ok(result);
    }

    [HttpPost("login/external")]
    public async Task<IActionResult> ExternalLogin([FromBody] ExternalLoginDto externalLoginDto)
    {
        var result = await _authService.ExternalLoginAsync(externalLoginDto);
        
        if (result == null)
        {
            return Unauthorized(new { message = "Invalid external token or login failed" });
        }

        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        var result = await _authService.LoginAsync(loginDto);
        
        if (result == null)
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        return Ok(result);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        var result = await _authService.RefreshTokenAsync(request);
        
        if (result == null)
        {
            return Unauthorized(new { message = "Invalid token" });
        }

        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        var fullName = User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)?.Value
            ?? User.Identity?.Name;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        return Ok(new
        {
            userId,
            fullName,
            email,
            role
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutDto dto)
    {
        var userId = GetUserId();
        var ok = await _authService.LogoutAsync(userId, dto.RefreshToken);
        if (!ok)
        {
            return BadRequest(new { message = "Invalid refresh token" });
        }

        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPatch("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = GetUserId();
        var ok = await _authService.ChangePasswordAsync(userId, dto);
        if (!ok)
        {
            return BadRequest(new { message = "Change password failed. Please check current password and password policy." });
        }

        return Ok(new { message = "Password changed successfully" });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var token = await _authService.GenerateResetPasswordTokenAsync(dto.Email);

        // For school-project/demo mode: return token directly for easy testing.
        if (token == null)
        {
            return Ok(new { message = "If the email exists, reset instructions have been generated." });
        }

        return Ok(new
        {
            message = "Reset token generated",
            resetToken = token
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var ok = await _authService.ResetPasswordAsync(dto);
        if (!ok)
        {
            return BadRequest(new { message = "Reset password failed. Token may be invalid or expired." });
        }

        return Ok(new { message = "Password reset successfully" });
    }

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.Parse(raw ?? "0");
    }
}
