using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.RiskManagement.Api;
using NEXUS.Modules.RiskManagement.Application;
using NEXUS.Modules.RiskManagement.Application.DTOs;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class RiskControllerActorIdentityTests
{
    [Fact]
    public async Task Create_AuthenticatedTenantOnlyPrincipalIsRejectedWithoutServiceMutation()
    {
        var service = new Mock<IRiskService>(MockBehavior.Strict);
        var controller = new RiskController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("tenantId", Guid.NewGuid().ToString()),
                        new Claim("permission", "Risk.Create")
                    ], "test"))
                }
            }
        };

        var response = await controller.Create(new CreateRiskDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        service.VerifyNoOtherCalls();
    }
}
