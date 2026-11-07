using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Approvals.Application;

public class ApprovalService : IApprovalService
{
    private const string ViewPermission = "ApprovalRequest.View";
    private const string SubmitPermission = "ApprovalRequest.Submit";
    private const string ApprovePermission = "ApprovalRequest.Approve";
    private const string RejectPermission = "ApprovalRequest.Reject";
    private const string DelegatePermission = "ApprovalRequest.Delegate";
    private const string CancelPermission = "ApprovalRequest.Cancel";

    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IApprovalDelegationEligibilityLookup _delegationEligibility;

    public ApprovalService(
        DbContext context,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        IApprovalDelegationEligibilityLookup delegationEligibility)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _delegationEligibility = delegationEligibility;
    }

    public async Task<Result<ApprovalRequest>> GetByIdAsync(Guid id, Guid callerId, CancellationToken cancellationToken = default)
    {
        if (callerId == Guid.Empty) return Result<ApprovalRequest>.Failure(Error.NotFound());
        if (!HasPermission(ViewPermission))
            return Result<ApprovalRequest>.Failure(Error.Forbidden($"{ViewPermission} permission is required."));
        if (EnsureTenantContext(out var tenantId) is { } tenantContextError)
            return Result<ApprovalRequest>.Failure(tenantContextError);

        var request = await VisibleQuery(callerId)
            .Include(a => a.Actions)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (request is null) return Result<ApprovalRequest>.Failure(Error.NotFound());

        // Defense-in-depth on top of the global tenant query filter.
        if (EnsureRecordTenant(request, tenantId) is { } recordTenantError)
            return Result<ApprovalRequest>.Failure(recordTenantError);

        return Result<ApprovalRequest>.Success(request);
    }

    public async Task<Result<PagedResult<ApprovalRequest>>> GetPagedAsync(int page, int pageSize, Guid callerId, string? status = null, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (callerId == Guid.Empty) return Result<PagedResult<ApprovalRequest>>.Failure(Error.Unauthorized());
        if (!HasPermission(ViewPermission))
            return Result<PagedResult<ApprovalRequest>>.Failure(Error.Forbidden($"{ViewPermission} permission is required."));
        if (EnsureTenantContext(out var tenantId) is { } tenantContextError)
            return Result<PagedResult<ApprovalRequest>>.Failure(tenantContextError);

        // The explicit tenant predicate mirrors the global filter so the read stays
        // tenant-bound even if that filter is ever bypassed.
        var query = VisibleQuery(callerId).Where(a => a.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);
        if (userId.HasValue) query = query.Where(a => a.RequestedBy == userId || a.CurrentApproverId == userId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(a => a.RequestedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Result<PagedResult<ApprovalRequest>>.Success(new PagedResult<ApprovalRequest>(items, page, pageSize, total));
    }

    public async Task<Result<ApprovalRequest>> SubmitAsync(Guid id, Guid callerId, string? comments = null, CancellationToken cancellationToken = default)
    {
        if (callerId == Guid.Empty) return Result<ApprovalRequest>.Failure(Error.Unauthorized());
        if (EnsureTenantContext(out var tenantId) is { } tenantContextError)
            return Result<ApprovalRequest>.Failure(tenantContextError);

        var request = await VisibleQuery(callerId)
            .Include(a => a.Actions)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (request is null) return Result<ApprovalRequest>.Failure(Error.NotFound());
        if (EnsureRecordTenant(request, tenantId) is { } recordTenantError)
            return Result<ApprovalRequest>.Failure(recordTenantError);

        if (!request.RequestedBy.HasValue || !request.RequestedBy.Value.Equals(callerId))
            return Result<ApprovalRequest>.Failure(Error.Forbidden("Only the requester can submit an approval request."));

        if (!HasPermission(SubmitPermission))
            return Result<ApprovalRequest>.Failure(Error.Forbidden($"{SubmitPermission} permission is required."));

        var oldStatus = request.Status;
        try
        {
            request.Submit(callerId.ToString());
        }
        catch (InvalidOperationException ex)
        {
            // A non-Draft submit is an invalid request against the current state.
            return Result<ApprovalRequest>.Failure(Error.Validation(ex.Message));
        }
        // P0-03-04: append the audit row to this same change tracker first, then
        // persist decision + audit in ONE transactional SaveChanges. A failed audit
        // aborts the mutation before anything is committed.
        if (await AppendAuditAsync(request.Id, request.TenantId, "Submitted", new { status = new { from = oldStatus, to = request.Status }, comments }, callerId.ToString(), cancellationToken) is { } auditError)
            return Result<ApprovalRequest>.Failure(auditError);

        if (await PersistWithAuditAsync(cancellationToken) is { } persistError)
            return Result<ApprovalRequest>.Failure(persistError);

        return Result<ApprovalRequest>.Success(request);
    }

    public async Task<Result<ApprovalRequest>> ApproveAsync(Guid id, Guid callerId, string? comments = null, CancellationToken cancellationToken = default)
    {
        if (callerId == Guid.Empty) return Result<ApprovalRequest>.Failure(Error.Unauthorized());
        if (EnsureTenantContext(out var tenantId) is { } tenantContextError)
            return Result<ApprovalRequest>.Failure(tenantContextError);

        var request = await VisibleQuery(callerId)
            .Include(a => a.Actions)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (request is null) return Result<ApprovalRequest>.Failure(Error.NotFound());
        if (EnsureRecordTenant(request, tenantId) is { } recordTenantError)
            return Result<ApprovalRequest>.Failure(recordTenantError);

        if (request.CurrentApproverId is null || !request.CurrentApproverId.Value.Equals(callerId))
            return Result<ApprovalRequest>.Failure(Error.Forbidden("Only the current assignee can approve this request."));

        // Separation of duties: the requester may never decide their own request.
        if (request.RequestedBy.HasValue && request.RequestedBy.Value.Equals(callerId))
            return Result<ApprovalRequest>.Failure(Error.Forbidden("The requester cannot approve their own request."));

        if (!HasPermission(ApprovePermission))
            return Result<ApprovalRequest>.Failure(Error.Forbidden($"{ApprovePermission} permission is required."));

        var oldStatus = request.Status;
        try
        {
            request.Approve(callerId, comments, callerId.ToString());
        }
        catch (InvalidOperationException ex)
        {
            // Status already changed (e.g. decided elsewhere) or the assignee moved on.
            return Result<ApprovalRequest>.Failure(Error.Conflict(ex.Message));
        }
        // P0-03-04: audit row appended to the same change tracker, then decision +
        // audit persisted in ONE transactional SaveChanges; audit failure aborts.
        if (await AppendAuditAsync(request.Id, request.TenantId, "Approved", new { status = new { from = oldStatus, to = request.Status }, comments }, callerId.ToString(), cancellationToken) is { } auditError)
            return Result<ApprovalRequest>.Failure(auditError);

        if (await PersistWithAuditAsync(cancellationToken) is { } persistError)
            return Result<ApprovalRequest>.Failure(persistError);

        return Result<ApprovalRequest>.Success(request);
    }

    public async Task<Result<ApprovalRequest>> RejectAsync(Guid id, Guid callerId, string comments, CancellationToken cancellationToken = default)
    {
        if (callerId == Guid.Empty) return Result<ApprovalRequest>.Failure(Error.Unauthorized());
        if (EnsureTenantContext(out var tenantId) is { } tenantContextError)
            return Result<ApprovalRequest>.Failure(tenantContextError);

        var request = await VisibleQuery(callerId)
            .Include(a => a.Actions)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (request is null) return Result<ApprovalRequest>.Failure(Error.NotFound());
        if (EnsureRecordTenant(request, tenantId) is { } recordTenantError)
            return Result<ApprovalRequest>.Failure(recordTenantError);

        if (request.CurrentApproverId is null || !request.CurrentApproverId.Value.Equals(callerId))
            return Result<ApprovalRequest>.Failure(Error.Forbidden("Only the current assignee can reject this request."));

        // Separation of duties: the requester may never decide their own request.
        if (request.RequestedBy.HasValue && request.RequestedBy.Value.Equals(callerId))
            return Result<ApprovalRequest>.Failure(Error.Forbidden("The requester cannot reject their own request."));

        if (!HasPermission(RejectPermission))
            return Result<ApprovalRequest>.Failure(Error.Forbidden($"{RejectPermission} permission is required."));

        if (string.IsNullOrWhiteSpace(comments))
            return Result<ApprovalRequest>.Failure(Error.Validation("A rejection comment is required."));

        var oldStatus = request.Status;
        try
        {
            request.Reject(callerId, comments, callerId.ToString());
        }
        catch (InvalidOperationException ex)
        {
            // Status already changed (e.g. decided elsewhere) or the assignee moved on.
            return Result<ApprovalRequest>.Failure(Error.Conflict(ex.Message));
        }
        // P0-03-04: audit row appended to the same change tracker, then decision +
        // audit persisted in ONE transactional SaveChanges; audit failure aborts.
        if (await AppendAuditAsync(request.Id, request.TenantId, "Rejected", new { status = new { from = oldStatus, to = request.Status }, comments }, callerId.ToString(), cancellationToken) is { } auditError)
            return Result<ApprovalRequest>.Failure(auditError);

        if (await PersistWithAuditAsync(cancellationToken) is { } persistError)
            return Result<ApprovalRequest>.Failure(persistError);

        return Result<ApprovalRequest>.Success(request);
    }

    public async Task<Result<ApprovalRequest>> DelegateAsync(Guid id, Guid callerId, Guid toUserId, string? reason = null, CancellationToken cancellationToken = default)
    {
        if (callerId == Guid.Empty) return Result<ApprovalRequest>.Failure(Error.Unauthorized());
        if (EnsureTenantContext(out var tenantId) is { } tenantContextError)
            return Result<ApprovalRequest>.Failure(tenantContextError);

        var request = await VisibleQuery(callerId)
            .Include(a => a.Actions)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (request is null) return Result<ApprovalRequest>.Failure(Error.NotFound());
        if (EnsureRecordTenant(request, tenantId) is { } recordTenantError)
            return Result<ApprovalRequest>.Failure(recordTenantError);

        if ((request.CurrentApproverId is null || !request.CurrentApproverId.Value.Equals(callerId))
            && (!request.RequestedBy.HasValue || !request.RequestedBy.Value.Equals(callerId)))
            return Result<ApprovalRequest>.Failure(Error.Forbidden("Only the current assignee or requester can delegate this request."));

        if (!HasPermission(DelegatePermission))
            return Result<ApprovalRequest>.Failure(Error.Forbidden($"{DelegatePermission} permission is required."));

        if (toUserId == Guid.Empty)
            return Result<ApprovalRequest>.Failure(Error.Validation("A delegate target user is required."));

        // Separation of duties: delegating to the requester would hand the requester
        // their own decision. When the caller IS the requester this is delegation to
        // self; both cases fall out of the same rule. Requester-initiated delegation
        // to a different eligible user remains allowed.
        if (request.RequestedBy.HasValue && toUserId.Equals(request.RequestedBy.Value))
            return Result<ApprovalRequest>.Failure(Error.Forbidden("A request cannot be delegated to its own requester."));

        var eligibility = await _delegationEligibility.CheckAsync(toUserId, tenantId, ApprovePermission, cancellationToken);
        if (!eligibility.UserExists)
            return Result<ApprovalRequest>.Failure(Error.Forbidden("The delegation target user does not exist."));
        if (!eligibility.IsActive)
            return Result<ApprovalRequest>.Failure(Error.Forbidden("The delegation target user account is inactive."));
        if (!eligibility.IsSameTenant)
            return Result<ApprovalRequest>.Failure(Error.Forbidden("The delegation target user does not belong to the current tenant."));
        if (!eligibility.HasRequiredPermission)
            return Result<ApprovalRequest>.Failure(Error.Forbidden($"The delegation target user does not hold the {ApprovePermission} permission."));

        var oldApprover = request.CurrentApproverId;
        try
        {
            request.Delegate(toUserId, reason, callerId.ToString());
        }
        catch (InvalidOperationException ex)
        {
            // Terminal status already reached (e.g. decided elsewhere).
            return Result<ApprovalRequest>.Failure(Error.Conflict(ex.Message));
        }
        // P0-03-04: audit row appended to the same change tracker, then assignment +
        // audit persisted in ONE transactional SaveChanges; audit failure aborts.
        if (await AppendAuditAsync(request.Id, request.TenantId, "Delegated", new { from = oldApprover, to = toUserId, reason }, callerId.ToString(), cancellationToken) is { } auditError)
            return Result<ApprovalRequest>.Failure(auditError);

        if (await PersistWithAuditAsync(cancellationToken) is { } persistError)
            return Result<ApprovalRequest>.Failure(persistError);

        return Result<ApprovalRequest>.Success(request);
    }

    public async Task<Result<ApprovalRequest>> CancelAsync(Guid id, Guid callerId, CancellationToken cancellationToken = default)
    {
        if (callerId == Guid.Empty) return Result<ApprovalRequest>.Failure(Error.Unauthorized());
        if (EnsureTenantContext(out var tenantId) is { } tenantContextError)
            return Result<ApprovalRequest>.Failure(tenantContextError);

        var request = await VisibleQuery(callerId)
            .Include(a => a.Actions)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (request is null) return Result<ApprovalRequest>.Failure(Error.NotFound());
        if (EnsureRecordTenant(request, tenantId) is { } recordTenantError)
            return Result<ApprovalRequest>.Failure(recordTenantError);

        if (!request.RequestedBy.HasValue || !request.RequestedBy.Value.Equals(callerId))
            return Result<ApprovalRequest>.Failure(Error.Forbidden("Only the requester can cancel an approval request."));

        if (!HasPermission(CancelPermission))
            return Result<ApprovalRequest>.Failure(Error.Forbidden($"{CancelPermission} permission is required."));

        var oldStatus = request.Status;
        try
        {
            request.Cancel(callerId.ToString());
        }
        catch (InvalidOperationException ex)
        {
            // Status already changed (e.g. already decided), so the cancel is stale.
            return Result<ApprovalRequest>.Failure(Error.Conflict(ex.Message));
        }
        // P0-03-04: audit row appended to the same change tracker, then decision +
        // audit persisted in ONE transactional SaveChanges; audit failure aborts.
        if (await AppendAuditAsync(request.Id, request.TenantId, "Cancelled", new { status = new { from = oldStatus, to = request.Status } }, callerId.ToString(), cancellationToken) is { } auditError)
            return Result<ApprovalRequest>.Failure(auditError);

        if (await PersistWithAuditAsync(cancellationToken) is { } persistError)
            return Result<ApprovalRequest>.Failure(persistError);

        return Result<ApprovalRequest>.Success(request);
    }

    /// <summary>
    /// The caller's own approval workload: requests they raised, requests assigned to them,
    /// and requests they have already acted on. Tenant isolation is applied by the global
    /// filter and re-checked explicitly per record/row by the callers.
    /// </summary>
    private IQueryable<ApprovalRequest> VisibleQuery(Guid callerId) =>
        _context.Set<ApprovalRequest>().AsQueryable().Where(a =>
            a.RequestedBy == callerId
            || a.CurrentApproverId == callerId
            || a.Actions.Any(action => action.ApproverId == callerId));

    private bool HasPermission(string permissionKey)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        return principal is not null && principal.HasClaim("permission", permissionKey);
    }

    /// <summary>
    /// Resolves the trusted tenant for this operation: the authenticated tenantId claim
    /// always wins, falling back to <see cref="TrustedTenantExecutionContext.CurrentTenantId"/>
    /// for trusted server-side work without an HTTP request, mirroring the established
    /// repo pattern (see WorkItemWorkflowIntegration.CurrentTenantId). Returns an error
    /// when no trusted tenant context exists at all.
    /// </summary>
    private Error? EnsureTenantContext(out Guid tenantId)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            tenantId = httpContext.User.Identity?.IsAuthenticated == true
                && Guid.TryParse(httpContext.User.FindFirst("tenantId")?.Value, out var claimTenantId)
                && claimTenantId != Guid.Empty
                    ? claimTenantId
                    : Guid.Empty;
        }
        else
        {
            tenantId = TrustedTenantExecutionContext.CurrentTenantId is { } trustedTenantId && trustedTenantId != Guid.Empty
                ? trustedTenantId
                : Guid.Empty;
        }

        return tenantId == Guid.Empty
            ? Error.Forbidden("An authenticated tenant context is required.")
            : null;
    }

    /// <summary>
    /// Defense-in-depth record check: the resolved record must belong to the caller's
    /// trusted tenant even though the EF global query filter already scopes every read.
    /// </summary>
    private static Error? EnsureRecordTenant(ApprovalRequest request, Guid tenantId) =>
        request.TenantId == tenantId ? null : Error.NotFound("Approval request not found.");

    /// <summary>
    /// Appends the audit entry to the CURRENT scoped DbContext without saving it,
    /// so the caller's single SaveChanges persists the mutation and its audit row
    /// atomically (P0-03-04). The audit service's failure result is surfaced,
    /// never swallowed: the caller must abort before any save, so no state change
    /// can ever be committed without its audit record.
    /// </summary>
    private async Task<Error?> AppendAuditAsync(Guid entityId, Guid tenantId, string action, object changes, string userId, CancellationToken cancellationToken)
    {
        var actorName = _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var appendResult = await _auditService.AppendAsync(new NEXUS.Modules.Audit.Domain.AuditEntry
        {
            TenantId = tenantId,
            EntityType = "ApprovalRequest",
            EntityId = entityId,
            Action = action,
            Changes = System.Text.Json.JsonSerializer.Serialize(changes),
            UserId = Guid.TryParse(userId, out var uid) ? uid : null,
            UserName = actorName,
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);
        return appendResult.IsFailure ? appendResult.Error : null;
    }

    /// <summary>
    /// Persists the pending ApprovalRequest mutation and its already-appended audit
    /// row in ONE SaveChangesAsync inside an explicit transaction (P0-03-04),
    /// following the WorkItemWorkflowIntegration transaction pattern. A lost
    /// optimistic-concurrency race (P0-03-05) rolls back and returns a controlled
    /// 409 conflict; every other persistence failure rolls back and rethrows, so no
    /// partial decision (status change, ApprovalAction row or audit row) can be
    /// committed and no error is converted into success.
    /// </summary>
    private async Task<Error?> PersistWithAuditAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict("This approval request was changed by another user. Refresh and retry.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
