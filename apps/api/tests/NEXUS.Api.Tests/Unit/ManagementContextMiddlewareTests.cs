using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using PortfolioProgram = NEXUS.Modules.Portfolio.Domain.Program;
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

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

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

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

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

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

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

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

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

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.True(nextCalled);
        Assert.True(capturedContext?.CanReadAllTenantPortfolioProgramData);
    }

    [Fact]
    public async Task Tenant_admin_can_read_portfolio_and_program_handover_details_in_current_tenant()
    {
        foreach (var path in new[]
        {
            $"/api/manager-handovers/portfolios/{Guid.NewGuid()}/requests",
            $"/api/manager-handovers/portfolios/{Guid.NewGuid()}/history",
            $"/api/manager-handovers/portfolios/{Guid.NewGuid()}/snapshots",
            $"/api/manager-handovers/programs/{Guid.NewGuid()}/requests",
            $"/api/manager-handovers/programs/{Guid.NewGuid()}/history",
            $"/api/manager-handovers/programs/{Guid.NewGuid()}/snapshots"
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
            ManagementContext? capturedContext = null;
            var accessor = new Mock<IManagementContextAccessor>();
            accessor.Setup(item => item.Set(It.IsAny<ManagementContext>())).Callback<ManagementContext>(value => capturedContext = value);
            var httpContext = CreateAuthenticatedContext(path);
            httpContext.Request.Method = HttpMethods.Get;

            await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

            Assert.True(nextCalled);
            Assert.True(capturedContext?.CanReadAllTenantPortfolioProgramData);
        }
    }

    [Fact]
    public async Task Tenant_admin_handover_read_scope_does_not_cover_mutations_or_manager_discovery()
    {
        foreach (var (path, method) in new[]
        {
            ($"/api/manager-handovers/portfolios/{Guid.NewGuid()}/requests", HttpMethods.Post),
            ($"/api/manager-handovers/programs/{Guid.NewGuid()}/eligible-managers", HttpMethods.Get)
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

            await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

            Assert.False(nextCalled);
            Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        }
    }

    [Fact]
    public async Task Explicit_admin_handover_permission_allows_native_initiation_and_own_cancel_paths_outside_manager_scope()
    {
        foreach (var (path, method) in new[]
        {
            ($"/api/projects/{Guid.NewGuid()}/handover-capabilities", HttpMethods.Get),
            ($"/api/projects/{Guid.NewGuid()}/handover-managers", HttpMethods.Get),
            ($"/api/projects/{Guid.NewGuid()}/handover-requests/admin", HttpMethods.Post),
            ($"/api/projects/{Guid.NewGuid()}/handover-requests/initial-assignment", HttpMethods.Post),
            ($"/api/manager-handovers/programs/{Guid.NewGuid()}/capabilities", HttpMethods.Get),
            ($"/api/manager-handovers/portfolios/{Guid.NewGuid()}/eligible-managers", HttpMethods.Get),
            ($"/api/manager-handovers/programs/{Guid.NewGuid()}/admin-requests", HttpMethods.Post),
            ($"/api/manager-handovers/portfolios/{Guid.NewGuid()}/initial-assignment-requests", HttpMethods.Post),
            ($"/api/handover-requests/{Guid.NewGuid()}/cancel", HttpMethods.Post),
            ($"/api/manager-handovers/programs/requests/{Guid.NewGuid()}/cancel", HttpMethods.Post)
        })
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
            var httpContext = CreateAuthenticatedContext(path);
            httpContext.User.AddIdentity(new ClaimsIdentity(new[] { new Claim("permission", "Handover.AdminInitiate") }, "admin-permission"));
            httpContext.Request.Method = method;

            await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

            Assert.True(nextCalled);
            Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        }
    }

    [Fact]
    public async Task Handover_tenant_read_scope_does_not_apply_to_non_admin_or_view_as_requests()
    {
        foreach (var (isAdmin, viewMode, expectedStatus) in new[]
        {
            (false, "Native", StatusCodes.Status404NotFound),
            (true, "ReadOnlyViewAs", StatusCodes.Status403Forbidden)
        })
        {
            var nextCalled = false;
            var middleware = new ManagementContextMiddleware(_ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
            var resolver = new Mock<IManagementScopeResolver>();
            resolver.Setup(item => item.IsTenantAdministratorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(isAdmin);
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
            var httpContext = CreateAuthenticatedContext($"/api/manager-handovers/programs/{Guid.NewGuid()}/requests");
            httpContext.Request.Method = HttpMethods.Get;
            if (viewMode == "ReadOnlyViewAs")
            {
                httpContext.Request.Headers[ManagementContextMiddleware.PerspectiveHeader] = "ProgramManager";
                httpContext.Request.Headers[ManagementContextMiddleware.ViewModeHeader] = viewMode;
            }

            await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

            Assert.False(nextCalled);
            Assert.Equal(expectedStatus, httpContext.Response.StatusCode);
        }
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

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

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

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

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

        await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

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

            await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

            Assert.False(nextCalled);
            Assert.Equal(expectedStatus, httpContext.Response.StatusCode);
            resolver.Verify(item => item.IsTenantAdministratorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task Self_scoped_handover_pending_is_addressable_without_management_assignment()
    {
        foreach (var path in new[]
        {
            $"/api/manager-handovers/portfolios/{Guid.NewGuid()}/pending-for-me",
            $"/api/manager-handovers/programs/{Guid.NewGuid()}/pending-for-me"
        })
        {
            var nextCalled = false;
            var middleware = new ManagementContextMiddleware(_ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
            var resolver = new Mock<IManagementScopeResolver>();
            resolver.Setup(item => item.IsTenantAdministratorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
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
            httpContext.Request.Method = HttpMethods.Get;

            await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

            Assert.True(nextCalled);
        }
    }

    [Fact]
    public async Task ReadOnlyViewAs_blocks_handover_mutations()
    {
        foreach (var path in new[]
        {
            $"/api/manager-handovers/portfolios/requests/{Guid.NewGuid()}/accept",
            $"/api/manager-handovers/portfolios/requests/{Guid.NewGuid()}/reject",
            $"/api/manager-handovers/portfolios/requests/{Guid.NewGuid()}/cancel",
            $"/api/handover-requests/{Guid.NewGuid()}/cancel"
        })
        {
            var nextCalled = false;
            var middleware = new ManagementContextMiddleware(_ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
            var readOnlyContext = new ManagementContext(
                Guid.NewGuid(), Guid.NewGuid(), ManagementPerspective.PortfolioManager,
                ManagementViewMode.ReadOnlyViewAs, null, null, null, ManagementScope.Empty);
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
                .ReturnsAsync(readOnlyContext);
            var accessor = new Mock<IManagementContextAccessor>();
            var httpContext = CreateAuthenticatedContext(path);
            httpContext.Request.Method = HttpMethods.Post;

            await middleware.InvokeAsync(httpContext, resolver.Object, accessor.Object, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

            Assert.False(nextCalled);
            Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
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

[Collection("Static tenant resolution")]
public sealed class OrganizationScopedCollectionPermissionMiddlewareTests
{
    [Fact]
    public async Task Adds_permission_when_one_current_organization_grants_it()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var context = CreateContext(userId, tenantId, organizationId);
        await using var db = CreateDb(tenantId, organizationId);
        var accessor = CreateAccessor(context);
        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsForOrganizationAsync(
                userId, tenantId, organizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["Project.View"], StringComparer.Ordinal));

        var httpContext = CreateRequest(userId, tenantId, "GET", "Project.View");
        var middleware = new OrganizationScopedCollectionPermissionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, db, accessor, permissions.Object, new ProjectOrganizationLookup(db));

        Assert.Contains(httpContext.User.FindAll("permission"), claim => claim.Value == "Project.View");
        Assert.Equal(new[] { organizationId }, accessor.Current!.Scope.OrganizationIds);
    }

    [Fact]
    public async Task Does_not_combine_permissions_from_different_organizations()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var firstOrganizationId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();
        var context = CreateContext(userId, tenantId, firstOrganizationId, secondOrganizationId);
        await using var db = CreateDb(tenantId, firstOrganizationId, secondOrganizationId);
        var accessor = CreateAccessor(context);
        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsForOrganizationAsync(
                userId, tenantId, firstOrganizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["Project.View"], StringComparer.Ordinal));
        permissions.Setup(service => service.GetEffectivePermissionsForOrganizationAsync(
                userId, tenantId, secondOrganizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["Project.Edit"], StringComparer.Ordinal));

        var httpContext = CreateRequest(userId, tenantId, "GET", "Project.View", "Project.Edit");
        var middleware = new OrganizationScopedCollectionPermissionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, db, accessor, permissions.Object, new ProjectOrganizationLookup(db));

        Assert.DoesNotContain(httpContext.User.FindAll("permission"), claim => claim.Value is "Project.View" or "Project.Edit");
        Assert.Empty(accessor.Current!.Scope.OrganizationIds);
        Assert.Empty(accessor.Current.Scope.PortfolioIds);
        Assert.Empty(accessor.Current.Scope.ProgramIds);
        Assert.Empty(accessor.Current.Scope.ProjectIds);
    }

    [Fact]
    public async Task Does_not_add_scoped_permission_for_mutations_or_unmarked_endpoints()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var context = CreateContext(userId, tenantId, organizationId);
        await using var db = CreateDb(tenantId, organizationId);
        var accessor = CreateAccessor(context);
        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsForOrganizationAsync(
                userId, tenantId, organizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["Project.View"], StringComparer.Ordinal));

        foreach (var (method, marked) in new[] { ("POST", true), ("GET", false) })
        {
            var httpContext = marked
                ? CreateRequest(userId, tenantId, method, "Project.View")
                : CreateRequest(userId, tenantId, method);
            var middleware = new OrganizationScopedCollectionPermissionMiddleware(_ => Task.CompletedTask);

            await middleware.InvokeAsync(httpContext, db, accessor, permissions.Object, new ProjectOrganizationLookup(db));

            Assert.DoesNotContain(httpContext.User.FindAll("permission"), claim => claim.Value == "Project.View");
        }

        permissions.Verify(service => service.GetEffectivePermissionsForOrganizationAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ManagementContext CreateContext(Guid userId, Guid tenantId, params Guid[] organizationIds) =>
        new(userId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>())
            {
                OrganizationIds = organizationIds.ToHashSet()
            });

    [Fact]
    public async Task Organization_view_grant_does_not_expose_directly_scoped_projects_from_another_organization()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var grantedOrganizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var grantedPortfolioId = Guid.NewGuid();
        var otherPortfolioId = Guid.NewGuid();
        var grantedProgramId = Guid.NewGuid();
        var otherProgramId = Guid.NewGuid();
        var grantedProject = new Project
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PortfolioId = grantedPortfolioId, Name = "Granted", Code = "A"
        };
        var otherProject = new Project
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PortfolioId = otherPortfolioId, Name = "Other", Code = "B"
        };
        var grantedProgram = new PortfolioProgram
        {
            Id = grantedProgramId, TenantId = tenantId, PortfolioId = grantedPortfolioId, Name = "Granted", Code = "A"
        };
        var otherProgram = new PortfolioProgram
        {
            Id = otherProgramId, TenantId = tenantId, PortfolioId = otherPortfolioId, Name = "Other", Code = "B"
        };
        await using var db = CreateDb(
            tenantId,
            [grantedOrganizationId, otherOrganizationId],
            [
                new Portfolio { Id = grantedPortfolioId, TenantId = tenantId, OrganizationId = grantedOrganizationId, Name = "Granted", Code = "A" },
                new Portfolio { Id = otherPortfolioId, TenantId = tenantId, OrganizationId = otherOrganizationId, Name = "Other", Code = "B" }
            ],
            [grantedProject, otherProject],
            [grantedProgram, otherProgram]);
        var originalContext = new ManagementContext(
            userId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
            new ManagementScope(
                new HashSet<Guid> { grantedPortfolioId, otherPortfolioId },
                new HashSet<Guid> { grantedProgramId, otherProgramId },
                new HashSet<Guid> { grantedProject.Id, otherProject.Id })
            {
                OrganizationIds = new HashSet<Guid> { grantedOrganizationId, otherOrganizationId },
                OrganizationReferences =
                [
                    new ManagementEntityReference(grantedOrganizationId, "Granted"),
                    new ManagementEntityReference(otherOrganizationId, "Other")
                ],
                ProjectOrganizationIds = new Dictionary<Guid, Guid>
                {
                    [grantedProject.Id] = grantedOrganizationId,
                    [otherProject.Id] = otherOrganizationId
                }
            });
        var accessor = CreateAccessor(originalContext);
        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsForOrganizationAsync(
                userId, tenantId, grantedOrganizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["Project.View"], StringComparer.Ordinal));
        permissions.Setup(service => service.GetEffectivePermissionsForOrganizationAsync(
                userId, tenantId, otherOrganizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));
        var httpContext = CreateRequest(userId, tenantId, "GET", "Project.View");
        var middleware = new OrganizationScopedCollectionPermissionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, db, accessor, permissions.Object, new ProjectOrganizationLookup(db));

        Assert.Equal(new[] { grantedOrganizationId }, accessor.Current!.Scope.OrganizationIds);
        Assert.Equal(new[] { grantedPortfolioId }, accessor.Current.Scope.PortfolioIds);
        Assert.Equal(new[] { grantedProgramId }, accessor.Current.Scope.ProgramIds);
        Assert.Equal(new[] { grantedProject.Id }, accessor.Current.Scope.ProjectIds);
        Assert.DoesNotContain(otherProject.Id, accessor.Current.Scope.ProjectIds);
        Assert.DoesNotContain(otherProgramId, accessor.Current.Scope.ProgramIds);
    }

    [Fact]
    public async Task Existing_tenant_permission_keeps_the_existing_tenant_scope()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var firstOrganizationId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();
        var firstPortfolioId = Guid.NewGuid();
        var secondPortfolioId = Guid.NewGuid();
        var firstProgramId = Guid.NewGuid();
        var secondProgramId = Guid.NewGuid();
        var firstProject = new Project
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PortfolioId = firstPortfolioId, Name = "First", Code = "A"
        };
        var secondProject = new Project
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PortfolioId = secondPortfolioId, Name = "Second", Code = "B"
        };
        var firstProgram = new PortfolioProgram
        {
            Id = firstProgramId, TenantId = tenantId, PortfolioId = firstPortfolioId, Name = "First", Code = "A"
        };
        var secondProgram = new PortfolioProgram
        {
            Id = secondProgramId, TenantId = tenantId, PortfolioId = secondPortfolioId, Name = "Second", Code = "B"
        };
        await using var db = CreateDb(
            tenantId,
            [firstOrganizationId, secondOrganizationId],
            [
                new Portfolio { Id = firstPortfolioId, TenantId = tenantId, OrganizationId = firstOrganizationId, Name = "First", Code = "A" },
                new Portfolio { Id = secondPortfolioId, TenantId = tenantId, OrganizationId = secondOrganizationId, Name = "Second", Code = "B" }
            ],
            [firstProject, secondProject],
            [firstProgram, secondProgram]);
        var context = new ManagementContext(
            userId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
            new ManagementScope(
                new HashSet<Guid> { firstPortfolioId, secondPortfolioId },
                new HashSet<Guid> { firstProgramId, secondProgramId },
                new HashSet<Guid> { firstProject.Id, secondProject.Id })
            {
                OrganizationIds = new HashSet<Guid> { firstOrganizationId, secondOrganizationId },
                ProjectOrganizationIds = new Dictionary<Guid, Guid>
                {
                    [firstProject.Id] = firstOrganizationId,
                    [secondProject.Id] = secondOrganizationId
                }
            });
        var accessor = CreateAccessor(context);
        var permissions = new Mock<IUserEffectivePermissionService>(MockBehavior.Strict);
        var httpContext = CreateRequest(userId, tenantId, "GET", "Project.View");
        httpContext.User.Identities.Single(identity => identity.IsAuthenticated)
            .AddClaim(new Claim("permission", "Project.View"));
        var middleware = new OrganizationScopedCollectionPermissionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, db, accessor, permissions.Object, new ProjectOrganizationLookup(db));

        Assert.Equal(context.Scope.OrganizationIds, accessor.Current!.Scope.OrganizationIds);
        Assert.Equal(context.Scope.PortfolioIds, accessor.Current.Scope.PortfolioIds);
        Assert.Equal(context.Scope.ProgramIds, accessor.Current.Scope.ProgramIds);
        Assert.Equal(context.Scope.ProjectIds, accessor.Current.Scope.ProjectIds);
        permissions.VerifyNoOtherCalls();
    }

    private static ManagementContextAccessor CreateAccessor(ManagementContext context)
    {
        var accessor = new ManagementContextAccessor();
        accessor.Set(context);
        return accessor;
    }

    private static AppDbContext CreateDb(
        Guid tenantId,
        params Guid[] organizationIds) => CreateDb(tenantId, organizationIds, [], []);

    private static AppDbContext CreateDb(
        Guid tenantId,
        Guid[] organizationIds,
        Portfolio[] portfolios,
        Project[] projects,
        PortfolioProgram[]? programs = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);
        db.Organizations.AddRange(organizationIds.Select(id => new Organization
        {
            Id = id, TenantId = tenantId, Name = id.ToString("N"), IsActive = true
        }));
        db.Portfolios.AddRange(portfolios);
        db.Programs.AddRange(programs ?? []);
        db.Projects.AddRange(projects);
        var requestContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenantId", tenantId.ToString("D"))], "test"))
        };
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor { HttpContext = requestContext });
        try
        {
            db.SaveChanges();
        }
        finally
        {
            AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        }
        return db;
    }

    private static DefaultHttpContext CreateRequest(Guid userId, Guid tenantId, string method, params string[] permissionKeys)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("tenantId", tenantId.ToString())
        ], "test"));
        if (permissionKeys.Length > 0)
            context.SetEndpoint(new Endpoint(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(new OrganizationScopedCollectionPermissionAttribute(permissionKeys)),
                "organization-scoped collection"));
        return context;
    }
}
