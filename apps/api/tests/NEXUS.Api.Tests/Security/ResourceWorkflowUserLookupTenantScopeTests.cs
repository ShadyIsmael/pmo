using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.Identity.Api;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ResourceWorkflowUserLookupTenantScopeTests
{
    [Fact]
    public async Task Resource_lookup_resolves_linked_resources_inside_authenticated_tenant()
    {
        var tenantId = Guid.NewGuid();
        var user = new User { Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Ari", LastName = "Tenant" };
        var resourceLookup = new Mock<IResourceReferenceLookup>(MockBehavior.Strict);
        resourceLookup
            .Setup(lookup => lookup.GetDetailsByUserIdsInTenantAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResourceReference>());

        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users
            .Setup(repository => repository.GetActiveInTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<User>)[user]);
        var controller = new UsersController(
            users.Object,
            Mock.Of<IUserAdministrationService>(),
            Mock.Of<IUserEffectivePermissionService>(),
            resourceLookup.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("tenantId", tenantId.ToString("D"))], "test"))
                }
            }
        };

        var result = await controller.GetResourceWorkflowUsers(null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        resourceLookup.Verify(
            lookup => lookup.GetDetailsByUserIdsInTenantAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)), tenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Resource_lookup_does_not_resolve_resource_outside_authenticated_tenant()
    {
        var tenantId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var resourceLookup = new Mock<IResourceReferenceLookup>(MockBehavior.Strict);
        resourceLookup
            .Setup(lookup => lookup.FindInTenantAsync(resourceId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceReference?)null);

        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        var controller = new UsersController(
            users.Object,
            Mock.Of<IUserAdministrationService>(),
            Mock.Of<IUserEffectivePermissionService>(),
            resourceLookup.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("tenantId", tenantId.ToString("D"))], "test"))
                }
            }
        };

        var result = await controller.GetResourceWorkflowUsers(resourceId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        resourceLookup.Verify(
            lookup => lookup.FindInTenantAsync(resourceId, tenantId, It.IsAny<CancellationToken>()),
            Times.Once);
        resourceLookup.Verify(
            lookup => lookup.GetDetailsByUserIdsInTenantAsync(
                It.IsAny<IEnumerable<Guid>>(), tenantId, It.IsAny<CancellationToken>()),
            Times.Never);
        users.Verify(
            repository => repository.GetActiveInTenantAsync(tenantId, It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
