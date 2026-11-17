using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.ProjectPlanning.Api;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class PlanningCalendarControllerActorIdentityTests
{
    [Fact]
    public async Task GetTemplates_AuthenticatedTenantOnlyPrincipalIsRejectedBeforeTenantRead()
    {
        var service = new Mock<IPlanningCalendarService>(MockBehavior.Strict);
        var controller = new PlanningCalendarController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("tenantId", Guid.NewGuid().ToString()),
                        new Claim("permission", "Planning.View")
                    ], "test"))
                }
            }
        };

        var response = await controller.GetTemplates(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateTemplate_AuthenticatedTenantOnlyPrincipalIsRejectedWithoutServiceMutation()
    {
        var service = new Mock<IPlanningCalendarService>(MockBehavior.Strict);
        var controller = new PlanningCalendarController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("tenantId", Guid.NewGuid().ToString()),
                        new Claim("permission", "Planning.Edit")
                    ], "test"))
                }
            }
        };

        var response = await controller.CreateTemplate(new CreateCalendarTemplateDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        service.VerifyNoOtherCalls();
    }
}
