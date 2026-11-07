using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Identity.Application;

namespace NEXUS.Modules.Identity.Api;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string AuthModeHeaderName = "X-Nexus-Auth-Mode";
    private const string CookieAuthMode = "cookie";
    private const string RefreshCookieName = "__Host-nexus-refresh";
    private readonly IAuthenticationService _authenticationService;
    private readonly LocalPasswordPolicyOptions _passwordPolicy;
    private readonly IConfiguration _configuration;

    public AuthController(
        IAuthenticationService authenticationService,
        IOptions<LocalPasswordPolicyOptions> passwordPolicy,
        IConfiguration configuration)
    {
        _authenticationService = authenticationService;
        _passwordPolicy = passwordPolicy.Value;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpGet("csrf")]
    public IActionResult GetCsrfToken([FromServices] IAntiforgery antiforgery)
    {
        DisableCaching();
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { csrfToken = tokens.RequestToken });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        [FromServices] IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        DisableCaching();
        if (!TryGetAuthMode(out var cookieMode)) return UnsupportedAuthMode();
        if (cookieMode && !await ValidateAntiforgeryRequestAsync(antiforgery)) return InvalidCsrfToken();

        var result = await _authenticationService.LoginAsync(command, cancellationToken);
        if (result.IsFailure)
            return StatusCode(result.Error?.StatusCode ?? StatusCodes.Status401Unauthorized, AuthProblemDetails.ToProblem(result.Error));

        if (cookieMode)
        {
            if (!TrySetRefreshCookie(result.Value.RefreshToken))
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "The authentication session could not be established.",
                    Status = StatusCodes.Status500InternalServerError
                });

            // Browser clients receive the refresh credential only in the HttpOnly cookie.
            result.Value.RefreshToken = null;
        }

        return Ok(result.Value);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest? request,
        [FromServices] IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        DisableCaching();
        if (!TryGetAuthMode(out var cookieMode)) return UnsupportedAuthMode();

        var refreshToken = cookieMode
            ? Request.Cookies[RefreshCookieName]
            : request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken)) return Unauthorized();
        if (cookieMode && !await ValidateAntiforgeryRequestAsync(antiforgery)) return InvalidCsrfToken();

        var result = await _authenticationService.RefreshAsync(refreshToken, cancellationToken);
        if (result.IsFailure)
            return StatusCode(result.Error?.StatusCode ?? StatusCodes.Status401Unauthorized, AuthProblemDetails.ToProblem(result.Error));

        if (cookieMode)
        {
            if (!TrySetRefreshCookie(result.Value.RefreshToken))
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "The authentication session could not be refreshed.",
                    Status = StatusCodes.Status500InternalServerError
                });

            result.Value.RefreshToken = null;
        }

        return Ok(result.Value);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? request,
        [FromServices] IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        DisableCaching();
        if (!TryGetAuthMode(out var cookieMode)) return UnsupportedAuthMode();
        if (cookieMode && !await ValidateAntiforgeryRequestAsync(antiforgery)) return InvalidCsrfToken();

        var refreshToken = cookieMode ? Request.Cookies[RefreshCookieName] : request?.RefreshToken;
        if (cookieMode)
        {
            var result = await _authenticationService.LogoutCookieSessionAsync(refreshToken, cancellationToken);
            if (result.IsFailure)
                return StatusCode(result.Error?.StatusCode ?? StatusCodes.Status400BadRequest, AuthProblemDetails.ToProblem(result.Error));
            DeleteRefreshCookie();
            return Ok(new { message = "Logged out successfully" });
        }

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId == Guid.Empty)
            return Unauthorized();

        await _authenticationService.LogoutAsync(userId, refreshToken, cancellationToken);
        return Ok(new { message = "Logged out successfully" });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        DisableCaching();
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId == Guid.Empty
            || !Guid.TryParse(User.FindFirstValue("tenantId"), out var parsedTenantId) || parsedTenantId == Guid.Empty)
            return Unauthorized();

        var tenantId = (Guid?)parsedTenantId;
        var result = await _authenticationService.GetCurrentUserAsync(userId, tenantId, cancellationToken);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = result.Error?.Message, Status = 404 });
        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost("change-initial-password")]
    public async Task<IActionResult> ChangeInitialPassword([FromBody] ChangeInitialPasswordCommand command, CancellationToken cancellationToken)
    {
        DisableCaching();
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
    public IActionResult GetInitialPasswordPolicy()
    {
        DisableCaching();
        return Ok(new
        {
            minimumLength = _passwordPolicy.MinimumLength,
            maximumLength = _passwordPolicy.MaximumLength
        });
    }

    [Authorize]
    [HttpPost("switch-role")]
    public async Task<IActionResult> SwitchRole([FromBody] SwitchRoleRequest request, CancellationToken cancellationToken)
    {
        DisableCaching();
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId == Guid.Empty
            || !Guid.TryParse(User.FindFirstValue("tenantId"), out var tenantId) || tenantId == Guid.Empty)
            return Unauthorized();

        var result = await _authenticationService.SwitchRoleAsync(userId, tenantId, request.RoleName, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = result.Error?.Message, Status = 400 });
        return Ok(result.Value);
    }

    private bool TryGetAuthMode(out bool cookieMode)
    {
        cookieMode = false;
        if (Request.Headers.TryGetValue(AuthModeHeaderName, out var values))
        {
            if (values.Count != 1 || !string.Equals(values[0], CookieAuthMode, StringComparison.Ordinal))
                return false;
            cookieMode = true;
            return true;
        }

        // Browser POSTs carry Origin and, in modern browsers, Sec-Fetch-Site. They must
        // opt into the cookie+CSRF contract; otherwise XSS could request the legacy
        // JSON refresh-token response by omitting the mode header.
        return !Request.Headers.ContainsKey("Origin") && !Request.Headers.ContainsKey("Sec-Fetch-Site");
    }

    private async Task<bool> ValidateAntiforgeryRequestAsync(IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private IActionResult UnsupportedAuthMode() => BadRequest(new ProblemDetails
    {
        Title = "A supported authentication mode is required for this request.",
        Status = StatusCodes.Status400BadRequest
    });

    private IActionResult InvalidCsrfToken() => BadRequest(new ProblemDetails
    {
        Title = "The anti-forgery token is missing or invalid.",
        Status = StatusCodes.Status400BadRequest
    });

    private bool TrySetRefreshCookie(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return false;

        var configuredDays = _configuration.GetValue<int?>("Jwt:RefreshTokenLifetimeDays");
        var lifetimeDays = configuredDays is > 0 ? configuredDays.Value : 7;
        // Strict is intentional: the supported browser deployment serves the SPA and API
        // on the same site. Cross-site API tunnels must use a same-site proxy to support
        // this session contract; do not relax this to None as a tunnel workaround.
        Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(lifetimeDays)
        });
        return true;
    }

    private void DeleteRefreshCookie() => Response.Cookies.Delete(RefreshCookieName, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true
    });

    private void DisableCaching()
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
    }
}

internal static class AuthProblemDetails
{
    public static ProblemDetails ToProblem(NEXUS.SharedKernel.Error? error)
    {
        var message = error?.Message ?? "The request could not be completed.";
        var problem = new ProblemDetails
        {
            Title = message,
            Detail = message,
            Status = error?.StatusCode ?? StatusCodes.Status400BadRequest
        };
        if (!string.IsNullOrWhiteSpace(error?.Code))
            problem.Extensions["code"] = error.Code;
        return problem;
    }
}

public record SwitchRoleRequest(string RoleName);
public record LogoutRequest(string? RefreshToken = null);
