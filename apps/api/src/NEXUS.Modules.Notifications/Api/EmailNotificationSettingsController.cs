using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NEXUS.Modules.Notifications.Application;

namespace NEXUS.Modules.Notifications.Api;

[ApiController]
[Route("api/notifications/email-settings")]
[Authorize]
public sealed class EmailNotificationSettingsController(IEmailNotificationSettingsService settings) : ControllerBase
{
    private readonly IEmailNotificationSettingsService _settings = settings;

    [HttpGet("access")]
    public async Task<IActionResult> GetAccess(CancellationToken cancellationToken) =>
        await ExecuteAsync(() => _settings.GetAccessAsync(cancellationToken));

    [HttpGet("{scope}")]
    public Task<IActionResult> GetSettings(string scope, [FromQuery] Guid? organizationId, CancellationToken cancellationToken) =>
        ExecuteScopeAsync(scope, parsed => _settings.GetSettingsAsync(parsed, organizationId, cancellationToken));

    [HttpPut("{scope}")]
    public Task<IActionResult> UpdateSettings(
        string scope,
        [FromQuery] Guid? organizationId,
        [FromBody] UpdateEmailNotificationProfileRequest request,
        CancellationToken cancellationToken) =>
        ExecuteScopeAsync(scope, parsed => _settings.UpdateSettingsAsync(parsed, organizationId, request, cancellationToken));

    [HttpPost("{scope}/reset")]
    public Task<IActionResult> ResetSettings(
        string scope,
        [FromQuery] Guid? organizationId,
        [FromBody] ResetEmailNotificationProfileRequest request,
        CancellationToken cancellationToken) =>
        ExecuteScopeAsync(scope, parsed => _settings.ResetSettingsAsync(parsed, organizationId, request.ExpectedVersion, cancellationToken));

    [HttpPost("{scope}/test-connection")]
    [EnableRateLimiting("EmailNotificationTest")]
    public Task<IActionResult> TestConnection(
        string scope,
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken) =>
        ExecuteScopeAsync(scope, parsed => _settings.TestConnectionAsync(parsed, organizationId, cancellationToken));

    [HttpPost("{scope}/send-test")]
    [EnableRateLimiting("EmailNotificationTest")]
    public Task<IActionResult> SendTestEmail(
        string scope,
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken) =>
        ExecuteScopeAsync(scope, parsed => _settings.SendTestEmailAsync(parsed, organizationId, cancellationToken));

    [HttpGet("{scope}/logs")]
    public Task<IActionResult> GetLogs(
        string scope,
        [FromQuery] Guid? organizationId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) =>
        ExecuteScopeAsync(scope, parsed => _settings.GetLogsAsync(parsed, organizationId, limit, cancellationToken));

    [HttpPost("{scope}/trusted-relay-check")]
    public Task<IActionResult> CheckTrustedRelay(
        string scope,
        [FromQuery] Guid? organizationId,
        [FromBody] EmailNotificationTrustedRelayCheckRequest request,
        CancellationToken cancellationToken) =>
        ExecuteScopeAsync(scope, parsed => _settings.CheckTrustedRelayAsync(parsed, organizationId, request.Host, cancellationToken));

    private async Task<IActionResult> ExecuteScopeAsync<T>(string scope, Func<EmailNotificationProfileScope, Task<T>> operation)
    {
        if (!TryParseScope(scope, out var parsed))
            return ProblemResult(400, "invalidEmailNotificationScope", "The requested email profile scope is invalid.");
        return await ExecuteAsync(() => operation(parsed));
    }

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return Ok(await operation());
        }
        catch (EmailNotificationSettingsException exception)
        {
            return ProblemResult(exception.StatusCode, exception.Code, exception.Message);
        }
        catch (EmailNotificationProfileConcurrencyException exception)
        {
            return ProblemResult(StatusCodes.Status409Conflict, "profileConcurrencyConflict", exception.Message);
        }
    }

    private IActionResult ProblemResult(int statusCode, string code, string detail) =>
        StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status400BadRequest => "Invalid email notification settings",
                StatusCodes.Status401Unauthorized => "Authentication required",
                StatusCodes.Status403Forbidden => "Email notification access denied",
                StatusCodes.Status404NotFound => "Email notification profile not found",
                StatusCodes.Status409Conflict => "Email notification settings changed",
                StatusCodes.Status503ServiceUnavailable => "Email notification settings unavailable",
                _ => "Email notification operation failed"
            },
            Detail = detail,
            Extensions = { ["code"] = code, ["traceId"] = HttpContext.TraceIdentifier }
        });

    private static bool TryParseScope(string value, out EmailNotificationProfileScope scope) =>
        Enum.TryParse(value, ignoreCase: true, out scope)
        && Enum.IsDefined(scope);
}

