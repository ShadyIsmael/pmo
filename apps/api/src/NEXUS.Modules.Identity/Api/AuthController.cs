using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Identity.Application;

namespace NEXUS.Modules.Identity.Api;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly LocalPasswordPolicyOptions _passwordPolicy;

    public AuthController(IAuthenticationService authenticationService, IOptions<LocalPasswordPolicyOptions> passwordPolicy)
    {
        _authenticationService = authenticationService;
        _passwordPolicy = passwordPolicy.Value;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _authenticationService.LoginAsync(command, cancellationToken);
        if (result.IsFailure)
            return StatusCode(result.Error?.StatusCode ?? StatusCodes.Status401Unauthorized, AuthProblemDetails.ToProblem(result.Error));
        return Ok(result.Value);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authenticationService.RefreshAsync(request.RefreshToken, cancellationToken);
        if (result.IsFailure)
            return StatusCode(result.Error?.StatusCode ?? StatusCodes.Status401Unauthorized, AuthProblemDetails.ToProblem(result.Error));
        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tenantId = Guid.TryParse(User.FindFirstValue("tenantId"), out var parsedTenantId) ? parsedTenantId : (Guid?)null;
        var result = await _authenticationService.GetCurrentUserAsync(userId, tenantId, cancellationToken);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = result.Error?.Message, Status = 404 });
        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _authenticationService.LogoutAsync(userId, request?.RefreshToken, cancellationToken);
        return Ok(new { message = "Logged out successfully" });
    }

    [Authorize]
    [HttpPost("change-initial-password")]
    public async Task<IActionResult> ChangeInitialPassword([FromBody] ChangeInitialPasswordCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue("tenantId"), out var tenantId))
            return Unauthorized();

        var result = await _authenticationService.ChangeInitialPasswordAsync(userId, tenantId, command, cancellationToken);
        if (result.IsFailure)
            return StatusCode(result.Error?.StatusCode ?? StatusCodes.Status400BadRequest, AuthProblemDetails.ToProblem(result.Error));
        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("initial-password-policy")]
    public IActionResult GetInitialPasswordPolicy() => Ok(new
    {
        minimumLength = _passwordPolicy.MinimumLength,
        maximumLength = _passwordPolicy.MaximumLength
    });

    [Authorize]
    [HttpPost("switch-role")]
    public async Task<IActionResult> SwitchRole([FromBody] SwitchRoleRequest request, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _authenticationService.SwitchRoleAsync(userId, request.RoleName, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = result.Error?.Message, Status = 400 });
        return Ok(result.Value);
    }
}

internal static class AuthProblemDetails
{
    public static ProblemDetails ToProblem(NEXUS.SharedKernel.Error? error)
    {
        var problem = new ProblemDetails
        {
            Title = error?.Message ?? "The request could not be completed.",
            Status = error?.StatusCode ?? StatusCodes.Status400BadRequest
        };
        if (!string.IsNullOrWhiteSpace(error?.Code))
            problem.Extensions["code"] = error.Code;
        return problem;
    }
}

public record SwitchRoleRequest(string RoleName);

public record LogoutRequest(string? RefreshToken);
