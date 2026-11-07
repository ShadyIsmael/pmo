using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NEXUS.Api.Middleware;
using NEXUS.Modules.Identity.Api;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class AuthenticationPipelineTests
{
    [Fact]
    public void Login_and_refresh_are_anonymous_while_me_remains_protected()
    {
        var controller = typeof(AuthController);

        Assert.NotNull(controller.GetMethod(nameof(AuthController.Login))!
            .GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(controller.GetMethod(nameof(AuthController.Refresh))!
            .GetCustomAttribute<AllowAnonymousAttribute>());

        var me = controller.GetMethod(nameof(AuthController.GetCurrentUser))!;
        Assert.NotNull(me.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(me.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public async Task Login_credential_failure_keeps_credential_detail_after_api_error_normalization()
    {
        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(service => service.LoginAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResponseDto>.Failure(Error.Unauthorized("Invalid email or password")));
        var context = new DefaultHttpContext();
        var controller = new AuthController(
            authentication.Object,
            Options.Create(new LocalPasswordPolicyOptions()),
            new ConfigurationBuilder().Build())
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var actionResult = await controller.Login(
            new LoginCommand("person@example.test", "invalid", "11111111-1111-1111-1111-111111111111"),
            Mock.Of<IAntiforgery>(),
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var problem = ApiProblemDetails.FromResponse(
            objectResult.Value,
            objectResult.StatusCode!.Value,
            "/api/auth/login",
            "trace-login");

        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
        Assert.Equal("UNAUTHORIZED", problem.Extensions["code"]);
        Assert.Equal("Invalid email or password", problem.Detail);
    }

    [Fact]
    public async Task Database_exception_remains_a_server_error_instead_of_becoming_a_session_401()
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var logger = new CapturingLogger<ApiExceptionHandler>();
        var handler = new ApiExceptionHandler(logger);
        var databaseFailure = new DbUpdateException("database unavailable");

        var handled = await handler.TryHandleAsync(
            context,
            databaseFailure,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        Assert.Equal("INTERNAL_ERROR", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(StatusCodes.Status500InternalServerError, document.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain(
            "Your session is not valid",
            document.RootElement.GetProperty("detail").GetString(),
            StringComparison.Ordinal);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && ReferenceEquals(entry.Exception, databaseFailure));
    }

    [Fact]
    public async Task Request_aborted_operation_cancellation_is_handled_without_unexpected_error_logging()
    {
        using var requestAborted = new CancellationTokenSource();
        requestAborted.Cancel();
        var context = new DefaultHttpContext { RequestAborted = requestAborted.Token };
        var logger = new CapturingLogger<ApiExceptionHandler>();
        var handler = new ApiExceptionHandler(logger);

        var handled = await handler.TryHandleAsync(
            context,
            new OperationCanceledException(requestAborted.Token),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(logger.Entries);
        Assert.False(context.Response.HasStarted);
    }

    [Fact]
    public async Task SqlClient_request_cancellation_is_handled_without_database_error_logging()
    {
        using var requestAborted = new CancellationTokenSource();
        requestAborted.Cancel();
        var context = new DefaultHttpContext { RequestAborted = requestAborted.Token };
        var logger = new CapturingLogger<ApiExceptionHandler>();
        var handler = new ApiExceptionHandler(logger);

        var handled = await handler.TryHandleAsync(
            context,
            CreateSqlException(
                (0, 11, 0, "A severe error occurred on the current command."),
                (0, 11, 0, "Operation cancelled by user.")),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(logger.Entries);
        Assert.False(context.Response.HasStarted);
    }

    [Fact]
    public async Task Real_sql_failure_remains_logged_when_client_has_disconnected()
    {
        using var requestAborted = new CancellationTokenSource();
        requestAborted.Cancel();
        var context = new DefaultHttpContext { RequestAborted = requestAborted.Token };
        var logger = new CapturingLogger<ApiExceptionHandler>();
        var handler = new ApiExceptionHandler(logger);
        var sqlFailure = CreateSqlException((208, 16, 1, "Invalid object name."));

        var handled = await handler.TryHandleAsync(context, sqlFailure, CancellationToken.None);

        Assert.True(handled);
        var logEntry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, logEntry.Level);
        Assert.Same(sqlFailure, logEntry.Exception);
        Assert.False(context.Response.HasStarted);
    }

    [Fact]
    public async Task First_authenticated_request_rebuilds_permissions_and_applies_current_deny()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var claims = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("tenantId", tenantId.ToString()),
            new Claim("sid", sessionId.ToString()),
            new Claim("permission", "Project.Create")
        ], "test");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(claims)
        };
        var sessions = new Mock<IUserSessionValidator>();
        sessions.Setup(service => service.IsActiveAsync(sessionId, userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var tenants = new Mock<ITenantRegistryStatusValidator>();
        tenants.Setup(service => service.IsActiveAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var deniedPermissions = EffectivePermissionResolver.Resolve(
            ["Project.Create"],
            [("Project.Create", PermissionOverrideEffect.Deny)]);
        var effectivePermissions = new Mock<IUserEffectivePermissionService>();
        effectivePermissions.Setup(service => service.GetEffectivePermissionsAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deniedPermissions);
        effectivePermissions.Setup(service => service.GetEffectiveRoleNamesAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        var nextInvoked = false;
        var middleware = new UserPermissionClaimsMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, effectivePermissions.Object, sessions.Object, tenants.Object);

        Assert.True(nextInvoked);
        Assert.DoesNotContain(context.User.Claims, claim => claim.Type == "permission" && claim.Value == "Project.Create");
        effectivePermissions.Verify(
            service => service.GetEffectivePermissionsAsync(userId, tenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Protected_api_request_with_an_invalid_session_is_rejected_before_endpoint_execution()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("tenantId", tenantId.ToString()),
                new Claim("sid", sessionId.ToString())
            ], "test"))
        };
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute()),
            "protected project lookup"));

        var sessions = new Mock<IUserSessionValidator>();
        sessions.Setup(service => service.IsActiveAsync(sessionId, userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var tenants = new Mock<ITenantRegistryStatusValidator>();
        var permissions = new Mock<IUserEffectivePermissionService>();
        var nextInvoked = false;
        var middleware = new UserPermissionClaimsMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, permissions.Object, sessions.Object, tenants.Object);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(nextInvoked);
        sessions.Verify(service => service.IsActiveAsync(sessionId, userId, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        tenants.Verify(service => service.IsActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        permissions.Verify(service => service.GetEffectivePermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SqlException CreateSqlException(params (int Number, byte Severity, byte State, string Message)[] errors)
    {
        var collection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        var addError = typeof(SqlErrorCollection).GetMethod(
            "Add",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        var errorConstructor = typeof(SqlError).GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types:
            [
                typeof(int),
                typeof(byte),
                typeof(byte),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(int),
                typeof(int),
                typeof(Exception)
            ],
            modifiers: null)!;

        foreach (var error in errors)
        {
            var sqlError = errorConstructor.Invoke(
            [
                error.Number,
                error.State,
                error.Severity,
                "Shady",
                error.Message,
                string.Empty,
                0,
                0,
                null
            ]);
            addError.Invoke(collection, [sqlError]);
        }

        var createException = typeof(SqlException)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == "CreateException"
                && method.GetParameters().Length == 2
                && method.GetParameters().Select(parameter => parameter.ParameterType)
                    .SequenceEqual([typeof(SqlErrorCollection), typeof(string)]));
        return (SqlException)createException.Invoke(null, [collection, "16.0"])!;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
