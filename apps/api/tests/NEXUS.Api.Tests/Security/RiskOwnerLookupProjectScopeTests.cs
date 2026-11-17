using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.Identity.Api;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class RiskOwnerLookupProjectScopeTests
{
    [Fact]
    public async Task RiskOwnerLookup_RequiresProjectAndNeverFallsBackToTenantCandidates()
    {
        var tenantId = Guid.NewGuid();
        var audience = new Mock<IProjectAudienceLookup>(MockBehavior.Strict);
        var controller = CreateController(tenantId, new HashSet<Guid>(), audience.Object);

        var response = await controller.GetRiskWorkflowUsers(null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response);
        audience.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RiskOwnerLookup_UsesOnlyCurrentProjectAudience()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var candidate = new ProjectAudienceUserReference(Guid.NewGuid(), "Project Member", "member@example.test");
        var audience = new Mock<IProjectAudienceLookup>(MockBehavior.Strict);
        audience.Setup(lookup => lookup.SearchProjectUsersAsync(tenantId, projectId, null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidate });
        var controller = CreateController(tenantId, new HashSet<Guid> { projectId }, audience.Object);

        var response = await controller.GetRiskWorkflowUsers(projectId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        var candidates = Assert.IsAssignableFrom<IReadOnlyList<WorkflowUserLookupDto>>(ok.Value);
        var result = Assert.Single(candidates);
        Assert.Equal(candidate.UserId, result.Id);
        Assert.Equal(candidate.FullName, result.FullName);
        audience.Verify(lookup => lookup.SearchProjectUsersAsync(tenantId, projectId, null, 50, It.IsAny<CancellationToken>()), Times.Once);
        audience.VerifyNoOtherCalls();
    }

    private static UsersController CreateController(Guid tenantId, IReadOnlySet<Guid> projectIds, IProjectAudienceLookup audience)
    {
        var accessor = new Mock<IManagementContextAccessor>();
        accessor.SetupGet(value => value.Current).Returns(new ManagementContext(
            Guid.NewGuid(), tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), projectIds)));
        var controller = new UsersController(
            Mock.Of<IUserRepository>(),
            Mock.Of<IUserAdministrationService>(),
            Mock.Of<IUserEffectivePermissionService>(),
            projectAudience: audience,
            managementContext: accessor.Object);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenantId", tenantId.ToString())
        }, "Test"));
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }
}
