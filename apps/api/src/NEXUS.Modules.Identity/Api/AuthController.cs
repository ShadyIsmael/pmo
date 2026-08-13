using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Identity.Application;

namespace NEXUS.Modules.Identity.Api;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;

    public AuthController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _authenticationService.LoginAsync(command, cancellationToken);
        if (result.IsFailure)
            return Unauthorized(new ProblemDetails { Title = result.Error?.Message, Status = result.Error?.StatusCode });
        return Ok(result.Value);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authenticationService.RefreshAsync(request.RefreshToken, cancellationToken);
        if (result.IsFailure)
            return Unauthorized(new ProblemDetails { Title = result.Error?.Message, Status = result.Error?.StatusCode });
        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _authenticationService.GetCurrentUserAsync(userId, cancellationToken);
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

public record SwitchRoleRequest(string RoleName);

public record LogoutRequest(string? RefreshToken);
