using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.ProjectPlanning.Api;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class PlanningCommandControllerActorIdentityTests
{
    [Fact]
    public async Task PlanWbsCreate_AuthenticatedTenantOnlyPrincipalIsRejectedBeforeMutation()
    {
        var service = new Mock<IPlanWbsService>(MockBehavior.Strict);
        var controller = new PlanWbsController(service.Object) { ControllerContext = AuthenticatedTenantOnlyContext("Planning.Edit") };

        var response = await controller.Create(Guid.NewGuid(), Guid.NewGuid(), new CreatePlanWbsNodeDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SchedulingRecalculate_AuthenticatedTenantOnlyPrincipalIsRejectedBeforeAnalysis()
    {
        var activityService = new Mock<IPlanActivityService>(MockBehavior.Strict);
        var analysisService = new Mock<IPlanSchedulingAnalysisService>(MockBehavior.Strict);
        var controller = new PlanSchedulingCommandController(activityService.Object, analysisService.Object)
        {
            ControllerContext = AuthenticatedTenantOnlyContext("Planning.Edit")
        };

        var response = await controller.Recalculate(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        activityService.VerifyNoOtherCalls();
        analysisService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TablePreferenceGet_AuthenticatedTenantOnlyPrincipalIsRejectedBeforeUserLookup()
    {
        var service = new Mock<IPlanningTablePreferenceService>(MockBehavior.Strict);
        var controller = new PlanningTablePreferenceController(service.Object)
        {
            ControllerContext = AuthenticatedTenantOnlyContext("Planning.View")
        };

        var response = await controller.Get(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GridHistoryUndo_InvalidActorClaimIsRejectedBeforeMutation()
    {
        var service = new Mock<IPlanGridCommandHistoryService>(MockBehavior.Strict);
        var controller = new PlanGridCommandHistoryController(service.Object)
        {
            ControllerContext = AuthenticatedInvalidActorContext("Planning.Edit")
        };

        var response = await controller.Undo(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        service.VerifyNoOtherCalls();
    }

    private static ControllerContext AuthenticatedTenantOnlyContext(string permission) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenantId", Guid.NewGuid().ToString()),
                new Claim("permission", permission)
            ], "test"))
        }
    };

    private static ControllerContext AuthenticatedInvalidActorContext(string permission) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "not-a-guid"),
                new Claim("tenantId", Guid.NewGuid().ToString()),
                new Claim("permission", permission)
            ], "test"))
        }
    };
}
