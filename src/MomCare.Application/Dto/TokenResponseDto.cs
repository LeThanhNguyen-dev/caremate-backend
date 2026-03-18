namespace MomCare.Dto;

public class TokenResponseDto
{
    public required string AccessToken { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public required string Username { get; set; }
    public required string Role { get; set; }
    public required string RefreshToken { get; set; }
}
