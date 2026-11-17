using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class FinanceMutationActorBindingTests
{
    [Fact]
    public void Mutation_actor_must_match_claims_and_current_tenant_management_context()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var accessor = HttpAccessor(actorId, tenantId);

        Assert.Null(FinancePermissionGuard.RequireWritableActorContext(
            accessor, Management(tenantId, actorId), actorId.ToString("D"), tenantId));
        Assert.Equal(403, FinancePermissionGuard.RequireWritableActorContext(
            accessor, Management(tenantId, actorId), "System", tenantId)?.StatusCode);
        Assert.Equal(403, FinancePermissionGuard.RequireWritableActorContext(
            accessor, Management(tenantId, Guid.NewGuid()), actorId.ToString("D"), tenantId)?.StatusCode);
        Assert.Equal(403, FinancePermissionGuard.RequireWritableActorContext(
            accessor, Management(Guid.NewGuid(), actorId), actorId.ToString("D"), tenantId)?.StatusCode);
    }

    [Fact]
    public void Mutation_actor_guard_rejects_missing_identity_and_read_only_management_context()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var accessorWithoutActor = HttpAccessor(null, tenantId);
        var readOnly = Management(tenantId, actorId, ManagementViewMode.ReadOnlyViewAs);

        Assert.Equal(401, FinancePermissionGuard.RequireWritableActorContext(
            accessorWithoutActor, Management(tenantId, actorId), actorId.ToString("D"), tenantId)?.StatusCode);
        Assert.Equal(403, FinancePermissionGuard.RequireWritableActorContext(
            HttpAccessor(actorId, tenantId), readOnly, actorId.ToString("D"), tenantId)?.StatusCode);
    }

    private static HttpContextAccessor HttpAccessor(Guid? actorId, Guid tenantId)
    {
        var claims = new List<Claim> { new("tenantId", tenantId.ToString("D")) };
        if (actorId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, actorId.Value.ToString("D")));
        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            }
        };
    }

    private static ManagementContext Management(
        Guid tenantId,
        Guid actorId,
        ManagementViewMode viewMode = ManagementViewMode.Native)
    {
        return new ManagementContext(
            actorId, tenantId, ManagementPerspective.ProjectManager, viewMode,
            null, null, null, ManagementScope.Empty);
    }
}
