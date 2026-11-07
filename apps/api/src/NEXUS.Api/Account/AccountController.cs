using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace NEXUS.Api.Account;

[ApiController]
[Route("api/account")]
[Authorize]
public sealed class AccountController(IAccountService accountService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var tenantId, out var sessionId)) return Unauthorized();
        var result = await accountService.GetAsync(userId, tenantId, sessionId, cancellationToken);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateAccountProfileRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var tenantId, out _)) return Unauthorized();
        var result = await accountService.UpdateProfileAsync(userId, tenantId, request, cancellationToken);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPost("change-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangeAccountPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var tenantId, out _)) return Unauthorized();
        var result = await accountService.ChangePasswordAsync(userId, tenantId, request,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers["User-Agent"].ToString(), cancellationToken);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var tenantId, out var sessionId)) return Unauthorized();
        var result = await accountService.GetSessionsAsync(userId, tenantId, sessionId, cancellationToken);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var tenantId, out var currentSessionId)) return Unauthorized();
        var result = await accountService.RevokeSessionAsync(userId, tenantId, currentSessionId, sessionId,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers["User-Agent"].ToString(), cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return result.Value ? Ok(new { currentSessionRevoked = true }) : NoContent();
    }

    [HttpPost("sessions/revoke-others")]
    public async Task<IActionResult> RevokeOtherSessions(CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var tenantId, out var currentSessionId)) return Unauthorized();
        var result = await accountService.RevokeOtherSessionsAsync(userId, tenantId, currentSessionId,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers["User-Agent"].ToString(), cancellationToken);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    private bool TryGetIdentity(out Guid userId, out Guid tenantId, out Guid sessionId)
    {
        userId = Guid.Empty;
        tenantId = Guid.Empty;
        sessionId = Guid.Empty;
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId)
            && Guid.TryParse(User.FindFirstValue("tenantId"), out tenantId)
            && Guid.TryParse(User.FindFirstValue("sid"), out sessionId)
            && userId != Guid.Empty && tenantId != Guid.Empty && sessionId != Guid.Empty;
    }

    private IActionResult Failure(NEXUS.SharedKernel.Error? error)
    {
        var problem = new ProblemDetails
        {
            Title = error?.Message ?? "The account request could not be completed.",
            Status = error?.StatusCode ?? StatusCodes.Status400BadRequest
        };
        if (!string.IsNullOrWhiteSpace(error?.Code)) problem.Extensions["code"] = error.Code;
        if (!string.IsNullOrWhiteSpace(error?.FieldName)) problem.Extensions["field"] = error.FieldName;
        return StatusCode(problem.Status ?? StatusCodes.Status400BadRequest, problem);
    }
}
