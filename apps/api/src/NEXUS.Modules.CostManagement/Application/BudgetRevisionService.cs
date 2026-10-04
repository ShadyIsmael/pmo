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

namespace NEXUS.Modules.CostManagement.Application;

public sealed class BudgetRevisionService : IBudgetRevisionService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IProjectReferenceLookup _projectLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IProjectWorkflowConfigurationService _projectWorkflowConfiguration;
    private readonly IWorkflowInstanceService _workflowInstanceService;

    public BudgetRevisionService(
        DbContext context,
        IAuditService auditService,
        IProjectReferenceLookup projectLookup,
        IHttpContextAccessor httpContextAccessor,
        IProjectWorkflowConfigurationService projectWorkflowConfiguration,
        IWorkflowInstanceService workflowInstanceService)
    {
        _context = context;
        _auditService = auditService;
        _projectLookup = projectLookup;
        _httpContextAccessor = httpContextAccessor;
        _projectWorkflowConfiguration = projectWorkflowConfiguration;
        _workflowInstanceService = workflowInstanceService;
    }

    public async Task<Result<BudgetRevisionDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var revision = await _context.Set<BudgetRevision>()
            .AsNoTracking()
            .Include(r => r.Budget)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (revision is null)
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

        var query = _context.Set<BudgetRevision>()
            .AsNoTracking()
            .Where(r => r.BudgetId == budgetId)
            .OrderByDescending(r => r.CreatedAt);

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

        var query = _context.Set<BudgetRevision>()
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId);

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
        var budget = await _context.Set<Budget>()
            .FirstOrDefaultAsync(b => b.Id == dto.BudgetId && !b.IsDeleted, cancellationToken);

        if (budget is null)
            return Result<BudgetRevisionDto>.Failure(Error.NotFound("Budget not found"));

        if (dto.ProposedApprovedAmount < 0)
            return Result<BudgetRevisionDto>.Failure(Error.Validation("Proposed amount cannot be negative"));

        if (string.IsNullOrWhiteSpace(dto.Reason))
            return Result<BudgetRevisionDto>.Failure(Error.Validation("Reason is required"));

        var hasPending = await _context.Set<BudgetRevision>()
            .AnyAsync(r => r.BudgetId == dto.BudgetId
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
        await _context.SaveChangesAsync(cancellationToken);
        if (workflowSelection.Value is { IsCustom: true } selected)
        {
            var instance = await _workflowInstanceService.ProvisionAsync(FinanceWorkflow.EntityType, revision.Id, selected.WorkflowDefinitionId!.Value, selected.WorkflowVersionId!.Value, BuildWorkflowContext(userId), cancellationToken);
            if (instance.IsFailure) return Result<BudgetRevisionDto>.Failure(instance.Error!);
        }
        await AuditAsync("BudgetRevision", revision.Id, "Create", userId, null, dto, cancellationToken);

        var requesterName = await ResolveNameAsync(revision.RequestedBy, cancellationToken);
        return Result<BudgetRevisionDto>.Success(ToDto(revision, requesterName, null));
    }

    public async Task<Result<BudgetRevisionDto>> UpdateAsync(
        Guid id, UpdateBudgetRevisionDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var revision = await _context.Set<BudgetRevision>()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (revision is null)
            return Result<BudgetRevisionDto>.Failure(Error.NotFound("Budget revision not found"));

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

        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("BudgetRevision", revision.Id, "Update", userId, original, dto, cancellationToken);

        var requesterName = await ResolveNameAsync(revision.RequestedBy, cancellationToken);
        return Result<BudgetRevisionDto>.Success(ToDto(revision, requesterName, null));
    }

    public async Task<Result> SubmitAsync(
        Guid id, SubmitBudgetRevisionDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var revision = await _context.Set<BudgetRevision>()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (revision is null)
            return Result.Failure(Error.NotFound("Budget revision not found"));

        var workflowGuard = await EnsureStandardFinanceMutationAsync(revision.ProjectId, userId, cancellationToken);
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

        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("BudgetRevision", revision.Id, "Submit", userId, original, revision.Status.ToString(), cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ApproveAsync(
        Guid id, ReviewBudgetRevisionDto dto, string userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var revision = await _context.Set<BudgetRevision>()
            .Include(r => r.Budget)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (revision is null)
            return Result.Failure(Error.NotFound("Budget revision not found"));

        var workflowGuard = await EnsureStandardFinanceMutationAsync(revision.ProjectId, userId, cancellationToken);
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

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await AuditAsync("BudgetRevision", revision.Id, "Approve", userId, original,
            new { revision.ProposedApprovedAmount, revision.ReviewComment }, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(
        Guid id, ReviewBudgetRevisionDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var revision = await _context.Set<BudgetRevision>()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (revision is null)
            return Result.Failure(Error.NotFound("Budget revision not found"));

        var workflowGuard = await EnsureStandardFinanceMutationAsync(revision.ProjectId, userId, cancellationToken);
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

        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("BudgetRevision", revision.Id, "Reject", userId, original, dto.ReviewComment, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CancelAsync(
        Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var revision = await _context.Set<BudgetRevision>()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (revision is null)
            return Result.Failure(Error.NotFound("Budget revision not found"));

        var workflowGuard = await EnsureStandardFinanceMutationAsync(revision.ProjectId, userId, cancellationToken);
        if (workflowGuard is not null) return workflowGuard;

        if (revision.Status != BudgetRevision.Statuses.Draft && revision.Status != BudgetRevision.Statuses.Submitted)
            return Result.Failure(Error.Validation("Only draft or submitted revisions can be cancelled"));

        var original = revision.Status.ToString();
        revision.Status = BudgetRevision.Statuses.Cancelled;
        revision.UpdatedBy = userId;
        revision.UpdatedAt = DateTime.UtcNow;
        revision.RowVersion = NewVersion();

        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("BudgetRevision", revision.Id, "Cancel", userId, original, revision.Status.ToString(), cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(
        Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var revision = await _context.Set<BudgetRevision>()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (revision is null)
            return Result.Failure(Error.NotFound("Budget revision not found"));

        if (revision.Status != BudgetRevision.Statuses.Draft)
            return Result.Failure(Error.Validation("Only draft revisions can be deleted"));

        if (!MatchesVersion(revision.RowVersion, rowVersion))
            return Result.Failure(Error.Conflict("Revision was changed by another user"));

        revision.IsDeleted = true;
        revision.UpdatedBy = userId;
        revision.UpdatedAt = DateTime.UtcNow;
        revision.RowVersion = NewVersion();

        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("BudgetRevision", revision.Id, "Delete", userId, null, null, cancellationToken);
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
        Currency = r.Budget?.Currency ?? "GBP",
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

    private Guid CurrentTenant() =>
        Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value, out var tenant)
            ? tenant : TenantConstants.DemoTenantId;

    private WorkflowExecutionContext BuildWorkflowContext(string userId) => new(CurrentTenant(), userId, _httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value, _httpContextAccessor.HttpContext?.User.FindFirst("readOnly")?.Value == "true");

    private async Task<Result?> EnsureStandardFinanceMutationAsync(Guid projectId, string userId, CancellationToken cancellationToken)
    {
        var selection = await _projectWorkflowConfiguration.ResolveForNewRecordAsync(projectId, WorkflowDomains.Finance, FinanceWorkflow.EntityType, BuildWorkflowContext(userId), cancellationToken);
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
        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = type,
            EntityId = id,
            Action = action,
            UserId = Guid.TryParse(userId, out var parsed) ? parsed : null,
            OriginalValues = original,
            NewValues = values is null ? null : JsonSerializer.Serialize(values),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);
    }
}
