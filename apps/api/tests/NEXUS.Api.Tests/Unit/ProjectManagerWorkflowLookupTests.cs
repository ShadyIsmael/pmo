using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.Identity.Api;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class ProjectManagerWorkflowLookupTests
{
    [Fact]
    public async Task ProjectUserLookup_ReturnsTenantUsersWithoutARoleNameFilter()
    {
        var tenantId = Guid.NewGuid();
        var user = new User { Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Project", LastName = "Sponsor" };
        var repository = new Mock<IUserRepository>();
        repository.Setup(item => item.GetActiveInTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });

        var controller = CreateController(repository.Object, Mock.Of<IUserEffectivePermissionService>(), tenantId);
        var response = await controller.GetProjectWorkflowUsers(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        var candidates = Assert.IsAssignableFrom<IReadOnlyList<WorkflowUserLookupDto>>(ok.Value);
        Assert.Equal(user.Id, Assert.Single(candidates).Id);
        repository.Verify(item => item.GetByRoleNameInTenantAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProjectManagerLookup_FiltersUsersWithoutEffectiveHandoverApproveAndUsesTenantScope()
    {
        var tenantId = Guid.NewGuid();
        var eligibleId = Guid.NewGuid();
        var deniedId = Guid.NewGuid();
        var users = new List<User>
        {
            new() { Id = eligibleId, TenantId = tenantId, FirstName = "Eligible", LastName = "Manager" },
            new() { Id = deniedId, TenantId = tenantId, FirstName = "Denied", LastName = "Manager" }
        };

        var repository = new Mock<IUserRepository>();
        repository.Setup(item => item.GetByRoleCategoryInTenantAsync("Management", tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var effectivePermissions = new Mock<IUserEffectivePermissionService>();
        effectivePermissions.Setup(service => service.GetEffectivePermissionsAsync(eligibleId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { "Handover.Approve" });
        effectivePermissions.Setup(service => service.GetEffectivePermissionsAsync(deniedId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));

        var controller = CreateController(repository.Object, effectivePermissions.Object, tenantId);
        var response = await controller.GetProjectManagerCandidates(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        var candidates = Assert.IsAssignableFrom<IReadOnlyList<WorkflowUserLookupDto>>(ok.Value);
        var candidate = Assert.Single(candidates);
        Assert.Equal(eligibleId, candidate.Id);
        Assert.Equal("Eligible Manager", candidate.FullName);
        repository.Verify(item => item.GetByRoleCategoryInTenantAsync("Management", tenantId, It.IsAny<CancellationToken>()), Times.Once);
        effectivePermissions.Verify(service => service.GetEffectivePermissionsAsync(eligibleId, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        effectivePermissions.Verify(service => service.GetEffectivePermissionsAsync(deniedId, tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProjectManagerLookup_RequiresTenantClaim()
    {
        var repository = new Mock<IUserRepository>();
        var effectivePermissions = new Mock<IUserEffectivePermissionService>();
        var controller = CreateController(repository.Object, effectivePermissions.Object, tenantId: null);

        var response = await controller.GetProjectManagerCandidates(CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
        repository.Verify(item => item.GetByRoleCategoryInTenantAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static UsersController CreateController(IUserRepository repository, IUserEffectivePermissionService effectivePermissions, Guid? tenantId)
    {
        var controller = new UsersController(repository, Mock.Of<IUserAdministrationService>(), effectivePermissions);
        var context = new DefaultHttpContext();
        var claims = tenantId.HasValue
            ? new[] { new Claim("tenantId", tenantId.Value.ToString()) }
            : Array.Empty<Claim>();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }
}
