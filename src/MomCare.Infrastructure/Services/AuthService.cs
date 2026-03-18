using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class AuthService : IAuthService
{
    private readonly IJwtService _jwtService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly MomCareContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AuthService(
        IJwtService jwtService,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        MomCareContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _jwtService = jwtService;
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<TokenResponseDto?> LoginAsync(LoginDto loginDto)
    {
        var user = await _userManager.FindByEmailAsync(loginDto.Email);
        if (user == null)
        {
            return null;
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, loginDto.Password);
        if (!isPasswordValid)
        {
            return null;
        }

        return await BuildTokenResponseAsync(user);
    }

    public async Task<TokenResponseDto?> RegisterAsync(RegisterDto registerDto)
    {
        // Public register should only create customers.
        var roleCode = (registerDto.Role ?? AppRoles.Customer).ToLowerInvariant();
        if (roleCode != AppRoles.Customer)
        {
            return null;
        }

        var existing = await _userManager.FindByEmailAsync(registerDto.Email);
        if (existing != null)
        {
            return null;
        }

        var user = new ApplicationUser
        {
            FullName = registerDto.FullName,
            Email = registerDto.Email,
            UserName = registerDto.Email,
            PhoneNumber = registerDto.Phone,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, registerDto.Password);
        if (!createResult.Succeeded)
        {
            return null;
        }

        await EnsureRoleExistsAsync(AppRoles.Customer, "Customer");
        var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Customer);
        if (!roleResult.Succeeded)
        {
            return null;
        }

        return await BuildTokenResponseAsync(user);
    }

    public async Task<TokenResponseDto?> RegisterNurseAsync(RegisterNurseDto registerDto)
    {
        var existing = await _userManager.FindByEmailAsync(registerDto.Email);
        if (existing != null)
        {
            return null;
        }

        var user = new ApplicationUser
        {
            FullName = registerDto.FullName,
            Email = registerDto.Email,
            UserName = registerDto.Email,
            PhoneNumber = registerDto.Phone,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, registerDto.Password);
        if (!createResult.Succeeded)
        {
            return null;
        }

        await EnsureRoleExistsAsync(AppRoles.NurseUnconfirmed, "Nurse (Unconfirmed)");
        var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.NurseUnconfirmed);
        if (!roleResult.Succeeded)
        {
            return null;
        }

        var nurseProfile = new NurseProfile
        {
            UserId = user.Id,
            Bio = registerDto.Bio,
            YearsExperience = registerDto.YearsExperience,
            ServiceRadiusKm = registerDto.ServiceRadiusKm,
            IsVerified = "unverified",
            ConfirmedAt = null
        };

        _context.NurseProfiles.Add(nurseProfile);
        await _context.SaveChangesAsync();

        return await BuildTokenResponseAsync(user);
    }

    public async Task<TokenResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto request)
    {
        var principal = _jwtService.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal == null)
        {
            return null;
        }

        var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }

        var storedRefreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken && r.UserId == userId);

        if (storedRefreshToken == null ||
            storedRefreshToken.ExpiresAt <= DateTime.UtcNow ||
            storedRefreshToken.RevokedAt != null)
        {
            return null;
        }

        storedRefreshToken.RevokedAt = DateTime.UtcNow;

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return null;
        }

        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var accessToken = _jwtService.GenerateToken(user, roles);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return new TokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            TokenType = "Bearer",
            ExpiresIn = _jwtService.GetTokenExpirationMinutes() * 60,
            Username = user.FullName,
            Role = roles.FirstOrDefault() ?? AppRoles.Customer
        };
    }

    public async Task<TokenResponseDto?> ExternalLoginAsync(ExternalLoginDto externalLoginDto)
    {
        var provider = externalLoginDto.Provider.ToLowerInvariant();
        string providerUserId;
        string email;
        string name = "User";

        if (provider == "google")
        {
            var payload = await VerifyGoogleTokenAsync(externalLoginDto.IdToken);
            if (payload == null)
            {
                return null;
            }

            providerUserId = payload.Subject;
            email = payload.Email;
            name = payload.Name;
        }
        else if (provider == "facebook")
        {
            var fbUser = await VerifyFacebookTokenAsync(externalLoginDto.IdToken);
            if (fbUser == null)
            {
                return null;
            }

            providerUserId = fbUser.Id;
            email = fbUser.Email;
            name = fbUser.Name;
        }
        else
        {
            return null;
        }

        var user = await _userManager.FindByLoginAsync(provider, providerUserId);

        if (user == null)
        {
            user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    FullName = string.IsNullOrWhiteSpace(name) ? "User" : name,
                    Email = email,
                    UserName = email,
                    EmailConfirmed = true,
                    Status = "active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    return null;
                }

                await EnsureRoleExistsAsync(AppRoles.Customer, "Customer");
                var addRoleResult = await _userManager.AddToRoleAsync(user, AppRoles.Customer);
                if (!addRoleResult.Succeeded)
                {
                    return null;
                }
            }

            var logins = await _userManager.GetLoginsAsync(user);
            if (!logins.Any(l => l.LoginProvider == provider && l.ProviderKey == providerUserId))
            {
                var loginResult = await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerUserId, provider));
                if (!loginResult.Succeeded)
                {
                    return null;
                }
            }
        }

        return await BuildTokenResponseAsync(user);
    }

    private async Task<TokenResponseDto> BuildTokenResponseAsync(ApplicationUser user)
    {
        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var token = _jwtService.GenerateToken(user, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var expirationMinutes = _jwtService.GetTokenExpirationMinutes();

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return new TokenResponseDto
        {
            AccessToken = token,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = expirationMinutes * 60,
            Username = user.FullName,
            Role = roles.FirstOrDefault() ?? AppRoles.Customer
        };
    }

    private async Task EnsureRoleExistsAsync(string roleCode, string displayName)
    {
        var normalizedRoleCode = _roleManager.NormalizeKey(roleCode);
        var role = await _roleManager.FindByNameAsync(roleCode);
        role ??= await _roleManager.Roles.FirstOrDefaultAsync(r => r.Name == roleCode);

        if (role == null)
        {
            var createResult = await _roleManager.CreateAsync(new ApplicationRole
            {
                Name = roleCode,
                DisplayName = displayName
            });

            if (createResult.Succeeded)
            {
                return;
            }

            role = await _roleManager.Roles.FirstOrDefaultAsync(r => r.Name == roleCode);
            if (role == null)
            {
                throw new InvalidOperationException(
                    $"Unable to create role '{roleCode}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        var changed = false;
        if (role.DisplayName != displayName)
        {
            role.DisplayName = displayName;
            changed = true;
        }

        if (role.NormalizedName != normalizedRoleCode)
        {
            role.NormalizedName = normalizedRoleCode;
            changed = true;
        }

        if (changed)
        {
            var updateResult = await _roleManager.UpdateAsync(role);
            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to update role '{roleCode}': {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
            }
        }
    }

    private async Task<Google.Apis.Auth.GoogleJsonWebSignature.Payload?> VerifyGoogleTokenAsync(string idToken)
    {
        try
        {
            var googleClientId = _configuration["Authentication:Google:ClientId"];
            if (string.IsNullOrWhiteSpace(googleClientId))
            {
                return null;
            }

            var settings = new Google.Apis.Auth.GoogleJsonWebSignature.ValidationSettings()
            {
                Audience = new List<string>() { googleClientId }
            };
            var payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return payload;
        }
        catch
        {
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

            var debugTokenUrl = $"https://graph.facebook.com/debug_token?input_token={token}&access_token={appId}|{appSecret}";
            var debugResponse = await httpClient.GetAsync(debugTokenUrl);
            if (!debugResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var debugContent = await debugResponse.Content.ReadAsStringAsync();
            var debugResult = System.Text.Json.JsonSerializer.Deserialize<FacebookDebugTokenResponse>(debugContent);

            if (debugResult?.Data == null || !debugResult.Data.IsValid || debugResult.Data.AppId != appId)
            {
                return null;
            }

            var response = await httpClient.GetAsync($"https://graph.facebook.com/me?fields=id,name,email&access_token={token}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<FacebookUserDto>(content);
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
        public FacebookDebugTokenData Data { get; set; } = null!;
    }

    private class FacebookDebugTokenData
    {
        [System.Text.Json.Serialization.JsonPropertyName("app_id")]
        public string AppId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("is_valid")]
        public bool IsValid { get; set; }
    }
}
