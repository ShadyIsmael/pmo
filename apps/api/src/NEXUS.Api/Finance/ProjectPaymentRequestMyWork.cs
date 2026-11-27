using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

public sealed partial class ProjectPaymentRequestWorkflow
{
    public async Task<Result<IReadOnlyList<PendingPaymentRequestApprovalDto>>> GetMyPendingApprovalsAsync(
        Guid tenantId, Guid actorId, CancellationToken cancellationToken)
    {
        var current = management.Current;
        if (tenantId == Guid.Empty || actorId == Guid.Empty || current is null
            || current.TenantId != tenantId || current.UserId != actorId)
            return Result<IReadOnlyList<PendingPaymentRequestApprovalDto>>.Failure(Error.NotFound("Approval inbox is unavailable."));
        var actorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (actorError is not null)
            return Result<IReadOnlyList<PendingPaymentRequestApprovalDto>>.Failure(actorError);
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        if (!effective.Contains(ApprovePermission))
            return Result<IReadOnlyList<PendingPaymentRequestApprovalDto>>.Failure(Error.Forbidden(
                "The ProjectPaymentRequest.Approve permission is required."));

        var rows = await (
            from assignment in db.PaymentRequestApproverAssignments.IgnoreQueryFilters().AsNoTracking()
            join detail in db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AsNoTracking()
                on new { assignment.TenantId, assignment.ApprovalRequestId }
                equals new { detail.TenantId, detail.ApprovalRequestId }
            join request in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                on new { assignment.TenantId, assignment.ApprovalRequestId }
                equals new { request.TenantId, ApprovalRequestId = request.Id }
            join project in db.Projects.IgnoreQueryFilters().AsNoTracking()
                on new { detail.TenantId, detail.ProjectId }
                equals new { project.TenantId, ProjectId = project.Id }
            where assignment.TenantId == tenantId && !assignment.IsDeleted
                && assignment.ApproverUserId == actorId && assignment.State == PaymentRequestAssignmentStates.Pending
                && assignment.ApprovalCycleId == detail.CurrentApprovalCycleId
                && detail.ProjectId == project.Id && !project.IsDeleted
                && detail.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                && request.Status == PaymentRequestStatuses.Pending && !request.IsDeleted
                && request.RequestedBy != actorId
            select new { assignment, detail, request, project }
        ).ToListAsync(cancellationToken);

        var results = new List<PendingPaymentRequestApprovalDto>();
        foreach (var row in rows.OrderBy(item => item.detail.SubmittedAt).ThenBy(item => item.request.Id))
        {
            if (current.Scope.OrganizationIds.Count > 0
                && current.Scope.ProjectOrganizationIds.TryGetValue(row.project.Id, out var organizationId)
                && !current.Scope.OrganizationIds.Contains(organizationId))
                continue;
            if (!(await GetEligibleApproverIdsAsync(tenantId, row.project.Id, cancellationToken)).Contains(actorId))
                continue;
            var assignments = await GetAssignmentsAsync(tenantId, row.request.Id, tracked: false,
                cancellationToken, row.detail.CurrentApprovalCycleId);
            var actions = await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted
                    && item.ApprovalRequestId == row.request.Id)
                .ToListAsync(cancellationToken);
            var states = BuildStates(assignments, actions);
            var canDecide = row.detail.ApprovalMode == PaymentRequestApprovalModes.Sequential
                ? PaymentRequestWorkflowPolicy.CanDecideSequentially(actorId, row.request.Status, states)
                : row.detail.ApprovalMode == PaymentRequestApprovalModes.Parallel
                    && PaymentRequestWorkflowPolicy.CanDecideInParallel(actorId, row.request.Status, states);
            if (!canDecide) continue;
            var requesterName = await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(user => user.TenantId == tenantId && user.Id == row.request.RequestedBy
                    && user.IsActive && !user.IsDeleted)
                .Select(user => $"{user.FirstName} {user.LastName}".Trim())
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
            results.Add(new PendingPaymentRequestApprovalDto(
                row.project.Id, row.project.Name, row.request.Id, Reference(row.request), row.request.Title,
                row.request.Amount ?? 0m, row.request.Currency ?? string.Empty, row.request.Status,
                requesterName, row.detail.SubmittedAt ?? row.request.RequestedAt,
                row.detail.ApprovalMode, row.request.RowVersion, true));
        }

        return Result<IReadOnlyList<PendingPaymentRequestApprovalDto>>.Success(results);
    }
}
