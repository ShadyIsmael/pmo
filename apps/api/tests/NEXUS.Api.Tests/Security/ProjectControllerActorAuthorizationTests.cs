using NEXUS.Api.Management;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ProjectControllerActorAuthorizationTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    public async Task Project_actor_bound_routes_reject_missing_or_malformed_actor_before_service_calls(string actorMode)
    {
        var tenantId = Guid.NewGuid();
        var contextUserId = Guid.NewGuid();
        var claims = new List<Claim> { new("tenantId", tenantId.ToString()) };
        if (actorMode == "malformed")
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));
            claims.Add(new Claim("sub", contextUserId.ToString()));
        }

        var harness = CreateHarness(claims, contextUserId, tenantId);
        var results = await InvokeAllActorBoundRoutesAsync(harness.Controller);

        Assert.All(results, result => Assert.IsType<UnauthorizedResult>(result));
        harness.VerifyNoServiceCalls();
    }

    [Fact]
    public async Task Project_actor_bound_routes_require_actor_to_match_management_context_before_service_calls()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var otherContextUserId = Guid.NewGuid();
        var harness = CreateHarness(
            [new Claim(ClaimTypes.NameIdentifier, actorId.ToString()), new Claim("tenantId", tenantId.ToString())],
            otherContextUserId,
            tenantId);

        var results = await InvokeAllActorBoundRoutesAsync(harness.Controller);

        Assert.All(results, result => Assert.IsType<ForbidResult>(result));
        harness.VerifyNoServiceCalls();
    }

    private static async Task<IActionResult[]> InvokeAllActorBoundRoutesAsync(ProjectController controller)
    {
        var projectId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        return
        [
            await controller.Create(null!, CancellationToken.None),
            await controller.Update(projectId, null!, CancellationToken.None),
            await controller.ChangeStatus(projectId, null!, CancellationToken.None),
            await controller.Delete(projectId, CancellationToken.None),
            await controller.Archive(projectId, CancellationToken.None),
            await controller.Reactivate(projectId, CancellationToken.None),
            await controller.CreateStatusReport(projectId, null!, CancellationToken.None),
            await controller.UpdateStatusReport(projectId, reportId, null!, CancellationToken.None),
            await controller.PublishStatusReport(projectId, reportId, null!, CancellationToken.None),
            await controller.DeleteStatusReport(projectId, reportId, CancellationToken.None),
            await controller.SaveReportingConfiguration(projectId, null!, CancellationToken.None),
            await controller.CreateHandoverRequest(projectId, null!, CancellationToken.None),
            await controller.CreateAdministrativeHandoverRequest(projectId, null!, CancellationToken.None),
            await controller.CreateInitialManagerAssignment(projectId, null!, CancellationToken.None),
            await controller.GetEligibleHandoverManagers(projectId, CancellationToken.None),
            await controller.GetHandoverCapabilities(projectId, CancellationToken.None),
            await controller.GetHandoverRequests(projectId, CancellationToken.None),
            await controller.AddTeamMember(projectId, null!, CancellationToken.None),
            await controller.UpdateTeamMember(projectId, memberId, null!, CancellationToken.None),
            await controller.RemoveTeamMember(projectId, memberId, CancellationToken.None)
        ];
    }

    private static ControllerHarness CreateHarness(
        IEnumerable<Claim> claims,
        Guid managementUserId,
        Guid tenantId)
    {
        var project = new Mock<IProjectService>();
        var snapshots = new Mock<IProjectSnapshotService>();
        var handovers = new Mock<IProjectHandoverRequestService>();
        var reports = new Mock<IProjectStatusReportService>();
        var overview = new Mock<IProjectOverviewService>();
        var reporting = new Mock<IProjectReportingService>();
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            managementUserId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, null, ManagementScope.Empty));

        var controller = new ProjectController(
            project.Object,
            snapshots.Object,
            handovers.Object,
            reports.Object,
            overview.Object,
            reporting.Object,
            managementContext: management)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "project-controller-test"))
                }
            }
        };

        return new ControllerHarness(controller, project, snapshots, handovers, reports, overview, reporting);
    }

    private sealed record ControllerHarness(
        ProjectController Controller,
        Mock<IProjectService> Project,
        Mock<IProjectSnapshotService> Snapshots,
        Mock<IProjectHandoverRequestService> Handovers,
        Mock<IProjectStatusReportService> Reports,
        Mock<IProjectOverviewService> Overview,
        Mock<IProjectReportingService> Reporting)
    {
        public void VerifyNoServiceCalls()
        {
            Project.VerifyNoOtherCalls();
            Snapshots.VerifyNoOtherCalls();
            Handovers.VerifyNoOtherCalls();
            Reports.VerifyNoOtherCalls();
            Overview.VerifyNoOtherCalls();
            Reporting.VerifyNoOtherCalls();
        }
    }
}
