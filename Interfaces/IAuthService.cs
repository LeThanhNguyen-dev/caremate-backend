using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IAuthService
{
    Task<TokenResponseDto?> LoginAsync(LoginDto loginDto);
    Task<TokenResponseDto?> RegisterAsync(RegisterDto registerDto);
    Task<TokenResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto request);
}
