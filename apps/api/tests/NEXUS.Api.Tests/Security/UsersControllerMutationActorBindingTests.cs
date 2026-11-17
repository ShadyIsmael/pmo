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

public sealed class UsersControllerMutationActorBindingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task User_mutations_forbid_missing_empty_or_malformed_actor_before_calling_service(int identityCase)
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var claims = new List<Claim> { new("tenantId", tenantId.ToString("D")) };
        if (identityCase == 1) claims.Add(new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString("D")));
        if (identityCase == 2)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));
            claims.Add(new Claim("sub", actorId.ToString("D")));
        }

        var service = new Mock<IUserAdministrationService>(MockBehavior.Strict);
        var controller = CreateController(service, claims, actorId, tenantId);
        var userId = Guid.NewGuid();
        var create = new CreateUserDto { Email = "new@example.test", FirstName = "New", LastName = "User" };
        var update = new UpdateUserDto { Email = "user@example.test", FirstName = "Existing", LastName = "User", IsActive = true };

        Assert.IsType<ForbidResult>(await controller.Create(create, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.ResetPassword(userId, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.Update(userId, update, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.UpdatePermissions(userId, new UpdateUserPermissionsDto(), CancellationToken.None));

        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task User_mutation_accepts_sub_only_when_it_matches_management_context()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var request = new CreateUserDto { Email = "new@example.test", FirstName = "New", LastName = "User" };
        var service = new Mock<IUserAdministrationService>(MockBehavior.Strict);
        service.Setup(item => item.CreateAsync(request, actorId.ToString("D"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreatedUserDto>.Success(new CreatedUserDto { User = new UserAdminDto() }));
        var controller = CreateController(
            service,
            [new Claim("sub", actorId.ToString("D")), new Claim("tenantId", tenantId.ToString("D"))],
            actorId,
            tenantId);

        Assert.IsType<CreatedAtActionResult>(await controller.Create(request, CancellationToken.None));

        service.Verify(item => item.CreateAsync(request, actorId.ToString("D"), It.IsAny<CancellationToken>()), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task User_mutations_forbid_mismatched_or_read_only_management_context()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")),
            new Claim("tenantId", tenantId.ToString("D"))
        };
        var service = new Mock<IUserAdministrationService>(MockBehavior.Strict);
        var mismatched = CreateController(service, claims, Guid.NewGuid(), tenantId);
        var mismatchedTenant = CreateController(service, claims, actorId, Guid.NewGuid());
        var readOnly = CreateController(service, claims, actorId, tenantId, ManagementViewMode.ReadOnlyViewAs);
        var request = new CreateUserDto { Email = "new@example.test", FirstName = "New", LastName = "User" };

        Assert.IsType<ForbidResult>(await mismatched.Create(request, CancellationToken.None));
        Assert.IsType<ForbidResult>(await mismatchedTenant.Create(request, CancellationToken.None));
        Assert.IsType<ForbidResult>(await readOnly.Create(request, CancellationToken.None));

        service.VerifyNoOtherCalls();
    }

    private static UsersController CreateController(
        Mock<IUserAdministrationService> service,
        IEnumerable<Claim> claims,
        Guid contextUserId,
        Guid contextTenantId,
        ManagementViewMode viewMode = ManagementViewMode.Native)
    {
        var managementContext = new Mock<IManagementContextAccessor>(MockBehavior.Strict);
        managementContext.SetupGet(accessor => accessor.Current).Returns(new ManagementContext(
            contextUserId,
            contextTenantId,
            ManagementPerspective.ProjectManager,
            viewMode,
            null,
            null,
            null,
            ManagementScope.Empty));

        return new UsersController(
            Mock.Of<IUserRepository>(),
            service.Object,
            Mock.Of<IUserEffectivePermissionService>(),
            managementContext: managementContext.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "authenticated-test"))
                }
            }
        };
    }
}
