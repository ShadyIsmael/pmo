using System.Text.Json.Serialization;

namespace NEXUS.Modules.Identity.Application;

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public CurrentUserDto User { get; set; } = null!;
    public bool PasswordChangeRequired { get; set; }
}

public sealed record ChangeInitialPasswordCommand(string CurrentPassword, string NewPassword);

public sealed record ChangeInitialPasswordResponseDto(bool ReauthenticationRequired = true);
