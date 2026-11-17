using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.Portfolio.Api;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ProgramControllerActorIdentityTests
{
    [Fact]
    public async Task Create_AuthenticatedTenantOnlyPrincipalIsRejectedBeforeServiceCall()
    {
        var service = new Mock<IProgramService>(MockBehavior.Strict);
        var controller = new ProgramController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("tenantId", Guid.NewGuid().ToString()),
                        new Claim("permission", "Program.Create")
                    ], "test"))
                }
            }
        };

        var response = await controller.Create(new SaveProgramDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProgramService_CreateRejectsManagementContextForDifferentAuthenticatedActor()
    {
        var actorId = Guid.NewGuid();
        var otherActorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenantId", tenantId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, actorId.ToString())
            ], "test"))
        };
        var management = new Mock<IManagementContextAccessor>(MockBehavior.Strict);
        management.Setup(accessor => accessor.Current).Returns(new ManagementContext(
            otherActorId,
            tenantId,
            ManagementPerspective.ProgramManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            ManagementScope.Empty));
        using var db = new Microsoft.EntityFrameworkCore.DbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Microsoft.EntityFrameworkCore.DbContext>().Options);
        var service = new ProgramService(db, new HttpContextAccessor { HttpContext = httpContext }, management.Object);

        var result = await service.CreateAsync(new SaveProgramDto(), actorId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(401, result.Error?.StatusCode);
        management.Verify(accessor => accessor.Current, Times.AtLeastOnce);
        management.VerifyNoOtherCalls();
    }
}
