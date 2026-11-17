using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Finance;
using NEXUS.Api.Management;
using NEXUS.Modules.Audit.Api;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ProjectFinancialReadScopeIdentityTests
{
    [Fact]
    public async Task Financial_foundation_read_and_write_reject_actor_mismatch_before_database_or_audit_access()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var claimActorId = Guid.NewGuid();
        var managementContext = Context(tenantId, Guid.NewGuid(), projectId);
        var audit = new Mock<IAuditService>(MockBehavior.Strict);
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"financial-foundation-identity-{Guid.NewGuid():N}")
            .Options);
        var controller = new ProjectFinancialFoundationController(db, audit.Object, managementContext)
        {
            ControllerContext = Controller(claimActorId, tenantId)
        };

        var read = await controller.Get(projectId, CancellationToken.None);
        var write = await controller.UpdateApprovalConfiguration(
            projectId,
            new UpdateProjectFinanceApprovalConfigurationDto("Sequential", "row-version"),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(read);
        Assert.IsType<NotFoundResult>(write);
        audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Financial_activity_read_and_facets_reject_tenant_mismatch_before_scope_resolution()
    {
        var contextTenantId = Guid.NewGuid();
        var claimTenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var managementContext = Context(contextTenantId, actorId, projectId);
        var audit = new Mock<IAuditService>(MockBehavior.Strict);
        var scopeProvider = new Mock<IProjectAuditScopeProvider>(MockBehavior.Strict);
        var controller = new ProjectFinancialActivityController(
            audit.Object, scopeProvider.Object, managementContext)
        {
            ControllerContext = Controller(actorId, claimTenantId)
        };

        var read = await controller.Get(projectId, cancellationToken: CancellationToken.None);
        var facets = await controller.GetFacets(projectId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(read);
        Assert.IsType<NotFoundObjectResult>(facets);
        scopeProvider.VerifyNoOtherCalls();
        audit.VerifyNoOtherCalls();
    }

    private static ManagementContextAccessor Context(Guid tenantId, Guid actorId, Guid projectId)
    {
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(
            actorId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, projectId,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));
        return accessor;
    }

    private static ControllerContext Controller(Guid actorId, Guid tenantId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")),
                new Claim("tenantId", tenantId.ToString("D"))
            ], "test"))
        }
    };
}
