using MomCare.Models;
using System.Security.Claims;

namespace MomCare.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user, List<string> roleNames);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string? token);
    int GetTokenExpirationMinutes();
}
