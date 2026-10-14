using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NEXUS.Api.Management;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class ManagementContextMiddlewareTests
{
    [Fact]
    public async Task Timer_Allows_Stale_Native_Management_Context()
    {
        var nextCalled = false;
        var middleware = new ManagementContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var resolver = new Mock<IManagementScopeResolver>();
        resolver
            .Setup(item => item.ResolveAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<ManagementPerspective?>(),
                It.IsAny<ManagementViewMode>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ManagementContext?)null);
        var accessor = new Mock<IManagementContextAccessor>();
        var httpContext = CreateAuthenticatedContext("/api/timer/start");
        httpContext.Request.Headers[ManagementContextMiddleware.PerspectiveHeader] = "PortfolioManager";
        httpContext.Request.Headers[ManagementContextMiddleware.ViewModeHeader] = "Native";

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Timer_Does_Not_Bypass_ReadOnly_ViewAs()
    {
        var nextCalled = false;
        var middleware = new ManagementContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var resolver = new Mock<IManagementScopeResolver>();
        resolver
            .Setup(item => item.ResolveAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<ManagementPerspective?>(),
                It.IsAny<ManagementViewMode>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ManagementContext?)null);
        var accessor = new Mock<IManagementContextAccessor>();
        var httpContext = CreateAuthenticatedContext("/api/timer/start");
        httpContext.Request.Headers[ManagementContextMiddleware.PerspectiveHeader] = "ProjectManager";
        httpContext.Request.Headers[ManagementContextMiddleware.ViewModeHeader] = "ReadOnlyViewAs";

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task No_Perspective_Header_And_No_Assignments_Sets_FailClosed_Empty_Scope_Context()
    {
        var nextCalled = false;
        var middleware = new ManagementContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var emptyScope = ManagementScope.Empty;
        var resolver = new Mock<IManagementScopeResolver>();
        resolver
            .Setup(item => item.ResolveAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<ManagementPerspective?>(),
                It.IsAny<ManagementViewMode>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ManagementContext?)null);
        resolver
            .Setup(item => item.GetPerspectiveSnapshotAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<ManagementContext?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManagementPerspectiveSnapshot(
                Array.Empty<ManagementPerspective>(),
                Array.Empty<ManagementPerspectiveAvailability>(),
                null,
                false,
                emptyScope,
                null));
        ManagementContext? capturedContext = null;
        var accessor = new Mock<IManagementContextAccessor>();
        accessor
            .Setup(item => item.Set(It.IsAny<ManagementContext>()))
            .Callback<ManagementContext>(value => capturedContext = value);
        var httpContext = CreateAuthenticatedContext("/api/projects");

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance);

        Assert.True(nextCalled);
        Assert.NotNull(capturedContext);
        Assert.Empty(capturedContext!.Scope.ProjectIds);
        Assert.Empty(capturedContext.Scope.PortfolioIds);
        Assert.Empty(capturedContext.Scope.ProgramIds);
        resolver.Verify(item => item.GetPerspectiveSnapshotAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<ManagementContext?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task No_Perspective_Header_But_Resolver_Returns_Context_Passes_It_Through()
    {
        var nextCalled = false;
        var middleware = new ManagementContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var existingContext = new ManagementContext(
            Guid.NewGuid(), Guid.NewGuid(), ManagementPerspective.ProjectManager,
            ManagementViewMode.Native, null, null, null, ManagementScope.Empty);
        var resolver = new Mock<IManagementScopeResolver>();
        resolver
            .Setup(item => item.ResolveAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<ManagementPerspective?>(),
                It.IsAny<ManagementViewMode>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContext);
        ManagementContext? capturedContext = null;
        var accessor = new Mock<IManagementContextAccessor>();
        accessor
            .Setup(item => item.Set(It.IsAny<ManagementContext>()))
            .Callback<ManagementContext>(value => capturedContext = value);
        var httpContext = CreateAuthenticatedContext("/api/projects");

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance);

        Assert.True(nextCalled);
        Assert.Same(existingContext, capturedContext);
        resolver.Verify(item => item.GetPerspectiveSnapshotAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<ManagementContext?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Tenant_admin_flag_is_set_only_for_whitelisted_get_read_routes()
    {
        var nextCalled = false;
        var middleware = new ManagementContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var resolver = new Mock<IManagementScopeResolver>();
        resolver.Setup(item => item.IsTenantAdministratorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        resolver.Setup(item => item.GetPerspectiveSnapshotAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ManagementContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManagementPerspectiveSnapshot(
                Array.Empty<ManagementPerspective>(),
                Array.Empty<ManagementPerspectiveAvailability>(),
                null,
                false,
                ManagementScope.Empty,
                null));
        ManagementContext? capturedContext = null;
        var accessor = new Mock<IManagementContextAccessor>();
        accessor.Setup(item => item.Set(It.IsAny<ManagementContext>())).Callback<ManagementContext>(value => capturedContext = value);
        var httpContext = CreateAuthenticatedContext("/api/program-management/programs");
        httpContext.Request.Method = HttpMethods.Get;

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance);

        Assert.True(nextCalled);
        Assert.True(capturedContext?.CanReadAllTenantPortfolioProgramData);
    }

    [Fact]
    public async Task Tenant_admin_flag_does_not_apply_to_unlisted_resource_routes()
    {
        var nextCalled = false;
        var middleware = new ManagementContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var resolver = new Mock<IManagementScopeResolver>();
        resolver.Setup(item => item.IsTenantAdministratorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        resolver.Setup(item => item.GetPerspectiveSnapshotAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ManagementContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManagementPerspectiveSnapshot(
                Array.Empty<ManagementPerspective>(),
                Array.Empty<ManagementPerspectiveAvailability>(),
                null,
                false,
                ManagementScope.Empty,
                null));
        var accessor = new Mock<IManagementContextAccessor>();
        var resourceId = Guid.NewGuid();
        var httpContext = CreateAuthenticatedContext($"/api/program-management/programs/{resourceId}/financials");
        httpContext.Request.Method = HttpMethods.Get;

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        resolver.Verify(item => item.IsTenantAdministratorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Tenant_admin_write_scope_allows_only_native_portfolio_update_route()
    {
        var nextCalled = false;
        var middleware = new ManagementContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var resolver = new Mock<IManagementScopeResolver>();
        resolver.Setup(item => item.IsTenantAdministratorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        resolver.Setup(item => item.GetPerspectiveSnapshotAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ManagementContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManagementPerspectiveSnapshot(
                Array.Empty<ManagementPerspective>(),
                Array.Empty<ManagementPerspectiveAvailability>(),
                null,
                false,
                ManagementScope.Empty,
                null));
        ManagementContext? capturedContext = null;
        var accessor = new Mock<IManagementContextAccessor>();
        accessor.Setup(item => item.Set(It.IsAny<ManagementContext>())).Callback<ManagementContext>(value => capturedContext = value);
        var portfolioId = Guid.NewGuid();
        var httpContext = CreateAuthenticatedContext($"/api/portfolios/{portfolioId}");
        httpContext.Request.Method = HttpMethods.Put;
        httpContext.Request.Headers[ManagementContextMiddleware.PerspectiveHeader] = "PortfolioManager";
        httpContext.Request.Headers[ManagementContextMiddleware.ViewModeHeader] = "Native";

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance);

        Assert.True(nextCalled);
        Assert.True(capturedContext?.HasTenantAdministratorPortfolioWriteScope);
        Assert.False(capturedContext?.CanReadAllTenantPortfolioProgramData);
    }

    [Fact]
    public async Task Non_admin_without_portfolio_assignment_cannot_use_tenant_admin_write_scope()
    {
        var nextCalled = false;
        var middleware = new ManagementContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var resolver = new Mock<IManagementScopeResolver>();
        resolver.Setup(item => item.IsTenantAdministratorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        resolver.Setup(item => item.GetPerspectiveSnapshotAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ManagementContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManagementPerspectiveSnapshot(
                Array.Empty<ManagementPerspective>(),
                Array.Empty<ManagementPerspectiveAvailability>(),
                null,
                false,
                ManagementScope.Empty,
                null));
        var accessor = new Mock<IManagementContextAccessor>();
        var httpContext = CreateAuthenticatedContext($"/api/portfolios/{Guid.NewGuid()}");
        httpContext.Request.Method = HttpMethods.Put;

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Tenant_admin_write_scope_does_not_cover_archive_or_view_as_requests()
    {
        foreach (var (path, method, viewMode, expectedStatus) in new[]
        {
            ($"/api/portfolios/{Guid.NewGuid()}/archive", HttpMethods.Post, "Native", StatusCodes.Status404NotFound),
            ($"/api/portfolios/{Guid.NewGuid()}", HttpMethods.Put, "ReadOnlyViewAs", StatusCodes.Status403Forbidden)
        })
        {
            var nextCalled = false;
            var middleware = new ManagementContextMiddleware(_ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
            var resolver = new Mock<IManagementScopeResolver>();
            resolver.Setup(item => item.IsTenantAdministratorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            resolver.Setup(item => item.GetPerspectiveSnapshotAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ManagementContext?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ManagementPerspectiveSnapshot(
                    Array.Empty<ManagementPerspective>(),
                    Array.Empty<ManagementPerspectiveAvailability>(),
                    null,
                    false,
                    ManagementScope.Empty,
                    null));
            var accessor = new Mock<IManagementContextAccessor>();
            var httpContext = CreateAuthenticatedContext(path);
            httpContext.Request.Method = method;
            httpContext.Request.Headers[ManagementContextMiddleware.ViewModeHeader] = viewMode;
            if (viewMode == "ReadOnlyViewAs")
                httpContext.Request.Headers[ManagementContextMiddleware.PerspectiveHeader] = "PortfolioManager";

            await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance);

            Assert.False(nextCalled);
            Assert.Equal(expectedStatus, httpContext.Response.StatusCode);
            resolver.Verify(item => item.IsTenantAdministratorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    private static DefaultHttpContext CreateAuthenticatedContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("tenantId", Guid.NewGuid().ToString())
        }, "test"));
        return context;
    }
}
