using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Documents;

public sealed class TemplateDeletionWorkflowService(
    AppDbContext db,
    IAuditService audit,
    IHttpContextAccessor httpContextAccessor,
    IManagementContextAccessor managementContext,
    ITemplateDeletionApproverResolver approverResolver,
    ITemplateDeletionReferenceValidator referenceValidator,
    ITemplateDeletionNotificationPublisher notificationPublisher,
    IStorageProvider storage,
    ILogger<TemplateDeletionWorkflowService> logger) : ITemplateDeletionWorkflowService
{
    private const int MaximumReasonLength = 4000;
    private const int MaximumCommentsLength = 4000;

    public async Task<Result<TemplateDeletionRequestDto>> RequestAsync(
        CreateTemplateDeletionRequest input,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId, out var tenantId))
            return Result<TemplateDeletionRequestDto>.Failure(Error.Unauthorized());
        if (!HasPermission(TemplateDeletionPermissions.Request)
            || !HasPermission(DocumentPermissions.View))
            return Result<TemplateDeletionRequestDto>.Failure(Error.Forbidden());
        if (input.TemplateId == Guid.Empty)
            return Result<TemplateDeletionRequestDto>.Failure(Error.Validation("A template is required.", "templateId"));
        var reason = input.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                "TEMPLATE_DELETION_REASON_REQUIRED", "A deletion reason is required.", 400, "reason"));
        if (reason.Length > MaximumReasonLength)
            return Result<TemplateDeletionRequestDto>.Failure(Error.Validation(
                $"The deletion reason must be no more than {MaximumReasonLength} characters.", "reason"));
        if (!HasRequestScope(actorId, tenantId, managementContext.Current, null))
            return Result<TemplateDeletionRequestDto>.Failure(Error.Forbidden());

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var template = await db.Documents.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(document => document.Id == input.TemplateId
                    && document.TenantId == tenantId
                    && !document.IsDeleted
                    && !document.IsDeletionApproved
                    && document.IsTemplate,
                    cancellationToken);
            if (template is null || !HasRequestScope(actorId, tenantId, managementContext.Current, template.OrganizationId))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<TemplateDeletionRequestDto>.Failure(Error.NotFound("Template not found."));
            }

            var existingPending = await db.Set<TemplateDeletionRequest>().IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(item => item.TenantId == tenantId
                    && item.TemplateId == template.Id
                    && item.Status == TemplateDeletionRequest.StatusPendingApproval,
                    cancellationToken);
            if (existingPending)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                    "TEMPLATE_DELETION_ALREADY_PENDING", "A deletion request is already pending for this template.", 409));
            }

            var eligibleApprovers = (await approverResolver.GetEligibleApproverIdsAsync(
                    tenantId, template.OrganizationId, actorId, cancellationToken))
                .Where(id => id != Guid.Empty && id != actorId)
                .Distinct()
                .ToArray();
            if (eligibleApprovers.Length == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                    "TEMPLATE_DELETION_NO_ELIGIBLE_APPROVERS", "No eligible administrator can review this request.", 409));
            }

            var versions = await db.DocumentVersions.IgnoreQueryFilters().AsNoTracking()
                .Where(version => version.TenantId == tenantId && version.DocumentId == template.Id)
                .OrderBy(version => version.VersionNumber).ThenBy(version => version.Id)
                .ToListAsync(cancellationToken);
            var request = new TemplateDeletionRequest
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TemplateId = template.Id,
                TemplateName = template.Name,
                OwnerOrganizationId = template.OrganizationId,
                OwnerScope = template.OrganizationId.HasValue
                    ? TemplateDeletionRequest.OwnerScopeOrganization
                    : TemplateDeletionRequest.OwnerScopeTenant,
                RequesterId = actorId,
                RequesterName = GetActorName(actorId),
                Reason = reason,
                Status = TemplateDeletionRequest.StatusPendingApproval,
                RequestedAt = DateTime.UtcNow,
                TemplateMutationSnapshotHash = ComputeTemplateMutationSnapshotHash(template, versions),
                CreatedBy = actorId.ToString(),
                RowVersion = NewConcurrencyToken()
            };
            db.Set<TemplateDeletionRequest>().Add(request);

            var auditError = await AppendRequestAuditAsync(
                request, "Requested", actorId,
                new { request.Status, request.Reason, request.OwnerScope, request.OwnerOrganizationId },
                cancellationToken);
            if (auditError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<TemplateDeletionRequestDto>.Failure(auditError);
            }

            var notificationResult = await notificationPublisher.PublishPendingAsync(request, eligibleApprovers, cancellationToken);
            if (notificationResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<TemplateDeletionRequestDto>.Failure(notificationResult.Error ?? Error.Validation("The request notification could not be queued."));
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<TemplateDeletionRequestDto>.Success(ToDto(request));
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            return Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                "TEMPLATE_DELETION_ALREADY_PENDING", "A deletion request is already pending for this template.", 409));
        }
        catch (DbUpdateException exception)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            var racedPending = false;
            try
            {
                racedPending = await db.Set<TemplateDeletionRequest>().IgnoreQueryFilters().AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId
                        && item.TemplateId == input.TemplateId
                        && item.Status == TemplateDeletionRequest.StatusPendingApproval,
                        cancellationToken);
            }
            catch (Exception recheckException) when (recheckException is not OperationCanceledException)
            {
                logger.LogWarning(recheckException,
                    "Could not classify a template deletion request persistence conflict for template {TemplateId}.", input.TemplateId);
            }
            if (racedPending)
                return Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                    "TEMPLATE_DELETION_ALREADY_PENDING", "A deletion request is already pending for this template.", 409));
            if (IsSqlServerDeadlock(exception))
                return Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                    "TEMPLATE_DELETION_ALREADY_PENDING", "Another request conflicted with this submission. Refresh and check the template history.", 409));
            throw;
        }
    }

    public async Task<Result<TemplateDeletionPagedResult<TemplateDeletionRequestDto>>> GetTemplateHistoryAsync(
        Guid templateId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId, out var tenantId))
            return Result<TemplateDeletionPagedResult<TemplateDeletionRequestDto>>.Failure(Error.Unauthorized());
        if (!HasPermission(TemplateDeletionPermissions.View) || !HasPermission(DocumentPermissions.View))
            return Result<TemplateDeletionPagedResult<TemplateDeletionRequestDto>>.Failure(Error.Forbidden());
        if (templateId == Guid.Empty)
            return Result<TemplateDeletionPagedResult<TemplateDeletionRequestDto>>.Failure(Error.Validation("A template is required.", "templateId"));
        (page, pageSize) = NormalizePage(page, pageSize);

        var query = db.Set<TemplateDeletionRequest>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.TemplateId == templateId);
        var total = await query.CountAsync(cancellationToken);
        if (total == 0)
        {
            var template = await db.Documents.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.Id == templateId && !item.IsDeleted && item.IsTemplate)
                .Select(item => new { item.OrganizationId })
                .FirstOrDefaultAsync(cancellationToken);
            if (template is null || !HasRequestScope(actorId, tenantId, managementContext.Current, template.OrganizationId))
                return Result<TemplateDeletionPagedResult<TemplateDeletionRequestDto>>.Failure(Error.NotFound("Template not found."));
        }
        else
        {
            var owner = await query.Select(item => item.OwnerOrganizationId).FirstOrDefaultAsync(cancellationToken);
            var hasHistoryAccess = await query.AnyAsync(item => item.RequesterId == actorId || item.ApproverId == actorId, cancellationToken)
                || await approverResolver.IsAuthorizedAdministratorAsync(
                    actorId, tenantId, owner, TemplateDeletionPermissions.View, cancellationToken);
            if (!hasHistoryAccess)
                return Result<TemplateDeletionPagedResult<TemplateDeletionRequestDto>>.Failure(Error.NotFound("Template deletion history not found."));
        }

        var rows = await query.OrderByDescending(item => item.RequestedAt).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = rows.Select(ToDto).ToArray();
        return Result<TemplateDeletionPagedResult<TemplateDeletionRequestDto>>.Success(
            new TemplateDeletionPagedResult<TemplateDeletionRequestDto>(items, page, pageSize, total));
    }

    public async Task<Result<TemplateDeletionPagedResult<TemplateDeletionRequestDto>>> GetInboxAsync(
        string status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId, out var tenantId))
            return Result<TemplateDeletionPagedResult<TemplateDeletionRequestDto>>.Failure(Error.Unauthorized());
        if (!HasPermission(TemplateDeletionPermissions.View) || !HasPermission(DocumentPermissions.View))
            return Result<TemplateDeletionPagedResult<TemplateDeletionRequestDto>>.Failure(Error.Forbidden());
        if (!string.Equals(status, TemplateDeletionRequest.StatusPendingApproval, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "History", StringComparison.OrdinalIgnoreCase))
        {
            return Result<TemplateDeletionPagedResult<TemplateDeletionRequestDto>>.Failure(LocalizedError(
                "TEMPLATE_DELETION_STATUS_INVALID", "Choose PendingApproval or History.", 400, "status"));
        }
        (page, pageSize) = NormalizePage(page, pageSize);

        var isPendingView = string.Equals(status, TemplateDeletionRequest.StatusPendingApproval, StringComparison.OrdinalIgnoreCase);
        var requests = await db.Set<TemplateDeletionRequest>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && (isPendingView
                    ? item.Status == TemplateDeletionRequest.StatusPendingApproval
                    : item.Status != TemplateDeletionRequest.StatusPendingApproval))
            .OrderByDescending(item => item.RequestedAt).ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        var visible = new List<TemplateDeletionRequest>();
        var authorizationByOwner = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var request in requests)
        {
            if (isPendingView && request.RequesterId == actorId)
                continue;

            if (!isPendingView && (request.RequesterId == actorId || request.ApproverId == actorId))
            {
                visible.Add(request);
                continue;
            }

            var ownerKey = request.OwnerOrganizationId?.ToString("D") ?? "tenant";
            if (!authorizationByOwner.TryGetValue(ownerKey, out var ownerAuthorized))
            {
                if (isPendingView)
                {
                    var eligible = await approverResolver.GetEligibleApproverIdsAsync(
                        tenantId, request.OwnerOrganizationId, request.RequesterId, cancellationToken);
                    ownerAuthorized = eligible.Contains(actorId);
                }
                else
                {
                    ownerAuthorized = await approverResolver.IsAuthorizedAdministratorAsync(
                        actorId, tenantId, request.OwnerOrganizationId, TemplateDeletionPermissions.View, cancellationToken);
                }
                authorizationByOwner[ownerKey] = ownerAuthorized;
            }
            if (ownerAuthorized) visible.Add(request);
        }

        var total = visible.Count;
        var items = visible.Skip((page - 1) * pageSize).Take(pageSize).Select(ToDto).ToArray();
        return Result<TemplateDeletionPagedResult<TemplateDeletionRequestDto>>.Success(
            new TemplateDeletionPagedResult<TemplateDeletionRequestDto>(items, page, pageSize, total));
    }

    public Task<Result<TemplateDeletionRequestDto>> ApproveAsync(
        Guid requestId,
        string? comments,
        CancellationToken cancellationToken = default) => DecideAsync(
            requestId, comments, isApproval: true, cancellationToken);

    public Task<Result<TemplateDeletionRequestDto>> RejectAsync(
        Guid requestId,
        string? comments,
        CancellationToken cancellationToken = default) => DecideAsync(
            requestId, comments, isApproval: false, cancellationToken);

    private async Task<Result<TemplateDeletionRequestDto>> DecideAsync(
        Guid requestId,
        string? comments,
        bool isApproval,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var tenantId))
            return Result<TemplateDeletionRequestDto>.Failure(Error.Unauthorized());
        var permission = isApproval ? TemplateDeletionPermissions.Approve : TemplateDeletionPermissions.Reject;
        if (!HasPermission(permission) || !HasPermission(TemplateDeletionPermissions.View) || !HasPermission(DocumentPermissions.View))
            return Result<TemplateDeletionRequestDto>.Failure(Error.Forbidden());
        if (managementContext.Current?.EffectiveReadOnly == true)
            return Result<TemplateDeletionRequestDto>.Failure(Error.Forbidden());
        var normalizedComments = comments?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedComments))
            return Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                "TEMPLATE_DELETION_DECISION_COMMENTS_REQUIRED", "Decision comments are required.", 400, "comments"));
        if (normalizedComments.Length > MaximumCommentsLength)
            return Result<TemplateDeletionRequestDto>.Failure(Error.Validation(
                $"Decision comments must be no more than {MaximumCommentsLength} characters.", "comments"));
        if (requestId == Guid.Empty)
            return Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                "TEMPLATE_DELETION_REQUEST_NOT_FOUND", "Template deletion request not found.", 404));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var request = await db.Set<TemplateDeletionRequest>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == requestId, cancellationToken);
            if (request is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                    "TEMPLATE_DELETION_REQUEST_NOT_FOUND", "Template deletion request not found.", 404));
            }
            if (request.RequesterId == actorId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                    "TEMPLATE_DELETION_SELF_APPROVAL", "The requester cannot decide their own deletion request.", 403));
            }
            if (request.Status != TemplateDeletionRequest.StatusPendingApproval)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                    "TEMPLATE_DELETION_ALREADY_DECIDED", "This deletion request has already been decided.", 409));
            }

            var isAuthorized = await approverResolver.IsAuthorizedAdministratorAsync(
                actorId, tenantId, request.OwnerOrganizationId, permission, cancellationToken);
            if (!isAuthorized)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<TemplateDeletionRequestDto>.Failure(Error.Forbidden());
            }

            if (!isApproval)
                return await RejectInTransactionAsync(request, actorId, normalizedComments, transaction, cancellationToken);

            var template = await db.Documents.IgnoreQueryFilters()
                .FirstOrDefaultAsync(document => document.TenantId == tenantId
                    && document.Id == request.TemplateId
                    && !document.IsDeleted
                    && document.IsTemplate,
                    cancellationToken);
            var versions = template is null
                ? new List<DocumentVersion>()
                : await db.DocumentVersions.IgnoreQueryFilters().AsNoTracking()
                    .Where(version => version.TenantId == tenantId && version.DocumentId == template.Id)
                    .OrderBy(version => version.VersionNumber).ThenBy(version => version.Id)
                    .ToListAsync(cancellationToken);
            var decidingVersionIds = versions.Select(version => version.Id).ToArray();
            if (template is null || !string.Equals(
                    request.TemplateMutationSnapshotHash,
                    ComputeTemplateMutationSnapshotHash(template, versions),
                    StringComparison.Ordinal))
            {
                var stale = await MarkStaleAsync(request, actorId, normalizedComments,
                    "The template changed after this deletion request was submitted.", cancellationToken);
                if (stale.IsFailure && stale.Error?.Code != "TEMPLATE_DELETION_TEMPLATE_CHANGED")
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return stale;
                }
                await transaction.CommitAsync(cancellationToken);
                return stale;
            }

            var referenceCheck = await referenceValidator.CheckAsync(
                tenantId, template.Id, decidingVersionIds, cancellationToken);
            if (referenceCheck.HasProtectedReferences)
            {
                var originalValues = TemplateAuditValues(template);
                template.IsArchived = true;
                template.Status = "Archived";
                template.IsDeletionApproved = true;
                template.UpdatedBy = actorId.ToString();
                template.UpdatedAt = DateTime.UtcNow;
                request.DeletionType = TemplateDeletionRequest.DeletionTypeSoftDelete;
                await AppendTemplateAuditAsync(template, "ApprovedSoftDelete", actorId,
                    originalValues, new { referenceKinds = referenceCheck.ReferenceKinds.OrderBy(kind => kind).ToArray() }, cancellationToken);
            }
            else
            {
                request.DeletionType = TemplateDeletionRequest.DeletionTypeHardDelete;
                var keys = versions.Select(version => new TemplateDeletionStorageObject(
                        string.IsNullOrWhiteSpace(version.StorageProviderId) ? storage.ProviderId : version.StorageProviderId,
                        !string.IsNullOrWhiteSpace(version.StorageKey) ? version.StorageKey
                            : !string.IsNullOrWhiteSpace(version.StoragePath) ? version.StoragePath
                            : version.IsCurrent ? template.StoragePath : null))
                    .Append(new TemplateDeletionStorageObject(storage.ProviderId, template.StoragePath))
                    .Where(item => !string.IsNullOrWhiteSpace(item.ProviderId) && !string.IsNullOrWhiteSpace(item.StorageKey))
                    .GroupBy(item => $"{item.ProviderId.ToUpperInvariant()}\n{item.StorageKey}", StringComparer.Ordinal)
                    .Select(group => group.First())
                    .ToArray();
                if (keys.Length > 0)
                {
                    request.StorageCleanupKeysJson = JsonSerializer.Serialize(keys);
                    request.StorageCleanupStatus = TemplateDeletionRequest.CleanupStatusPending;
                }

                var allVersions = await db.DocumentVersions.IgnoreQueryFilters()
                    .Where(version => version.TenantId == tenantId && version.DocumentId == template.Id)
                    .ToListAsync(cancellationToken);
                db.DocumentVersions.RemoveRange(allVersions);
                db.Documents.Remove(template);
                await AppendTemplateAuditAsync(template, "ApprovedHardDelete", actorId,
                    TemplateAuditValues(template), new { referenceKinds = Array.Empty<string>() }, cancellationToken);
            }

            request.Status = TemplateDeletionRequest.StatusApproved;
            request.ApproverId = actorId;
            request.ApproverName = GetActorName(actorId);
            request.DecidedAt = DateTime.UtcNow;
            request.DecisionComments = normalizedComments;
            request.UpdatedBy = actorId.ToString();
            request.RowVersion = NewConcurrencyToken();

            var auditError = await AppendRequestAuditAsync(
                request, "Approved", actorId,
                new { request.Status, request.DeletionType, request.DecisionComments },
                cancellationToken);
            if (auditError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<TemplateDeletionRequestDto>.Failure(auditError);
            }

            var notificationResult = await notificationPublisher.PublishDecisionAsync(request, cancellationToken);
            if (notificationResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<TemplateDeletionRequestDto>.Failure(notificationResult.Error ?? Error.Validation("The decision notification could not be queued."));
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<TemplateDeletionRequestDto>.Success(ToDto(request));
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            var currentStatus = await TryGetCurrentStatusAsync(tenantId, requestId, cancellationToken);
            return currentStatus is not null && currentStatus != TemplateDeletionRequest.StatusPendingApproval
                ? Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                    "TEMPLATE_DELETION_ALREADY_DECIDED", "This deletion request was decided by another administrator.", 409))
                : Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                    "TEMPLATE_DELETION_REFERENCE_RECHECK_CONFLICT", "The template or its protected references changed during approval. Refresh and retry.", 409));
        }
        catch (DbUpdateException exception)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            if (IsSqlServerDeadlock(exception) || IsSqlServerForeignKeyConflict(exception))
                return Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                    "TEMPLATE_DELETION_REFERENCE_RECHECK_CONFLICT", "A protected reference or concurrent update blocked approval. Refresh and retry.", 409));

            var currentStatus = await TryGetCurrentStatusAsync(tenantId, requestId, cancellationToken);
            if (currentStatus is not null && currentStatus != TemplateDeletionRequest.StatusPendingApproval)
            {
                return Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
                    "TEMPLATE_DELETION_ALREADY_DECIDED", "This deletion request was decided by another administrator.", 409));
            }

            throw;
        }
        catch (TemplateDeletionAuditException exception)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            return Result<TemplateDeletionRequestDto>.Failure(exception.Error);
        }
    }

    private async Task<Result<TemplateDeletionRequestDto>> RejectInTransactionAsync(
        TemplateDeletionRequest request,
        Guid actorId,
        string comments,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        request.Status = TemplateDeletionRequest.StatusRejected;
        request.ApproverId = actorId;
        request.ApproverName = GetActorName(actorId);
        request.DecidedAt = DateTime.UtcNow;
        request.DecisionComments = comments;
        request.UpdatedBy = actorId.ToString();
        request.RowVersion = NewConcurrencyToken();

        var auditError = await AppendRequestAuditAsync(
            request, "Rejected", actorId,
            new { request.Status, request.DecisionComments }, cancellationToken);
        if (auditError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<TemplateDeletionRequestDto>.Failure(auditError);
        }

        var notificationResult = await notificationPublisher.PublishDecisionAsync(request, cancellationToken);
        if (notificationResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<TemplateDeletionRequestDto>.Failure(notificationResult.Error ?? Error.Validation("The decision notification could not be queued."));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<TemplateDeletionRequestDto>.Success(ToDto(request));
    }

    private async Task<Result<TemplateDeletionRequestDto>> MarkStaleAsync(
        TemplateDeletionRequest request,
        Guid actorId,
        string comments,
        string reason,
        CancellationToken cancellationToken)
    {
        request.Status = TemplateDeletionRequest.StatusStale;
        request.ApproverId = actorId;
        request.ApproverName = GetActorName(actorId);
        request.DecidedAt = DateTime.UtcNow;
        request.DecisionComments = comments;
        request.UpdatedBy = actorId.ToString();
        request.RowVersion = NewConcurrencyToken();
        var auditError = await AppendRequestAuditAsync(
            request, "Stale", actorId,
            new { request.Status, request.DecisionComments, reason }, cancellationToken);
        if (auditError is not null)
            return Result<TemplateDeletionRequestDto>.Failure(auditError);
        await db.SaveChangesAsync(cancellationToken);
        return Result<TemplateDeletionRequestDto>.Failure(LocalizedError(
            "TEMPLATE_DELETION_TEMPLATE_CHANGED", reason, 409));
    }

    private async Task<Error?> AppendRequestAuditAsync(
        TemplateDeletionRequest request,
        string action,
        Guid actorId,
        object changes,
        CancellationToken cancellationToken)
    {
        var result = await audit.AppendAsync(new AuditEntry
        {
            TenantId = request.TenantId,
            EntityType = "TemplateDeletionRequest",
            EntityId = request.Id,
            Action = action,
            Changes = JsonSerializer.Serialize(changes),
            UserId = actorId,
            UserName = GetActorName(actorId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = actorId.ToString()
        }, cancellationToken);
        return result.IsFailure ? result.Error : null;
    }

    private async Task AppendTemplateAuditAsync(
        Document template,
        string action,
        Guid actorId,
        object originalValues,
        object changes,
        CancellationToken cancellationToken)
    {
        var result = await audit.AppendAsync(new AuditEntry
        {
            TenantId = template.TenantId,
            EntityType = "DocumentTemplate",
            EntityId = template.Id,
            Action = action,
            OriginalValues = JsonSerializer.Serialize(originalValues),
            Changes = JsonSerializer.Serialize(changes),
            ChangedFields = action == "ApprovedSoftDelete" ? "IsArchived,IsDeletionApproved,Status" : "Deleted",
            UserId = actorId,
            UserName = GetActorName(actorId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = actorId.ToString()
        }, cancellationToken);
        if (result.IsFailure)
            throw new TemplateDeletionAuditException(result.Error ?? Error.Validation("The template audit event could not be recorded."));
    }

    private bool HasRequestScope(Guid userId, Guid tenantId, ManagementContext? context, Guid? organizationId)
    {
        if (context is null || context.UserId != userId || context.TenantId != tenantId || context.EffectiveReadOnly)
            return false;
        return !organizationId.HasValue
            || context.Scope.OrganizationIds.Contains(organizationId.Value)
            && context.Scope.OrganizationReferences.Any(item => item.Id == organizationId.Value);
    }

    private bool HasPermission(string permission)
    {
        // UserPermissionClaimsMiddleware rebuilds this set from live effective permissions
        // on every authenticated request, including active Deny overrides.
        var user = httpContextAccessor.HttpContext?.User;
        return user is not null && user.HasClaim("permission", permission) && !user.HasClaim("deny", permission);
    }

    private bool TryGetActor(out Guid actorId, out Guid tenantId)
    {
        var user = httpContextAccessor.HttpContext?.User;
        actorId = Guid.Empty;
        tenantId = Guid.Empty;
        return user?.Identity?.IsAuthenticated == true
            && Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out actorId)
            && actorId != Guid.Empty
            && Guid.TryParse(user.FindFirstValue("tenantId"), out tenantId)
            && tenantId != Guid.Empty;
    }

    private string GetActorName(Guid actorId)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var name = user?.FindFirstValue(ClaimTypes.Name)?.Trim();
        if (string.IsNullOrWhiteSpace(name)) name = user?.FindFirstValue("name")?.Trim();
        return string.IsNullOrWhiteSpace(name) ? actorId.ToString("D") : name;
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize)
    {
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        return (Math.Clamp(page, 1, int.MaxValue / normalizedPageSize), normalizedPageSize);
    }

    private static TemplateDeletionRequestDto ToDto(TemplateDeletionRequest item) => new(
        item.Id,
        item.TemplateId,
        item.TemplateName,
        item.OwnerOrganizationId,
        item.OwnerScope,
        item.RequesterId,
        item.RequesterName,
        item.Reason,
        item.Status,
        item.RequestedAt,
        item.ApproverId,
        item.ApproverName,
        item.DecidedAt,
        item.DecisionComments,
        item.DeletionType);

    private static object TemplateAuditValues(Document template) => new
    {
        template.Id,
        template.TenantId,
        template.Name,
        template.OrganizationId,
        template.IsTemplate,
        template.IsArchived,
        template.IsDeletionApproved,
        template.Status,
        template.Version
    };

    private static string ComputeTemplateMutationSnapshotHash(Document template, IReadOnlyCollection<DocumentVersion> versions)
    {
        var snapshot = new
        {
            template.Id,
            template.TenantId,
            template.OrganizationId,
            template.Name,
            template.Description,
            template.FileName,
            template.FileType,
            template.FileSize,
            template.ContentType,
            template.Tags,
            template.CategoryId,
            template.IsTemplate,
            template.IsMandatory,
            template.IsArchived,
            template.IsDeletionApproved,
            template.IsDeleted,
            template.Status,
            template.Version,
            template.UpdatedAt,
            template.StoragePath,
            Versions = versions.OrderBy(item => item.VersionNumber).ThenBy(item => item.Id).Select(item => new
            {
                item.Id,
                item.VersionNumber,
                item.IsCurrent,
                item.FileName,
                item.FileSize,
                item.ContentType,
                item.ChangeNotes,
                item.UploadedBy,
                item.UploadedAt,
                item.StorageProviderId,
                item.StorageKey,
                item.StoragePath,
                item.Sha256,
                item.IsDeleted
            }).ToArray()
        };
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot)));
        return Convert.ToHexString(bytes);
    }

    private static byte[] NewConcurrencyToken() => Guid.NewGuid().ToByteArray();

    private static Error LocalizedError(string code, string message, int statusCode, string? fieldName = null) =>
        new(code, message, statusCode, fieldName);

    private static bool IsSqlServerDeadlock(Exception exception) => GetSqlServerErrorNumber(exception) == 1205;

    private static bool IsSqlServerForeignKeyConflict(Exception exception) => GetSqlServerErrorNumber(exception) == 547;

    private static int? GetSqlServerErrorNumber(Exception exception)
    {
        var baseException = exception.GetBaseException();
        var type = baseException.GetType();
        if (!string.Equals(type.FullName, "Microsoft.Data.SqlClient.SqlException", StringComparison.Ordinal))
            return null;
        return int.TryParse(type.GetProperty("Number")?.GetValue(baseException)?.ToString(), out var number)
            ? number
            : null;
    }

    private async Task<string?> TryGetCurrentStatusAsync(
        Guid tenantId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await db.Set<TemplateDeletionRequest>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.Id == requestId)
                .Select(item => item.Status)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception,
                "Could not reload the status of template deletion request {RequestId} after a persistence conflict.", requestId);
            return null;
        }
    }

    private static async Task RollbackQuietlyAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try { await transaction.RollbackAsync(cancellationToken); }
        catch { }
    }

    private sealed record TemplateDeletionStorageObject(string ProviderId, string? StorageKey);

    private sealed class TemplateDeletionAuditException(Error error) : Exception(error.Message)
    {
        public Error Error { get; } = error;
    }
}
