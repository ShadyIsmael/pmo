using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.CostManagement.Application;

public sealed class BudgetRevisionService : IBudgetRevisionService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IProjectReferenceLookup _projectLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IProjectWorkflowConfigurationService _projectWorkflowConfiguration;
    private readonly IWorkflowInstanceService _workflowInstanceService;
    private readonly IManagementContextAccessor? _managementContext;

    public BudgetRevisionService(
        DbContext context,
        IAuditService auditService,
        IProjectReferenceLookup projectLookup,
        IHttpContextAccessor httpContextAccessor,
        IProjectWorkflowConfigurationService projectWorkflowConfiguration,
        IWorkflowInstanceService workflowInstanceService,
        IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _auditService = auditService;
        _projectLookup = projectLookup;
        _httpContextAccessor = httpContextAccessor;
        _projectWorkflowConfiguration = projectWorkflowConfiguration;
        _workflowInstanceService = workflowInstanceService;
        _managementContext = managementContext;
    }

    public async Task<Result<BudgetRevisionDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "BudgetRevision.View");
        if (accessError is not null) return Result<BudgetRevisionDto>.Failure(accessError);
        var revision = await ApplyManagementScope(_context.Set<BudgetRevision>().Where(item => item.TenantId == CurrentTenant()))
            .AsNoTracking()
            .Include(r => r.Budget)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (revision is null)
            return Result<BudgetRevisionDto>.Failure(Error.NotFound("Budget revision not found"));
        if (!await HasRevisionManagementScopeAsync(revision, cancellationToken))
            return Result<BudgetRevisionDto>.Failure(Error.NotFound("Budget revision not found"));

        var requesterName = await ResolveNameAsync(revision.RequestedBy, cancellationToken);
        var reviewerName = revision.ReviewedBy.HasValue
            ? await ResolveNameAsync(revision.ReviewedBy.Value, cancellationToken)
            : null;

        return Result<BudgetRevisionDto>.Success(ToDto(revision, requesterName, reviewerName));
    }

    public async Task<PagedResult<BudgetRevisionDto>> GetByBudgetIdAsync(
        Guid budgetId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        if (FinancePermissionGuard.Require(_httpContextAccessor, "BudgetRevision.View") is not null)
            return new PagedResult<BudgetRevisionDto>(Array.Empty<BudgetRevisionDto>(), page, pageSize, 0);

        var query = ApplyManagementScope(_context.Set<BudgetRevision>()
            .Where(item => item.TenantId == CurrentTenant())
            .Include(r => r.Budget)
            .AsNoTracking()
            .Where(r => r.BudgetId == budgetId)
            .OrderByDescending(r => r.CreatedAt));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var requesterIds = items.Select(r => r.RequestedBy).Distinct().ToList();
        var reviewerIds = items.Where(r => r.ReviewedBy.HasValue).Select(r => r.ReviewedBy!.Value).Distinct().ToList();
        var allUserIds = requesterIds.Concat(reviewerIds).Distinct().ToList();
        var names = allUserIds.Count > 0
            ? await ResolveNamesAsync(allUserIds, cancellationToken)
            : new Dictionary<Guid, string>();

        var dtos = items.Select(r =>
        {
            names.TryGetValue(r.RequestedBy, out var requesterName);
            string? reviewerName = null;
            if (r.ReviewedBy.HasValue)
                names.TryGetValue(r.ReviewedBy.Value, out reviewerName);
            return ToDto(r, requesterName, reviewerName);
        }).ToList();

        return new PagedResult<BudgetRevisionDto>(dtos, page, pageSize, total);
    }

    public async Task<PagedResult<BudgetRevisionDto>> GetByProjectIdAsync(
        Guid projectId, int page, int pageSize, string? status = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        if (FinancePermissionGuard.Require(_httpContextAccessor, "BudgetRevision.View") is not null)
            return new PagedResult<BudgetRevisionDto>(Array.Empty<BudgetRevisionDto>(), page, pageSize, 0);

        var query = ApplyManagementScope(_context.Set<BudgetRevision>()
            .Where(item => item.TenantId == CurrentTenant())
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BudgetRevision.Statuses>(status, true, out var parsed))
            query = query.Where(r => r.Status == parsed);

        query = query.OrderByDescending(r => r.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var requesterIds = items.Select(r => r.RequestedBy).Distinct().ToList();
        var reviewerIds = items.Where(r => r.ReviewedBy.HasValue).Select(r => r.ReviewedBy!.Value).Distinct().ToList();
        var allUserIds = requesterIds.Concat(reviewerIds).Distinct().ToList();
        var names = allUserIds.Count > 0
            ? await ResolveNamesAsync(allUserIds, cancellationToken)
            : new Dictionary<Guid, string>();

        var dtos = items.Select(r =>
        {
            names.TryGetValue(r.RequestedBy, out var requesterName);
            string? reviewerName = null;
            if (r.ReviewedBy.HasValue)
                names.TryGetValue(r.ReviewedBy.Value, out reviewerName);
            return ToDto(r, requesterName, reviewerName);
        }).ToList();

        return new PagedResult<BudgetRevisionDto>(dtos, page, pageSize, total);
    }

    public async Task<Result<BudgetRevisionDto>> CreateAsync(
        CreateBudgetRevisionDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "BudgetRevision.Create");
        if (accessError is not null) return Result<BudgetRevisionDto>.Failure(accessError);
        var writeContextError = FinancePermissionGuard.RequireWritableActorContext(
            _httpContextAccessor, _managementContext?.Current, userId, CurrentTenant());
        if (writeContextError is not null) return Result<BudgetRevisionDto>.Failure(writeContextError);
        var budget = await _context.Set<Budget>()
            .FirstOrDefaultAsync(b => b.Id == dto.BudgetId && b.TenantId == CurrentTenant() && !b.IsDeleted, cancellationToken);

        if (budget is null)
            return Result<BudgetRevisionDto>.Failure(Error.NotFound("Budget not found"));
        if (!await HasBudgetManagementScopeAsync(budget, cancellationToken))
            return Result<BudgetRevisionDto>.Failure(Error.NotFound("Budget not found"));
        if (!ProjectBudgetOwnershipGuard.IsProjectOwned(budget.ProjectId))
            return Result<BudgetRevisionDto>.Failure(Error.Forbidden("Portfolio rollup budgets are read-only."));

        if (dto.ProposedApprovedAmount < 0)
            return Result<BudgetRevisionDto>.Failure(Error.Validation("Proposed amount cannot be negative"));

        if (string.IsNullOrWhiteSpace(dto.Reason))
            return Result<BudgetRevisionDto>.Failure(Error.Validation("Reason is required"));

        var hasPending = await _context.Set<BudgetRevision>()
            .AnyAsync(r => r.TenantId == CurrentTenant() && r.BudgetId == dto.BudgetId && !r.IsDeleted
                && (r.Status == BudgetRevision.Statuses.Draft || r.Status == BudgetRevision.Statuses.Submitted),
                cancellationToken);

        if (hasPending)
            return Result<BudgetRevisionDto>.Failure(Error.Validation("A pending revision already exists for this budget"));

        var project = budget.ProjectId.HasValue
            ? await _projectLookup.FindAsync(budget.ProjectId.Value, cancellationToken)
            : null;
        var workflowSelection = await _projectWorkflowConfiguration.ResolveForNewRecordAsync(budget.ProjectId, WorkflowDomains.Finance, FinanceWorkflow.EntityType, BuildWorkflowContext(userId), cancellationToken);
        if (workflowSelection.IsFailure) return Result<BudgetRevisionDto>.Failure(workflowSelection.Error!);
        var initialStatus = BudgetRevision.Statuses.Draft;
        if (workflowSelection.Value is { IsCustom: true } custom && !Enum.TryParse(custom.InitialStateCode, true, out initialStatus))
            return Result<BudgetRevisionDto>.Failure(Error.Validation("The selected Finance workflow has an unsupported initial state."));

        var revision = new BudgetRevision
        {
            TenantId = CurrentTenant(),
            CreatedBy = userId,
            ProjectId = budget.ProjectId ?? Guid.Empty,
            BudgetId = dto.BudgetId,
            PreviousApprovedAmount = budget.ApprovedAmount,
            ProposedApprovedAmount = dto.ProposedApprovedAmount,
            Reason = dto.Reason.Trim(),
            Justification = dto.Justification?.Trim(),
            Status = initialStatus,
            RequestedBy = ParseUserId(userId),
            RequestedAt = DateTime.UtcNow,
            RowVersion = NewVersion()
        };

        _context.Set<BudgetRevision>().Add(revision);
        await AuditAsync("BudgetRevision", revision.Id, "Create", userId, null, dto, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        if (workflowSelection.Value is { IsCustom: true } selected)
        {
            var instance = await _workflowInstanceService.ProvisionAsync(FinanceWorkflow.EntityType, revision.Id, selected.WorkflowDefinitionId!.Value, selected.WorkflowVersionId!.Value, BuildWorkflowContext(userId), cancellationToken);
            if (instance.IsFailure) return Result<BudgetRevisionDto>.Failure(instance.Error!);
        }
        var requesterName = await ResolveNameAsync(revision.RequestedBy, cancellationToken);
        return Result<BudgetRevisionDto>.Success(ToDto(revision, requesterName, null));
    }

    public async Task<Result<BudgetRevisionDto>> UpdateAsync(
        Guid id, UpdateBudgetRevisionDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "BudgetRevision.EditDraft");
        if (accessError is not null) return Result<BudgetRevisionDto>.Failure(accessError);
        var writeContextError = FinancePermissionGuard.RequireWritableActorContext(
            _httpContextAccessor, _managementContext?.Current, userId, CurrentTenant());
        if (writeContextError is not null) return Result<BudgetRevisionDto>.Failure(writeContextError);
        var revision = await ApplyManagementScope(_context.Set<BudgetRevision>().Where(item => item.TenantId == CurrentTenant()))
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);

        if (revision is null)
            return Result<BudgetRevisionDto>.Failure(Error.NotFound("Budget revision not found"));
        if (!await HasRevisionManagementScopeAsync(revision, cancellationToken))
            return Result<BudgetRevisionDto>.Failure(Error.NotFound("Budget revision not found"));
        if (!await HasProjectOwnedBudgetAsync(revision, cancellationToken))
            return Result<BudgetRevisionDto>.Failure(Error.Forbidden("Portfolio rollup budget revisions are read-only."));

        if (revision.Status != BudgetRevision.Statuses.Draft)
            return Result<BudgetRevisionDto>.Failure(Error.Validation("Only draft revisions can be edited"));

        if (!MatchesVersion(revision.RowVersion, dto.RowVersion))
            return Result<BudgetRevisionDto>.Failure(Error.Conflict("Revision was changed by another user"));

        if (dto.ProposedApprovedAmount < 0)
            return Result<BudgetRevisionDto>.Failure(Error.Validation("Proposed amount cannot be negative"));

        if (string.IsNullOrWhiteSpace(dto.Reason))
            return Result<BudgetRevisionDto>.Failure(Error.Validation("Reason is required"));

        var original = JsonSerializer.Serialize(new { revision.PreviousApprovedAmount, revision.ProposedApprovedAmount, revision.Reason });

        revision.ProposedApprovedAmount = dto.ProposedApprovedAmount;
        revision.Reason = dto.Reason.Trim();
        revision.Justification = dto.Justification?.Trim();
        revision.UpdatedBy = userId;
        revision.UpdatedAt = DateTime.UtcNow;
        revision.RowVersion = NewVersion();

        await AuditAsync("BudgetRevision", revision.Id, "Update", userId, original, dto, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var requesterName = await ResolveNameAsync(revision.RequestedBy, cancellationToken);
        return Result<BudgetRevisionDto>.Success(ToDto(revision, requesterName, null));
    }

    public async Task<Result> SubmitAsync(
        Guid id, SubmitBudgetRevisionDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "BudgetRevision.SubmitForReview");
        if (accessError is not null) return Result.Failure(accessError);
        var writeContextError = FinancePermissionGuard.RequireWritableActorContext(
            _httpContextAccessor, _managementContext?.Current, userId, CurrentTenant());
        if (writeContextError is not null) return Result.Failure(writeContextError);
        var revision = await ApplyManagementScope(_context.Set<BudgetRevision>().Where(item => item.TenantId == CurrentTenant()))
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);

        if (revision is null)
            return Result.Failure(Error.NotFound("Budget revision not found"));
        if (!await HasProjectOwnedBudgetAsync(revision, cancellationToken))
            return Result.Failure(Error.Forbidden("Portfolio rollup budget revisions are read-only."));

        var workflowGuard = await EnsureStandardFinanceMutationAsync(revision, userId, cancellationToken);
        if (workflowGuard is not null) return workflowGuard;

        if (revision.Status != BudgetRevision.Statuses.Draft)
            return Result.Failure(Error.Validation("Only draft revisions can be submitted"));

        if (!MatchesVersion(revision.RowVersion, dto.RowVersion))
            return Result.Failure(Error.Conflict("Revision was changed by another user"));

        var original = revision.Status.ToString();
        revision.Status = BudgetRevision.Statuses.Submitted;
        revision.UpdatedBy = userId;
        revision.UpdatedAt = DateTime.UtcNow;
        revision.RowVersion = NewVersion();

        await AuditAsync("BudgetRevision", revision.Id, "Submit", userId, original, revision.Status.ToString(), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ApproveAsync(
        Guid id, ReviewBudgetRevisionDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "BudgetRevision.Approve");
        if (accessError is not null) return Result.Failure(accessError);
        var writeContextError = FinancePermissionGuard.RequireWritableActorContext(
            _httpContextAccessor, _managementContext?.Current, userId, CurrentTenant());
        if (writeContextError is not null) return Result.Failure(writeContextError);
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var revision = await ApplyManagementScope(_context.Set<BudgetRevision>().Where(item => item.TenantId == CurrentTenant()))
            .Include(r => r.Budget)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);

        if (revision is null)
            return Result.Failure(Error.NotFound("Budget revision not found"));

        if (!await HasRevisionManagementScopeAsync(revision, cancellationToken))
            return Result.Failure(Error.NotFound("Budget revision not found"));
        if (revision.Budget is null || !ProjectBudgetOwnershipGuard.IsProjectOwned(revision.Budget.ProjectId)
            || revision.ProjectId != revision.Budget.ProjectId.Value)
            return Result.Failure(Error.Forbidden("Portfolio rollup budget revisions are read-only."));
        var workflowGuard = await EnsureStandardFinanceMutationAsync(revision, userId, cancellationToken);
        if (workflowGuard is not null) return workflowGuard;

        if (revision.Status != BudgetRevision.Statuses.Submitted)
            return Result.Failure(Error.Validation("Only submitted revisions can be approved"));

        if (!MatchesVersion(revision.RowVersion, dto.RowVersion))
            return Result.Failure(Error.Conflict("Revision was changed by another user"));

        if (revision.Budget is null)
            return Result.Failure(Error.NotFound("Associated budget not found"));

        // Maker/checker separation: approver must differ from requester.
        var approverId = ParseUserId(userId);
        if (approverId != Guid.Empty && approverId == revision.RequestedBy)
            return Result.Failure(Error.Validation("The requester cannot approve their own budget revision"));

        var allocationError = await BudgetAuthority.ValidateAllocationAsync(_context, _projectLookup, revision.Budget.ProjectId, revision.BudgetId, revision.ProposedApprovedAmount, cancellationToken);
        if (allocationError is not null) return Result.Failure(allocationError);
        if (revision.PreviousApprovedAmount != revision.Budget.ApprovedAmount)
            return Result.Failure(Error.Conflict("The budget allocation has changed. Cancel and resubmit the revision."));
        var originalAmount = revision.Budget.ApprovedAmount;
        var original = JsonSerializer.Serialize(new { BudgetId = revision.BudgetId, ApprovedAmount = originalAmount });

        revision.Status = BudgetRevision.Statuses.Approved;
        revision.ReviewedBy = ParseUserId(userId);
        revision.ReviewedAt = DateTime.UtcNow;
        revision.ReviewComment = dto.ReviewComment?.Trim();
        revision.UpdatedBy = userId;
        revision.UpdatedAt = DateTime.UtcNow;
        revision.RowVersion = NewVersion();

        revision.Budget.ApprovedAmount = revision.ProposedApprovedAmount;
        revision.Budget.UpdatedBy = userId;
        revision.Budget.UpdatedAt = DateTime.UtcNow;
        revision.Budget.RowVersion = NewVersion();

        await AuditAsync("BudgetRevision", revision.Id, "Approve", userId, original,
            new { revision.ProposedApprovedAmount, revision.ReviewComment }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(
        Guid id, ReviewBudgetRevisionDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "BudgetRevision.Approve");
        if (accessError is not null) return Result.Failure(accessError);
        var writeContextError = FinancePermissionGuard.RequireWritableActorContext(
            _httpContextAccessor, _managementContext?.Current, userId, CurrentTenant());
        if (writeContextError is not null) return Result.Failure(writeContextError);
        var revision = await ApplyManagementScope(_context.Set<BudgetRevision>().Where(item => item.TenantId == CurrentTenant()))
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);

        if (revision is null)
            return Result.Failure(Error.NotFound("Budget revision not found"));
        if (!await HasProjectOwnedBudgetAsync(revision, cancellationToken))
            return Result.Failure(Error.Forbidden("Portfolio rollup budget revisions are read-only."));

        var workflowGuard = await EnsureStandardFinanceMutationAsync(revision, userId, cancellationToken);
        if (workflowGuard is not null) return workflowGuard;

        if (revision.Status != BudgetRevision.Statuses.Submitted)
            return Result.Failure(Error.Validation("Only submitted revisions can be rejected"));

        if (!MatchesVersion(revision.RowVersion, dto.RowVersion))
            return Result.Failure(Error.Conflict("Revision was changed by another user"));

        // Maker/checker separation: rejector must differ from requester.
        var rejectorId = ParseUserId(userId);
        if (rejectorId != Guid.Empty && rejectorId == revision.RequestedBy)
            return Result.Failure(Error.Validation("The requester cannot reject their own budget revision"));

        if (string.IsNullOrWhiteSpace(dto.ReviewComment))
            return Result.Failure(Error.Validation("Review comment is required for rejection"));

        var original = revision.Status.ToString();
        revision.Status = BudgetRevision.Statuses.Rejected;
        revision.ReviewedBy = ParseUserId(userId);
        revision.ReviewedAt = DateTime.UtcNow;
        revision.ReviewComment = dto.ReviewComment.Trim();
        revision.UpdatedBy = userId;
        revision.UpdatedAt = DateTime.UtcNow;
        revision.RowVersion = NewVersion();

        await AuditAsync("BudgetRevision", revision.Id, "Reject", userId, original, dto.ReviewComment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CancelAsync(
        Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "BudgetRevision.Cancel");
        if (accessError is not null) return Result.Failure(accessError);
        var writeContextError = FinancePermissionGuard.RequireWritableActorContext(
            _httpContextAccessor, _managementContext?.Current, userId, CurrentTenant());
        if (writeContextError is not null) return Result.Failure(writeContextError);
        var revision = await ApplyManagementScope(_context.Set<BudgetRevision>().Where(item => item.TenantId == CurrentTenant()))
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);

        if (revision is null)
            return Result.Failure(Error.NotFound("Budget revision not found"));
        if (!await HasProjectOwnedBudgetAsync(revision, cancellationToken))
            return Result.Failure(Error.Forbidden("Portfolio rollup budget revisions are read-only."));

        var workflowGuard = await EnsureStandardFinanceMutationAsync(revision, userId, cancellationToken);
        if (workflowGuard is not null) return workflowGuard;

        if (revision.Status != BudgetRevision.Statuses.Draft && revision.Status != BudgetRevision.Statuses.Submitted)
            return Result.Failure(Error.Validation("Only draft or submitted revisions can be cancelled"));

        var original = revision.Status.ToString();
        revision.Status = BudgetRevision.Statuses.Cancelled;
        revision.UpdatedBy = userId;
        revision.UpdatedAt = DateTime.UtcNow;
        revision.RowVersion = NewVersion();

        await AuditAsync("BudgetRevision", revision.Id, "Cancel", userId, original, revision.Status.ToString(), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(
        Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "BudgetRevision.Delete");
        if (accessError is not null) return Result.Failure(accessError);
        var writeContextError = FinancePermissionGuard.RequireWritableActorContext(
            _httpContextAccessor, _managementContext?.Current, userId, CurrentTenant());
        if (writeContextError is not null) return Result.Failure(writeContextError);
        var revision = await ApplyManagementScope(_context.Set<BudgetRevision>().Where(item => item.TenantId == CurrentTenant()))
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);

        if (revision is null)
            return Result.Failure(Error.NotFound("Budget revision not found"));
        if (!await HasRevisionManagementScopeAsync(revision, cancellationToken))
            return Result.Failure(Error.NotFound("Budget revision not found"));
        if (!await HasProjectOwnedBudgetAsync(revision, cancellationToken))
            return Result.Failure(Error.Forbidden("Portfolio rollup budget revisions are read-only."));

        if (revision.Status != BudgetRevision.Statuses.Draft)
            return Result.Failure(Error.Validation("Only draft revisions can be deleted"));

        if (!MatchesVersion(revision.RowVersion, rowVersion))
            return Result.Failure(Error.Conflict("Revision was changed by another user"));

        var original = JsonSerializer.Serialize(new { revision.ProposedApprovedAmount, revision.Reason, Status = revision.Status.ToString() });
        await AuditAsync("BudgetRevision", revision.Id, "Delete", userId, original, new { isDeleted = true }, cancellationToken);
        revision.IsDeleted = true;
        revision.UpdatedBy = userId;
        revision.UpdatedAt = DateTime.UtcNow;
        revision.RowVersion = NewVersion();

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<string?> ResolveNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var names = await ResolveNamesAsync([userId], cancellationToken);
        return names.TryGetValue(userId, out var name) ? name : null;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveNamesAsync(
        IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var distinctIds = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinctIds.Count == 0)
            return new Dictionary<Guid, string>();

        try
        {
            var userSet = _context.Set<global::NEXUS.Modules.Identity.Domain.User>();
            return await userSet
                .AsNoTracking()
                .Where(u => distinctIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName, cancellationToken);
        }
        catch
        {
            return new Dictionary<Guid, string>();
        }
    }

    private static BudgetRevisionDto ToDto(BudgetRevision r, string? requesterName, string? reviewerName) => new()
    {
        Id = r.Id,
        ProjectId = r.ProjectId,
        BudgetId = r.BudgetId,
        BudgetName = r.Budget?.Name ?? string.Empty,
        Currency = r.Budget?.Currency ?? string.Empty,
        PreviousApprovedAmount = r.PreviousApprovedAmount,
        ProposedApprovedAmount = r.ProposedApprovedAmount,
        Reason = r.Reason,
        Justification = r.Justification,
        Status = r.Status.ToString(),
        RequestedBy = r.RequestedBy,
        RequestedByName = requesterName,
        RequestedAt = r.RequestedAt,
        ReviewedBy = r.ReviewedBy,
        ReviewedByName = reviewerName,
        ReviewedAt = r.ReviewedAt,
        ReviewComment = r.ReviewComment,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
        RowVersion = r.RowVersion
    };

    private Guid CurrentTenant() => FinancePermissionGuard.TryGetTenantId(_httpContextAccessor.HttpContext?.User, out var tenant)
        ? tenant : Guid.Empty;

    private IQueryable<BudgetRevision> ApplyManagementScope(IQueryable<BudgetRevision> query)
    {
        var management = _managementContext?.Current;
        if (management is null || management.TenantId != CurrentTenant() || management.Scope is null)
            return query.Where(_ => false);
        var scope = management.Scope;

        var projectIds = scope.ProjectIds.ToArray();
        var portfolioIds = scope.PortfolioIds.ToArray();
        return query.Where(revision => projectIds.Contains(revision.ProjectId)
            || (revision.ProjectId == Guid.Empty && revision.Budget != null
                && revision.Budget.PortfolioId.HasValue && portfolioIds.Contains(revision.Budget.PortfolioId.Value)));
    }

    private Task<bool> HasBudgetManagementScopeAsync(Budget budget, CancellationToken cancellationToken)
    {
        var management = _managementContext?.Current;
        if (management is null || management.TenantId != CurrentTenant() || management.Scope is null
            || budget.TenantId != CurrentTenant()) return Task.FromResult(false);
        var scope = management.Scope;
        if (budget.ProjectId.HasValue) return Task.FromResult(scope.ProjectIds.Contains(budget.ProjectId.Value));
        return Task.FromResult(budget.PortfolioId.HasValue && scope.PortfolioIds.Contains(budget.PortfolioId.Value));
    }

    private async Task<bool> HasProjectOwnedBudgetAsync(BudgetRevision revision, CancellationToken cancellationToken)
    {
        var budgetProjectId = await _context.Set<Budget>().AsNoTracking()
            .Where(budget => budget.Id == revision.BudgetId && budget.TenantId == CurrentTenant() && !budget.IsDeleted)
            .Select(budget => budget.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);
        return ProjectBudgetOwnershipGuard.IsProjectOwned(budgetProjectId)
            && revision.ProjectId == budgetProjectId;
    }

    private async Task<bool> HasRevisionManagementScopeAsync(BudgetRevision revision, CancellationToken cancellationToken)
    {
        var management = _managementContext?.Current;
        if (management is null || management.TenantId != CurrentTenant() || management.Scope is null
            || revision.TenantId != CurrentTenant()) return false;
        var scope = management.Scope;
        if (revision.ProjectId != Guid.Empty) return scope.ProjectIds.Contains(revision.ProjectId);

        var portfolioId = await _context.Set<Budget>().AsNoTracking()
            .Where(budget => budget.Id == revision.BudgetId)
            .Select(budget => budget.PortfolioId)
            .FirstOrDefaultAsync(cancellationToken);
        return portfolioId.HasValue && scope.PortfolioIds.Contains(portfolioId.Value);
    }

    private WorkflowExecutionContext BuildWorkflowContext(string userId) => new(CurrentTenant(), userId, _httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value, _httpContextAccessor.HttpContext?.User.FindFirst("readOnly")?.Value == "true");

    private async Task<Result?> EnsureStandardFinanceMutationAsync(BudgetRevision revision, string userId, CancellationToken cancellationToken)
    {
        if (!await HasRevisionManagementScopeAsync(revision, cancellationToken))
            return Result.Failure(Error.NotFound("Budget revision not found"));
        var selection = await _projectWorkflowConfiguration.ResolveForNewRecordAsync(revision.ProjectId, WorkflowDomains.Finance, FinanceWorkflow.EntityType, BuildWorkflowContext(userId), cancellationToken);
        if (selection.IsFailure) return Result.Failure(selection.Error!);
        return selection.Value is { IsCustom: true }
            ? Result.Failure(Error.Validation("A custom Finance workflow is active for this project. Change the Budget Revision through a workflow transition."))
            : null;
    }

    private static Guid ParseUserId(string userId) =>
        Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;

    private static byte[] NewVersion() => Guid.NewGuid().ToByteArray();

    private static bool MatchesVersion(byte[] current, byte[] requested) =>
        requested.Length == 0 || current.Length == 0 || current.SequenceEqual(requested);

    private async Task AuditAsync(string type, Guid id, string action, string userId,
        string? original, object? values, CancellationToken cancellationToken)
    {
        var revision = _context.Set<BudgetRevision>().Local.FirstOrDefault(item => item.Id == id);
        var budget = revision is null ? null : _context.Set<Budget>().Local.FirstOrDefault(item => item.Id == revision.BudgetId);
        var projectReference = revision is null || revision.ProjectId == Guid.Empty
            ? null
            : await _projectLookup.FindAsync(revision.ProjectId, cancellationToken);
        var actorName = _httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value
            ?? _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var result = await _auditService.AppendAsync(FinancialAuditEntryFactory.ForCurrentRecord(
            _context, type, id, action, userId, actorName, original, values,
            referenceOverride: budget?.Name,
            currencyOverride: budget?.Currency ?? projectReference?.Currency), cancellationToken);
        if (result.IsFailure) throw new InvalidOperationException(result.Error?.Message ?? "Financial audit append failed.");
    }
}
