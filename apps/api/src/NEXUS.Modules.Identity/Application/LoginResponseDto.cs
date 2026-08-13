namespace NEXUS.Modules.Identity.Application;

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public CurrentUserDto User { get; set; } = null!;
}
