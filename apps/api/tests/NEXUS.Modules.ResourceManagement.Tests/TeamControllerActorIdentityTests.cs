using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.ResourceManagement.Api;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public sealed class TeamControllerActorIdentityTests
{
    [Fact]
    public async Task Create_AuthenticatedTenantOnlyPrincipalIsRejectedWithoutServiceMutation()
    {
        var service = new Mock<ITeamService>(MockBehavior.Strict);
        var controller = new TeamController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("tenantId", Guid.NewGuid().ToString()),
                        new Claim("permission", "Team.Create")
                    ], "test"))
                }
            }
        };

        var response = await controller.Create(new CreateTeamDto { Name = "Operations" }, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_ValidActorWithoutManagementContextIsRejectedWithoutServiceMutation()
    {
        var service = new Mock<ITeamService>(MockBehavior.Strict);
        var controller = new TeamController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                        new Claim("tenantId", Guid.NewGuid().ToString()),
                        new Claim("permission", "Team.Create")
                    ], "test"))
                }
            }
        };

        var response = await controller.Create(new CreateTeamDto { Name = "Operations" }, CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
        service.VerifyNoOtherCalls();
    }
}
