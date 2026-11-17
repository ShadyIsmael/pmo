using NEXUS.Api.Management;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.WorkflowEngine.Api;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ProjectManagementActorBindingTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    public async Task Project_management_actor_routes_reject_missing_or_malformed_claim_before_service_calls(string actorMode)
    {
        var tenantId = Guid.NewGuid();
        var contextUserId = Guid.NewGuid();
        var claims = new List<Claim> { new("tenantId", tenantId.ToString()) };
        if (actorMode == "malformed")
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));
            claims.Add(new Claim("sub", contextUserId.ToString()));
        }

        var harness = CreateHarness(claims, contextUserId, tenantId, ManagementViewMode.Native);
        var results = await InvokeEveryActorBoundRouteAsync(harness);

        Assert.All(results, result => Assert.IsType<UnauthorizedResult>(result));
        harness.VerifyNoServiceCalls();
    }

    [Fact]
    public async Task Project_management_actor_routes_require_management_context_match_before_service_calls()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var harness = CreateHarness(
            [new Claim(ClaimTypes.NameIdentifier, actorId.ToString()), new Claim("tenantId", tenantId.ToString())],
            Guid.NewGuid(), tenantId, ManagementViewMode.Native);

        var results = await InvokeEveryActorBoundRouteAsync(harness);

        Assert.All(results, result => Assert.IsType<ForbidResult>(result));
        harness.VerifyNoServiceCalls();
    }

    [Fact]
    public async Task Project_management_mutation_routes_reject_effective_read_only_context_before_service_calls()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var harness = CreateHarness(
            [new Claim(ClaimTypes.NameIdentifier, actorId.ToString()), new Claim("tenantId", tenantId.ToString())],
            actorId, tenantId, ManagementViewMode.ReadOnlyViewAs);

        var results = await InvokeEveryMutationAsync(harness);

        Assert.All(results, result => Assert.IsType<ForbidResult>(result));
        harness.VerifyNoServiceCalls();
    }

    private static async Task<IActionResult[]> InvokeEveryActorBoundRouteAsync(ControllerHarness h)
    {
        var id = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        return
        [
            await h.Handover.GetPendingForMe(CancellationToken.None),
            await h.Handover.GetDetails(id, CancellationToken.None),
            await h.Handover.Review(id, CancellationToken.None),
            await h.Handover.Accept(id, CancellationToken.None),
            await h.Handover.Reject(id, null!, CancellationToken.None),
            await h.Handover.Cancel(id, CancellationToken.None),
            await h.Baseline.CreateInitialBaseline(id, CancellationToken.None),
            await h.ChangeRequests.Create(id, null!, CancellationToken.None),
            await h.ChangeRequests.UpdateDraft(id, null!, CancellationToken.None),
            await h.ChangeRequests.Submit(id, null!, CancellationToken.None),
            await h.ChangeRequests.Approve(id, null!, CancellationToken.None),
            await h.ChangeRequests.Reject(id, null!, CancellationToken.None),
            await h.ChangeRequests.Cancel(id, CancellationToken.None),
            await h.ChangeRequests.Remediate(id, CancellationToken.None),
            await h.ChangeRequests.Reassign(id, assignmentId, null!, CancellationToken.None),
            await h.People.Add(id, null!, CancellationToken.None),
            await h.People.Update(id, Guid.NewGuid(), null!, CancellationToken.None),
            await h.People.Remove(id, Guid.NewGuid(), null!, CancellationToken.None),
            await h.People.ClearSponsor(id, CancellationToken.None),
            await h.Stakeholders.AddInternal(id, null!, CancellationToken.None),
            await h.Stakeholders.AddContact(id, null!, CancellationToken.None),
            await h.Stakeholders.Update(id, Guid.NewGuid(), null!, CancellationToken.None),
            await h.Stakeholders.Revoke(id, Guid.NewGuid(), null!, CancellationToken.None),
            await h.Stakeholders.Remove(id, Guid.NewGuid(), null, CancellationToken.None),
            await h.Invitations.Send(id, Guid.NewGuid(), CancellationToken.None),
            await h.Invitations.Revoke(id, Guid.NewGuid(), CancellationToken.None),
            await h.ProjectWorkflow.Save(id, "Risk", null!, CancellationToken.None)
        ];
    }

    private static async Task<IActionResult[]> InvokeEveryMutationAsync(ControllerHarness h)
    {
        var id = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        return
        [
            await h.Handover.Accept(id, CancellationToken.None),
            await h.Handover.Reject(id, null!, CancellationToken.None),
            await h.Handover.Cancel(id, CancellationToken.None),
            await h.Baseline.CreateInitialBaseline(id, CancellationToken.None),
            await h.ChangeRequests.Create(id, null!, CancellationToken.None),
            await h.ChangeRequests.UpdateDraft(id, null!, CancellationToken.None),
            await h.ChangeRequests.Submit(id, null!, CancellationToken.None),
            await h.ChangeRequests.Approve(id, null!, CancellationToken.None),
            await h.ChangeRequests.Reject(id, null!, CancellationToken.None),
            await h.ChangeRequests.Cancel(id, CancellationToken.None),
            await h.ChangeRequests.Remediate(id, CancellationToken.None),
            await h.ChangeRequests.Reassign(id, assignmentId, null!, CancellationToken.None),
            await h.People.Add(id, null!, CancellationToken.None),
            await h.People.Update(id, Guid.NewGuid(), null!, CancellationToken.None),
            await h.People.Remove(id, Guid.NewGuid(), null!, CancellationToken.None),
            await h.People.ClearSponsor(id, CancellationToken.None),
            await h.Stakeholders.AddInternal(id, null!, CancellationToken.None),
            await h.Stakeholders.AddContact(id, null!, CancellationToken.None),
            await h.Stakeholders.Update(id, Guid.NewGuid(), null!, CancellationToken.None),
            await h.Stakeholders.Revoke(id, Guid.NewGuid(), null!, CancellationToken.None),
            await h.Stakeholders.Remove(id, Guid.NewGuid(), null, CancellationToken.None),
            await h.Invitations.Send(id, Guid.NewGuid(), CancellationToken.None),
            await h.Invitations.Revoke(id, Guid.NewGuid(), CancellationToken.None),
            await h.ProjectWorkflow.Save(id, "Risk", null!, CancellationToken.None)
        ];
    }

    private static ControllerHarness CreateHarness(IEnumerable<Claim> claims, Guid contextUserId, Guid tenantId, ManagementViewMode viewMode)
    {
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            contextUserId, tenantId, ManagementPerspective.ProjectManager, viewMode,
            null, null, null, ManagementScope.Empty));

        var handoverService = new Mock<IProjectHandoverRequestService>();
        var baselineService = new Mock<IProjectBaselineService>();
        var changeRequestService = new Mock<IProjectChangeRequestService>();
        var peopleService = new Mock<IProjectPeopleService>();
        var authorization = new Mock<IAuthorizationService>();
        var stakeholderService = new Mock<IProjectStakeholderService>();
        var invitationService = new Mock<IStakeholderInvitationService>();
        var workflowService = new Mock<IProjectWorkflowConfigurationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "project-management-actor-test"));

        HandoverRequestController handover = new(handoverService.Object, management);
        ProjectBaselineController baseline = new(baselineService.Object, management);
        ProjectChangeRequestController changeRequests = new(changeRequestService.Object, management);
        ProjectPeopleController people = new(peopleService.Object, authorization.Object, management);
        ProjectStakeholderController stakeholders = new(stakeholderService.Object, management);
        StakeholderInvitationController invitations = new(invitationService.Object, management);
        ProjectWorkflowController projectWorkflow = new(workflowService.Object, management);
        SetUser(principal, handover, baseline, changeRequests, people, stakeholders, invitations, projectWorkflow);

        return new ControllerHarness(
            handover, baseline, changeRequests, people, stakeholders, invitations, projectWorkflow,
            handoverService, baselineService, changeRequestService, peopleService, authorization,
            stakeholderService, invitationService, workflowService);
    }

    private static void SetUser(ClaimsPrincipal principal, params ControllerBase[] controllers)
    {
        foreach (var controller in controllers)
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
    }

    private sealed record ControllerHarness(
        HandoverRequestController Handover,
        ProjectBaselineController Baseline,
        ProjectChangeRequestController ChangeRequests,
        ProjectPeopleController People,
        ProjectStakeholderController Stakeholders,
        StakeholderInvitationController Invitations,
        ProjectWorkflowController ProjectWorkflow,
        Mock<IProjectHandoverRequestService> HandoverService,
        Mock<IProjectBaselineService> BaselineService,
        Mock<IProjectChangeRequestService> ChangeRequestService,
        Mock<IProjectPeopleService> PeopleService,
        Mock<IAuthorizationService> Authorization,
        Mock<IProjectStakeholderService> StakeholderService,
        Mock<IStakeholderInvitationService> InvitationService,
        Mock<IProjectWorkflowConfigurationService> WorkflowService)
    {
        public void VerifyNoServiceCalls()
        {
            HandoverService.VerifyNoOtherCalls();
            BaselineService.VerifyNoOtherCalls();
            ChangeRequestService.VerifyNoOtherCalls();
            PeopleService.VerifyNoOtherCalls();
            Authorization.VerifyNoOtherCalls();
            StakeholderService.VerifyNoOtherCalls();
            InvitationService.VerifyNoOtherCalls();
            WorkflowService.VerifyNoOtherCalls();
        }
    }
}
