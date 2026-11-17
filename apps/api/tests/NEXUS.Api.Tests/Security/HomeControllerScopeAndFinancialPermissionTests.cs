using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Home;
using NEXUS.Api.Management;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class HomeControllerScopeAndFinancialPermissionTests
{
    [Fact]
    public async Task GetSummary_rejects_tenant_authenticated_principal_without_actor_id()
    {
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContext(tenantId, userId: null);
        ConfigureTenant(httpContext);
        await using var db = CreateDbContext();
        var controller = CreateController(db, httpContext, new ManagementContextAccessor());

        var result = await controller.GetSummary();

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetSummary_rejects_management_context_owned_by_another_actor()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var httpContext = CreateHttpContext(tenantId, userId);
        ConfigureTenant(httpContext);
        await using var db = CreateDbContext();
        var mismatchedContext = CreateManagementContextAccessor(tenantId, Guid.NewGuid(), Guid.NewGuid());
        var controller = CreateController(db, httpContext, mismatchedContext);

        var result = await controller.GetSummary();

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetSummary_rejects_management_context_from_another_tenant()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var httpContext = CreateHttpContext(tenantId, userId);
        ConfigureTenant(httpContext);
        await using var db = CreateDbContext();
        var mismatchedContext = CreateManagementContextAccessor(Guid.NewGuid(), userId, Guid.NewGuid());
        var controller = CreateController(db, httpContext, mismatchedContext);

        var result = await controller.GetSummary();

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetSummary_scopes_projects_and_omits_financial_aggregates_without_finance_permission()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var visibleProjectId = Guid.NewGuid();
        var outOfScopeProjectId = Guid.NewGuid();
        var httpContext = CreateHttpContext(tenantId, userId);
        ConfigureTenant(httpContext);
        await using var db = CreateDbContext();
        db.Projects.AddRange(
            new Project
            {
                Id = visibleProjectId,
                TenantId = tenantId,
                Name = "Visible project",
                Code = "VISIBLE",
                Status = "Active",
                Currency = "USD",
                Budget = 100m,
                ActualCost = 40m,
                CreatedBy = userId.ToString()
            },
            new Project
            {
                Id = outOfScopeProjectId,
                TenantId = tenantId,
                Name = "Out-of-scope project",
                Code = "OUTSIDE",
                Status = "Active",
                Health = "Red",
                Currency = "USD",
                Budget = 900000m,
                ActualCost = 700000m,
                CreatedBy = userId.ToString()
            });
        await db.SaveChangesAsync();

        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), "Project.View"))
            .ReturnsAsync(AuthorizationResult.Success());
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), "Risk.View"))
            .ReturnsAsync(AuthorizationResult.Failed());
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), "ProjectFinancialFoundation.View"))
            .ReturnsAsync(AuthorizationResult.Failed());

        var controller = CreateController(
            db,
            httpContext,
            CreateManagementContextAccessor(tenantId, userId, visibleProjectId),
            authorization.Object);

        var result = await controller.GetSummary();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var summary = Assert.IsType<HomeSummaryDto>(ok.Value);
        Assert.Equal(1, summary.ActiveProjectsCount);
        Assert.Empty(summary.AttentionProjects);
        Assert.Empty(summary.BudgetByCurrency);
    }

    private static HomeController CreateController(
        AppDbContext db,
        HttpContext httpContext,
        ManagementContextAccessor managementContext,
        IAuthorizationService? authorization = null)
    {
        var controller = new HomeController(db, authorization ?? Mock.Of<IAuthorizationService>(), managementContext)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
        return controller;
    }

    private static HttpContext CreateHttpContext(Guid tenantId, Guid? userId)
    {
        var claims = new List<Claim> { new("tenantId", tenantId.ToString()) };
        if (userId.HasValue) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
    }

    private static void ConfigureTenant(HttpContext httpContext) =>
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor { HttpContext = httpContext });

    private static AppDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static ManagementContextAccessor CreateManagementContextAccessor(
        Guid tenantId,
        Guid userId,
        params Guid[] projectIds)
    {
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(
            userId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            new ManagementScope(
                new HashSet<Guid>(),
                new HashSet<Guid>(),
                projectIds.ToHashSet())));
        return accessor;
    }
}
