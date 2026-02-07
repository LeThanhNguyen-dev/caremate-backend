using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;
using MomCare.Enums;
using System.Security.Cryptography;
using System.Net.Http;
using Microsoft.Extensions.Configuration;


namespace MomCare.Services;

public class AuthService : IAuthService
{
    private readonly IJwtService _jwtService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AuthService(IJwtService jwtService, IUnitOfWork unitOfWork, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _jwtService = jwtService;
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<TokenResponseDto?> LoginAsync(LoginDto loginDto)
    {
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
    public async Task<TokenResponseDto?> RegisterNurseAsync(RegisterNurseDto registerDto)
    {
        if (await _unitOfWork.Users.AnyAsync(u => u.Email == registerDto.Email))
        {
            return null; // Email exists
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);
        
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

        // Assign NURSE_UNCONFIRMED role
        var roleCode = AppRoles.NurseUnconfirmed;
        var userRole = await _unitOfWork.Roles.FindAsync(r => r.Code == roleCode);
        
        if (userRole == null)
        {
             userRole = new Role { Code = roleCode, Name = "Nurse (Unconfirmed)" };
             await _unitOfWork.Roles.AddAsync(userRole);
             await _unitOfWork.CompleteAsync();
        }

        await _unitOfWork.Users.AddAsync(newUser);
        await _unitOfWork.CompleteAsync(); 

        await _unitOfWork.UserRoles.AddAsync(new UserRole { UserId = newUser.Id, RoleId = userRole.Id });

        // Create Nurse Profile
        var nurseProfile = new NurseProfile
        {
            UserId = newUser.Id,
            Bio = registerDto.Bio,
            YearsExperience = registerDto.YearsExperience,
            ServiceRadiusKm = registerDto.ServiceRadiusKm,
            IsVerified = "unverified",
            ConfirmedAt = null 
        };
        await _unitOfWork.NurseProfiles.AddAsync(nurseProfile);
        await _unitOfWork.CompleteAsync();

        // Generate Token
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

    public async Task<TokenResponseDto?> ExternalLoginAsync(ExternalLoginDto externalLoginDto)
    {
        string providerUserId;
        string email;
        string name = "User";

        if (externalLoginDto.Provider.ToLower() == "google")
        {
            var payload = await VerifyGoogleTokenAsync(externalLoginDto.IdToken);
            if (payload == null) return null;
            providerUserId = payload.Subject;
            email = payload.Email;
            name = payload.Name;
        }
        else if (externalLoginDto.Provider.ToLower() == "facebook")
        {
            var fbUser = await VerifyFacebookTokenAsync(externalLoginDto.IdToken);
            if (fbUser == null) return null;
            providerUserId = fbUser.Id;
            email = fbUser.Email;
            name = fbUser.Name;
        }
        else
        {
            return null; // Unsupported provider
        }

        // 2. Check if OAuthProvider exists
        var existingOAuth = await _unitOfWork.OAuthProviders.FindAsync(
            o => o.Provider == externalLoginDto.Provider && o.ProviderUserId == providerUserId
        );

        User? user;

        if (existingOAuth != null)
        {
            // Login existing user
            user = await _unitOfWork.Users.FindAsync(u => u.Id == existingOAuth.UserId, "UserRoles.Role");
            if (user == null) return null; 
        }
        else
        {
            // 3. Check if email exists to link
            user = await _unitOfWork.Users.FindAsync(u => u.Email == email, "UserRoles.Role");

            if (user != null)
            {
                // Link account
                await _unitOfWork.OAuthProviders.AddAsync(new OAuthProvider
                {
                    Provider = externalLoginDto.Provider,
                    ProviderUserId = providerUserId,
                    Email = email,
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                await _unitOfWork.CompleteAsync();
            }
            else
            {
                // 4. Create new User
                var roleCode = AppRoles.Customer; 
                var userRole = await _unitOfWork.Roles.FindAsync(r => r.Code == roleCode);
                if (userRole == null)
                {
                     userRole = new Role { Code = roleCode, Name = "Customer" };
                     await _unitOfWork.Roles.AddAsync(userRole);
                     await _unitOfWork.CompleteAsync();
                }

                user = new User
                {
                    FullName = name, 
                    Email = email,
                    Status = "active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Users.AddAsync(user);
                await _unitOfWork.CompleteAsync(); 

                if (user.UserRoles == null) user.UserRoles = new List<UserRole>();
                await _unitOfWork.UserRoles.AddAsync(new UserRole { UserId = user.Id, RoleId = userRole.Id });
                await _unitOfWork.CompleteAsync();

                // Link OAuth
                await _unitOfWork.OAuthProviders.AddAsync(new OAuthProvider
                {
                    Provider = externalLoginDto.Provider,
                    ProviderUserId = providerUserId,
                    Email = email,
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                await _unitOfWork.CompleteAsync();

                // Reload user to ensure roles are loaded
                user = await _unitOfWork.Users.FindAsync(u => u.Id == user.Id, "UserRoles.Role");
            }
        }

        // Generate Token
        var roleNames = user!.UserRoles?.Select(ur => ur.Role.Code).ToList() ?? new List<string> { AppRoles.Customer };
        var token = _jwtService.GenerateToken(user, roleNames);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var expirationMinutes = _jwtService.GetTokenExpirationMinutes();

        await _unitOfWork.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
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
            Username = user.FullName,
            Role = roleNames.FirstOrDefault() ?? AppRoles.Customer
        };
    }

    private async Task<Google.Apis.Auth.GoogleJsonWebSignature.Payload?> VerifyGoogleTokenAsync(string idToken)
    {
        try
        {
            var googleClientId = _configuration["Authentication:Google:ClientId"];
            var settings = new Google.Apis.Auth.GoogleJsonWebSignature.ValidationSettings()
            {
                Audience = new List<string>() { googleClientId }
            };
            var payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return payload;
        }
        catch (Exception ex)
        {
            // Log.Error(ex, "Google token validation failed");
            return null;
        }
    }

    private async Task<FacebookUserDto?> VerifyFacebookTokenAsync(string token)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var appId = _configuration["Authentication:Facebook:AppId"];
            var appSecret = _configuration["Authentication:Facebook:AppSecret"];

            // 1. Verify token validity and app_id match
             var debugTokenUrl = $"https://graph.facebook.com/debug_token?input_token={token}&access_token={appId}|{appSecret}";
            var debugResponse = await httpClient.GetAsync(debugTokenUrl);

            if (!debugResponse.IsSuccessStatusCode) return null;

            var debugContent = await debugResponse.Content.ReadAsStringAsync();
            var debugResult = System.Text.Json.JsonSerializer.Deserialize<FacebookDebugTokenResponse>(debugContent);

            if (debugResult?.Data == null || !debugResult.Data.IsValid || debugResult.Data.AppId != appId)
            {
                return null;
            }

            // 2. Get user info
            var response = await httpClient.GetAsync($"https://graph.facebook.com/me?fields=id,name,email&access_token={token}");
            
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            var fbUser = System.Text.Json.JsonSerializer.Deserialize<FacebookUserDto>(content);
            
            return fbUser;
        }
        catch
        {
            return null;
        }
    }

    private class FacebookUserDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }

    private class FacebookDebugTokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public FacebookDebugTokenData Data { get; set; }
    }

    private class FacebookDebugTokenData
    {
        [System.Text.Json.Serialization.JsonPropertyName("app_id")]
        public string AppId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("is_valid")]
        public bool IsValid { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }
}
