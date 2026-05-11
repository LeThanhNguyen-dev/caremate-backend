using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IAuthService
{
    Task<TokenResponseDto?> LoginAsync(LoginDto loginDto);
    Task<TokenResponseDto?> RegisterAsync(RegisterDto registerDto); // Keeping for Customer or Generic
    Task<TokenResponseDto?> RegisterNurseAsync(RegisterNurseDto registerDto);
    Task<TokenResponseDto?> ExternalLoginAsync(ExternalLoginDto externalLoginDto);
    Task<TokenResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto request);
    Task<bool> LogoutAsync(int userId, string refreshToken);
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto);
    Task<string?> GenerateResetPasswordTokenAsync(string email);
    Task<bool> ResetPasswordAsync(ResetPasswordDto dto);
}
