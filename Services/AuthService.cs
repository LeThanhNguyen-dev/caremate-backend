using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class AuthService : IAuthService
{
    private readonly IJwtService _jwtService;
    private readonly IUnitOfWork _unitOfWork;

    public AuthService(IJwtService jwtService, IUnitOfWork unitOfWork)
    {
        _jwtService = jwtService;
        _unitOfWork = unitOfWork;
    }

    public async Task<TokenResponseDto?> LoginAsync(LoginDto loginDto)
    {
        // Find user by email with roles included
        // Note: include string "UserRoles.Role"
        var user = await _unitOfWork.Users.FindAsync(
            u => u.Email == loginDto.Email, 
            "UserRoles.Role");
        
        if (user == null)
        {
            return null; 
        }

        if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            return null; 
        } 

        // Safely handle if UserRoles is null (though EF should include it as empty list if not found)
        var roleNames = user.UserRoles?.Select(ur => ur.Role.Code).ToList() ?? new List<string>();
        var primaryRole = roleNames.FirstOrDefault() ?? "customer";
        
        var token = _jwtService.GenerateToken(user, roleNames);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var expirationMinutes = _jwtService.GetTokenExpirationMinutes();

        await _unitOfWork.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7), // Refresh token valid for 7 days
            CreatedAt = DateTime.UtcNow
        });
        await _unitOfWork.CompleteAsync();

        return new TokenResponseDto
        {
            AccessToken = token,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = expirationMinutes * 60,
            Username = user.FullName, 
            Role = primaryRole
        };
    }

    public async Task<TokenResponseDto?> RegisterAsync(RegisterDto registerDto)
    {
        if (await _unitOfWork.Users.AnyAsync(u => u.Email == registerDto.Email))
        {
            return null; 
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);
        
        var allowedRoles = new[] { "customer", "nurse", "admin" };
        if (!allowedRoles.Contains(registerDto.Role.ToLower()))
        {
            return null; // Invalid role
        }

        var newUser = new User
        {
            FullName = registerDto.FullName,
            Email = registerDto.Email,
            Phone = registerDto.Phone,
            PasswordHash = passwordHash,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assign requested role
        var roleCode = registerDto.Role.ToLower();
        var userRole = await _unitOfWork.Roles.FindAsync(r => r.Code == roleCode);
        
        if (userRole == null)
        {
             // Auto-create if missing (Dev convenience, remove in strict prod)
             userRole = new Role { Code = roleCode, Name = char.ToUpper(roleCode[0]) + roleCode.Substring(1) };
             await _unitOfWork.Roles.AddAsync(userRole);
             await _unitOfWork.CompleteAsync(); // Save to get ID
        }

        await _unitOfWork.Users.AddAsync(newUser);
        await _unitOfWork.CompleteAsync(); // Save user first to get ID

        await _unitOfWork.UserRoles.AddAsync(new UserRole { UserId = newUser.Id, RoleId = userRole.Id });
        await _unitOfWork.CompleteAsync();

        // Generate JWT token
        var roles = new List<string> { userRole.Code };
        var token = _jwtService.GenerateToken(newUser, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var expirationMinutes = _jwtService.GetTokenExpirationMinutes();

        await _unitOfWork.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = newUser.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        await _unitOfWork.CompleteAsync();

        return new TokenResponseDto
        {
            AccessToken = token,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = expirationMinutes * 60,
            Username = newUser.FullName,
            Role = userRole.Code
        };
    }

    public async Task<TokenResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto request)
    {
        var principal = _jwtService.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal == null) return null;

        var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId)) return null;

        var storedRefreshToken = await _unitOfWork.RefreshTokens.FindAsync(
            r => r.Token == request.RefreshToken && r.UserId == userId);

        if (storedRefreshToken == null || 
            storedRefreshToken.ExpiresAt <= DateTime.UtcNow || 
            storedRefreshToken.RevokedAt != null)
        {
            return null;
        }

        // Revoke old token
        storedRefreshToken.RevokedAt = DateTime.UtcNow;
        _unitOfWork.RefreshTokens.Update(storedRefreshToken);

        var user = await _unitOfWork.Users.FindAsync(u => u.Id == userId, "UserRoles.Role");
        if (user == null) return null;

        var roleNames = user.UserRoles?.Select(ur => ur.Role.Code).ToList() ?? new List<string>();
        
        var newAccessToken = _jwtService.GenerateToken(user, roleNames);
        var newRefreshToken = _jwtService.GenerateRefreshToken();
        
        await _unitOfWork.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = userId,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        
        await _unitOfWork.CompleteAsync();

        return new TokenResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            TokenType = "Bearer",
            ExpiresIn = _jwtService.GetTokenExpirationMinutes() * 60,
            Username = user.FullName,
            Role = roleNames.FirstOrDefault() ?? "customer"
        };
    }
}
