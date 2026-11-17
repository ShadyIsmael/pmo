using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class CommitmentServiceReadOnlyScopeTests
{
    [Fact]
    public async Task Every_mutation_is_denied_by_the_service_in_read_only_view_as()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
                new Claim("tenantId", tenantId.ToString()),
                new Claim("permission", "Commitment.Create"),
                new Claim("permission", "Commitment.Edit"),
                new Claim("permission", "Commitment.Close"),
                new Claim("permission", "Commitment.Cancel"),
                new Claim("permission", "Commitment.Delete")
            ], "test"))
        };
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            actorId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.ReadOnlyViewAs,
            null,
            null,
            projectId,
            new ManagementScope(
                new HashSet<Guid>(),
                new HashSet<Guid>(),
                new HashSet<Guid> { projectId })));
        var dbOptions = new DbContextOptionsBuilder<DbContext>()
            .UseInMemoryDatabase($"commitment-read-only-{Guid.NewGuid():N}")
            .Options;
        await using var context = new DbContext(dbOptions);
        var service = new CommitmentService(
            context,
            Mock.Of<IAuditService>(),
            Mock.Of<IProjectReferenceLookup>(),
            new HttpContextAccessor { HttpContext = httpContext },
            management);

        var created = await service.CreateAsync(new CreateCommitmentDto { ProjectId = projectId }, actorId.ToString());
        var updated = await service.UpdateAsync(Guid.NewGuid(), new UpdateCommitmentDto(), actorId.ToString());
        var closed = await service.CloseAsync(Guid.NewGuid(), [], actorId.ToString());
        var cancelled = await service.CancelAsync(Guid.NewGuid(), [], actorId.ToString());
        var deleted = await service.DeleteAsync(Guid.NewGuid(), [], actorId.ToString());

        Assert.Equal(403, created.Error?.StatusCode);
        Assert.Equal(403, updated.Error?.StatusCode);
        Assert.Equal(403, closed.Error?.StatusCode);
        Assert.Equal(403, cancelled.Error?.StatusCode);
        Assert.Equal(403, deleted.Error?.StatusCode);
    }
}
