using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.CostManagement.Api;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class CommitmentControllerActorBindingTests
{
    [Fact]
    public async Task Create_rejects_authenticated_tenant_only_principal_without_service_call()
    {
        var service = new Mock<ICommitmentService>(MockBehavior.Strict);
        var controller = Controller(
            new CommitmentController(service.Object),
            Principal(Guid.NewGuid(), userId: null));

        var response = await controller.Create(new CreateCommitmentDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_rejects_valid_actor_when_management_context_is_unresolved()
    {
        var service = new Mock<ICommitmentService>(MockBehavior.Strict);
        var controller = Controller(
            new CommitmentController(service.Object),
            Principal(Guid.NewGuid(), Guid.NewGuid()));

        var response = await controller.Create(new CreateCommitmentDto(), CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_requires_matching_writable_management_context()
    {
        var service = new Mock<ICommitmentService>(MockBehavior.Strict);
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var management = new TestManagementContextAccessor(new ManagementContext(
            actorId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.ReadOnlyViewAs,
            null,
            null,
            null,
            ManagementScope.Empty));
        var controller = Controller(
            new CommitmentController(service.Object, management),
            Principal(tenantId, actorId));

        var response = await controller.Create(new CreateCommitmentDto(), CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
        service.VerifyNoOtherCalls();
    }

    private static CommitmentController Controller(CommitmentController controller, ClaimsPrincipal principal)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    private static ClaimsPrincipal Principal(Guid tenantId, Guid? userId)
    {
        var claims = new List<Claim>
        {
            new("tenantId", tenantId.ToString()),
            new("permission", "Commitment.Create")
        };
        if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private sealed class TestManagementContextAccessor(ManagementContext? current) : IManagementContextAccessor
    {
        public ManagementContext? Current { get; private set; } = current;

        public void Set(ManagementContext context) => Current = context;
    }
}
