using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NEXUS.Api.Security;
using NEXUS.Modules.Identity.Api;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Api.Authorization;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class AuthCookieContractTests
{
    private const string RefreshCookieName = "__Host-nexus-refresh";
    private const string RefreshSecret = "refresh-secret-for-contract-test";

    [Fact]
    public void Browser_antiforgery_registration_matches_the_client_header_and_uses_a_secure_host_cookie()
    {
        var services = new ServiceCollection();
        services.AddBrowserCsrfProtection();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        Assert.Equal("X-Nexus-CSRF", options.HeaderName);
        Assert.Equal("__Host-nexus-csrf", options.Cookie.Name);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Strict, options.Cookie.SameSite);
        Assert.Equal("/", options.Cookie.Path);
        Assert.Null(options.Cookie.Domain);
    }

    [Fact]
    public async Task Login_sets_a_secure_httponly_host_cookie_and_redacts_refresh_secret_from_json()
    {
        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(service => service.LoginAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Success(AuthResponse(RefreshSecret)));
        var context = BrowserRequestContext();
        var controller = CreateController(authentication.Object, context);

        var actionResult = await controller.Login(
            new LoginCommand("admin@example.test", "test-password", Guid.NewGuid().ToString()),
            ValidAntiforgery().Object,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<LoginResponseDto>(ok.Value);
        Assert.Null(body.RefreshToken);
        Assert.DoesNotContain(RefreshSecret, JsonSerializer.Serialize(body), StringComparison.Ordinal);
        Assert.DoesNotContain("refreshToken", JsonSerializer.Serialize(body), StringComparison.OrdinalIgnoreCase);

        var cookie = Assert.Single(context.Response.Headers.SetCookie.ToString().Split(','));
        Assert.Contains($"{RefreshCookieName}=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-store", context.Response.Headers.CacheControl.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cookie_login_returns_server_error_when_success_has_no_refresh_token()
    {
        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(service => service.LoginAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Success(AuthResponse(null)));
        var context = BrowserRequestContext();
        var controller = CreateController(authentication.Object, context);

        var result = await controller.Login(
            new LoginCommand("admin@example.test", "test-password", Guid.NewGuid().ToString()),
            ValidAntiforgery().Object,
            CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, error.StatusCode);
        Assert.Equal("The authentication session could not be established.", Assert.IsType<ProblemDetails>(error.Value).Title);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task Refresh_uses_only_the_host_cookie_and_rotates_it_without_disclosing_the_secret()
    {
        var rotatedSecret = "rotated-refresh-secret-for-contract-test";
        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(service => service.RefreshAsync(RefreshSecret, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Success(AuthResponse(rotatedSecret)));
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{RefreshCookieName}={RefreshSecret}";
        context.Request.Headers["X-Nexus-Auth-Mode"] = "cookie";
        var controller = CreateController(authentication.Object, context);

        var actionResult = await controller.Refresh(
            new RefreshTokenRequest("body-token-must-be-ignored"),
            ValidAntiforgery().Object,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<LoginResponseDto>(ok.Value);
        Assert.Null(body.RefreshToken);
        Assert.DoesNotContain(rotatedSecret, JsonSerializer.Serialize(body), StringComparison.Ordinal);
        Assert.DoesNotContain("refreshToken", JsonSerializer.Serialize(body), StringComparison.OrdinalIgnoreCase);
        authentication.Verify(service => service.RefreshAsync(RefreshSecret, It.IsAny<CancellationToken>()), Times.Once);

        var cookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{RefreshCookieName}={rotatedSecret}", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        authentication.Verify(service => service.RefreshAsync("body-token-must-be-ignored", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cookie_refresh_returns_server_error_when_success_has_no_rotated_refresh_token()
    {
        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(service => service.RefreshAsync(RefreshSecret, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Success(AuthResponse(null)));
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{RefreshCookieName}={RefreshSecret}";
        context.Request.Headers["X-Nexus-Auth-Mode"] = "cookie";
        var controller = CreateController(authentication.Object, context);

        var result = await controller.Refresh(
            null,
            ValidAntiforgery().Object,
            CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, error.StatusCode);
        Assert.Equal("The authentication session could not be refreshed.", Assert.IsType<ProblemDetails>(error.Value).Title);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task Logout_revokes_the_cookie_session_and_expires_the_refresh_cookie()
    {
        var userId = Guid.NewGuid();
        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(service => service.LogoutCookieSessionAsync(RefreshSecret, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                authenticationType: "test"))
        };
        context.Request.Headers.Cookie = $"{RefreshCookieName}={RefreshSecret}";
        context.Request.Headers["X-Nexus-Auth-Mode"] = "cookie";
        var controller = CreateController(authentication.Object, context);

        var actionResult = await controller.Logout(
            new LogoutRequest("body-token-must-be-ignored"),
            ValidAntiforgery().Object,
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(actionResult);
        authentication.Verify(service => service.LogoutCookieSessionAsync(RefreshSecret, It.IsAny<CancellationToken>()), Times.Once);
        var cookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{RefreshCookieName}=", cookie, StringComparison.Ordinal);
        Assert.Contains("max-age=0", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cookie_logout_revokes_and_expires_session_when_access_token_is_expired_or_absent()
    {
        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(service => service.LogoutCookieSessionAsync(RefreshSecret, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{RefreshCookieName}={RefreshSecret}";
        context.Request.Headers["X-Nexus-Auth-Mode"] = "cookie";
        var controller = CreateController(authentication.Object, context);

        var result = await controller.Logout(new LogoutRequest("ignored-body-token"), ValidAntiforgery().Object, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        authentication.Verify(service => service.LogoutCookieSessionAsync(RefreshSecret, It.IsAny<CancellationToken>()), Times.Once);
        authentication.Verify(service => service.LogoutAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("max-age=0", context.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cookie_mode_rejects_invalid_anti_forgery_and_csrf_token_is_not_cached()
    {
        var authentication = new Mock<IAuthenticationService>();
        var context = BrowserRequestContext();
        var antiforgery = InvalidAntiforgery();
        var controller = CreateController(authentication.Object, context);

        var result = await controller.Login(
            new LoginCommand("admin@example.test", "test-password", Guid.NewGuid().ToString()),
            antiforgery.Object,
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        authentication.Verify(service => service.LoginAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()), Times.Never);

        var csrfContext = new DefaultHttpContext();
        var csrfController = CreateController(Mock.Of<IAuthenticationService>(), csrfContext);
        var csrfResult = csrfController.GetCsrfToken(ValidAntiforgery(csrfContext).Object);

        var ok = Assert.IsType<OkObjectResult>(csrfResult);
        Assert.Equal("request-csrf-token", ok.Value!.GetType().GetProperty("csrfToken")!.GetValue(ok.Value));
        Assert.Contains("no-store", csrfContext.Response.Headers.CacheControl.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-cache", csrfContext.Response.Headers.Pragma.ToString());
    }

    [Fact]
    public async Task Legacy_clients_keep_refresh_and_logout_token_body_contract_without_browser_cookies()
    {
        var legacyRefresh = "legacy-refresh-token";
        var rotatedRefresh = "legacy-rotated-refresh-token";
        var userId = Guid.NewGuid();
        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(service => service.LoginAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Success(AuthResponse(legacyRefresh)));
        authentication
            .Setup(service => service.RefreshAsync(legacyRefresh, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Success(AuthResponse(rotatedRefresh)));
        authentication
            .Setup(service => service.LogoutAsync(userId, rotatedRefresh, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var context = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString())],
                authenticationType: "test"))
        };
        var controller = CreateController(authentication.Object, context);

        var loginResult = await controller.Login(
            new LoginCommand("admin@example.test", "test-password", Guid.NewGuid().ToString()),
            ValidAntiforgery().Object,
            CancellationToken.None);
        var loginBody = Assert.IsType<LoginResponseDto>(Assert.IsType<OkObjectResult>(loginResult).Value);
        Assert.Equal(legacyRefresh, loginBody.RefreshToken);
        Assert.Contains(legacyRefresh, JsonSerializer.Serialize(loginBody), StringComparison.Ordinal);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));

        var refreshResult = await controller.Refresh(new RefreshTokenRequest(legacyRefresh), ValidAntiforgery().Object, CancellationToken.None);
        var refreshedBody = Assert.IsType<LoginResponseDto>(Assert.IsType<OkObjectResult>(refreshResult).Value);
        Assert.Equal(rotatedRefresh, refreshedBody.RefreshToken);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));

        var logoutResult = await controller.Logout(new LogoutRequest(rotatedRefresh), ValidAntiforgery().Object, CancellationToken.None);
        Assert.IsType<OkObjectResult>(logoutResult);
        authentication.Verify(service => service.LogoutAsync(userId, rotatedRefresh, It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Theory]
    [InlineData("Origin")]
    [InlineData("Sec-Fetch-Site")]
    public async Task Browser_originated_auth_post_without_mode_is_rejected(string browserHeader)
    {
        var authentication = new Mock<IAuthenticationService>();
        var context = new DefaultHttpContext();
        context.Request.Headers[browserHeader] = browserHeader == "Origin" ? "https://app.example.test" : "same-origin";
        var controller = CreateController(authentication.Object, context);

        var result = await controller.Login(
            new LoginCommand("admin@example.test", "test-password", Guid.NewGuid().ToString()),
            ValidAntiforgery().Object,
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        authentication.Verify(service => service.LoginAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unrecognized_auth_mode_is_rejected_without_legacy_fallback()
    {
        var authentication = new Mock<IAuthenticationService>();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Nexus-Auth-Mode"] = "unknown";
        var controller = CreateController(authentication.Object, context);

        var result = await controller.Login(
            new LoginCommand("admin@example.test", "test-password", Guid.NewGuid().ToString()),
            ValidAntiforgery().Object,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        authentication.Verify(service => service.LoginAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AuthController CreateController(IAuthenticationService authentication, HttpContext context) => new(
        authentication,
        Options.Create(new LocalPasswordPolicyOptions()),
        new ConfigurationBuilder().Build())
    {
        ControllerContext = new ControllerContext { HttpContext = context }
    };

    private static DefaultHttpContext BrowserRequestContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Nexus-Auth-Mode"] = "cookie";
        return context;
    }

    private static Mock<IAntiforgery> ValidAntiforgery(HttpContext? context = null)
    {
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery
            .Setup(service => service.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .Returns(Task.CompletedTask);
        if (context is not null)
        {
            antiforgery
                .Setup(service => service.GetAndStoreTokens(context))
                .Returns(new AntiforgeryTokenSet("request-csrf-token", "cookie-csrf-token", "__RequestVerificationToken", "X-Nexus-CSRF"));
        }
        return antiforgery;
    }

    private static Mock<IAntiforgery> InvalidAntiforgery()
    {
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery
            .Setup(service => service.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .ThrowsAsync(new AntiforgeryValidationException("Invalid CSRF token"));
        return antiforgery;
    }

    private static LoginResponseDto AuthResponse(string? refreshToken) => new()
    {
        Token = "access-jwt-for-contract-test",
        RefreshToken = refreshToken,
        ExpiresAt = DateTime.UtcNow.AddMinutes(5),
        User = new CurrentUserDto
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.test",
            FirstName = "Test",
            LastName = "Admin",
            TenantId = Guid.NewGuid().ToString(),
            AuthenticationSource = AuthenticationSource.Local
        },
        PasswordChangeRequired = false
    };
}
