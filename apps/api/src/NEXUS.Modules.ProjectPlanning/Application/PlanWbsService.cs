using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Application.Mapping;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.ProjectPlanning.Application;

public class PlanWbsService : IPlanWbsService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SummaryTaskRollupService _summaryRollup;
    private readonly IPlanGridCommandHistoryService? _commandHistory;

    public PlanWbsService(DbContext context, IAuditService auditService, IHttpContextAccessor httpContextAccessor, SummaryTaskRollupService? summaryRollup = null, IPlanGridCommandHistoryService? commandHistory = null)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _summaryRollup = summaryRollup ?? new SummaryTaskRollupService(context);
        _commandHistory = commandHistory;
    }

    public async Task<Result<PlanWbsNodeDto>> CreateAsync(Guid planId, CreatePlanWbsNodeDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var workingVersion = await GetActiveWorkingVersionAsync(planId, cancellationToken);
        if (workingVersion is null)
        {
            return Result<PlanWbsNodeDto>.Failure(Error.NotFound(await GetActiveWorkingVersionNotAvailableMessageAsync(planId, cancellationToken)));
        }

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<PlanWbsNodeDto>.Failure(Error.Validation("Node name is required"));
        }

        var code = dto.Code?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result<PlanWbsNodeDto>.Failure(Error.Validation("WBS code is required"));
        }

        if (dto.ParentWbsId.HasValue &&
            !await ParentExistsInVersionAsync(dto.ParentWbsId.Value, workingVersion.Id, cancellationToken))
        {
            return Result<PlanWbsNodeDto>.Failure(Error.Validation("Parent WBS node must belong to the same plan version"));
        }

        var codeExists = await _context.Set<PlanWbsNode>()
            .AnyAsync(n => n.PlanVersionId == workingVersion.Id && n.Code == code, cancellationToken);
        if (codeExists)
        {
            return Result<PlanWbsNodeDto>.Failure(Error.Conflict($"A WBS code '{code}' already exists in this plan version"));
        }

        var tenantId = GetCurrentTenantId();
        var sortOrder = dto.SortOrder ?? await NextSortOrderAsync(workingVersion.Id, dto.ParentWbsId, cancellationToken);
        var previousSummaryIds = await _summaryRollup.RecalculateAsync(workingVersion.Id, userId, cancellationToken: cancellationToken);

        var node = dto.ToEntity(tenantId, workingVersion.Id, userId);
        node.SortOrder = sortOrder;
        _context.Set<PlanWbsNode>().Add(node);
        await _summaryRollup.RecalculateAsync(workingVersion.Id, userId, previousSummaryIds, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "PlanWbsNode",
            EntityId = node.Id,
            Action = "Create",
            UserId = TryParseGuid(userId),
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);
        if (_commandHistory is not null)
            await _commandHistory.RecordBarrierAsync(planId, workingVersion.Id, userId, "CreateWbs", cancellationToken);

        return Result<PlanWbsNodeDto>.Success(node.ToDto());
    }

    public async Task<Result<PlanWbsNodeDto>> UpdateAsync(Guid planId, Guid nodeId, UpdatePlanWbsNodeDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var node = await _context.Set<PlanWbsNode>()
            .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);
        if (node is null) return Result<PlanWbsNodeDto>.Failure(Error.NotFound("WBS node not found"));

        var editError = await GetEditErrorAsync(node, planId, cancellationToken);
        if (editError is not null) return Result<PlanWbsNodeDto>.Failure(editError);

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<PlanWbsNodeDto>.Failure(Error.Validation("Node name is required"));
        }

        var code = dto.Code?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result<PlanWbsNodeDto>.Failure(Error.Validation("WBS code is required"));
        }

        if (dto.ParentWbsId.HasValue)
        {
            if (dto.ParentWbsId.Value == node.Id)
            {
                return Result<PlanWbsNodeDto>.Failure(Error.Validation("A node cannot be its own parent"));
            }

            if (!await ParentExistsInVersionAsync(dto.ParentWbsId.Value, node.PlanVersionId, cancellationToken))
            {
                return Result<PlanWbsNodeDto>.Failure(Error.Validation("Parent WBS node must belong to the same plan version"));
            }

            if (await WouldCreateCycleAsync(nodeId, dto.ParentWbsId.Value, cancellationToken))
            {
                return Result<PlanWbsNodeDto>.Failure(Error.Validation("The new parent would create a cycle in the WBS tree"));
            }
        }

        var codeExists = await _context.Set<PlanWbsNode>()
            .AnyAsync(n => n.PlanVersionId == node.PlanVersionId && n.Code == code && n.Id != node.Id, cancellationToken);
        if (codeExists)
        {
            return Result<PlanWbsNodeDto>.Failure(Error.Conflict($"A WBS code '{code}' already exists in this plan version"));
        }

        var originalValues = System.Text.Json.JsonSerializer.Serialize(node.ToDto());
        var historyBefore = _commandHistory is null ? null : PlanGridCommandHistoryService.CaptureWbsFields(node);
        var previousSummaryIds = await _summaryRollup.RecalculateAsync(node.PlanVersionId, userId, cancellationToken: cancellationToken);
        node.Name = name;
        node.Code = code;
        node.ParentWbsId = dto.ParentWbsId;
        node.SortOrder = dto.SortOrder;
        node.TextFormat = dto.TextFormat.ToEntity();
        node.UpdatedBy = userId;

        await _summaryRollup.RecalculateAsync(node.PlanVersionId, userId, previousSummaryIds, cancellationToken);

        _context.Entry(node).Property(n => n.RowVersion).OriginalValue = dto.RowVersion;

        await using var transaction = _commandHistory is not null && _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogAsync(new AuditEntry
            {
                EntityType = "PlanWbsNode",
                EntityId = node.Id,
                Action = "Update",
                UserId = TryParseGuid(userId),
                OriginalValues = originalValues,
                NewValues = System.Text.Json.JsonSerializer.Serialize(node.ToDto()),
                Timestamp = DateTime.UtcNow,
                CreatedBy = userId
            }, cancellationToken);
            if (historyBefore is not null && _commandHistory is not null)
                await _commandHistory.RecordWbsUpdateAsync(planId, node.PlanVersionId, userId, historyBefore, node, cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            return Result<PlanWbsNodeDto>.Failure(Error.Conflict("The node was modified by another user. Reload and try again."));
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PlanWbsNodeDto>.Success(node.ToDto());
    }

    public async Task<Result<IReadOnlyList<PlanWbsNodeDto>>> UpdatePlacementsAsync(
        Guid planId,
        BulkUpdatePlanWbsPlacementsDto dto,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (dto.Items is null || dto.Items.Count is < 1 or > 500 ||
            dto.Items.Any(item => item is null || item.NodeId == Guid.Empty || item.RowVersion is null || item.RowVersion.Length == 0) ||
            dto.Items.Select(item => item.NodeId).Distinct().Count() != dto.Items.Count)
            return Result<IReadOnlyList<PlanWbsNodeDto>>.Failure(Error.Validation("WBS placement updates must contain 1–500 unique nodes with row versions"));

        var version = await GetActiveWorkingVersionAsync(planId, cancellationToken);
        if (version is null)
            return Result<IReadOnlyList<PlanWbsNodeDto>>.Failure(Error.NotFound(await GetActiveWorkingVersionNotAvailableMessageAsync(planId, cancellationToken)));

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        using var historyCollection = _commandHistory?.BeginCollection("BulkWbsMove");
        try
        {
            var nodes = await _context.Set<PlanWbsNode>()
                .Where(node => node.PlanVersionId == version.Id && !node.IsDeleted)
                .ToListAsync(cancellationToken);
            var byId = nodes.ToDictionary(node => node.Id);
            if (dto.Items.Any(item => !byId.ContainsKey(item.NodeId)))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<IReadOnlyList<PlanWbsNodeDto>>.Failure(Error.Validation("Every WBS node must belong to the active Working version"));
            }

            foreach (var item in dto.Items)
            {
                if (item.ParentWbsId.HasValue && !byId.ContainsKey(item.ParentWbsId.Value))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<IReadOnlyList<PlanWbsNodeDto>>.Failure(Error.Validation("Every target parent must belong to the same plan version"));
                }
                if (!byId[item.NodeId].RowVersion.SequenceEqual(item.RowVersion))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<IReadOnlyList<PlanWbsNodeDto>>.Failure(Error.Conflict("The WBS structure changed. Reload and try again."));
                }
            }

            var parentById = nodes.ToDictionary(node => node.Id, node => node.ParentWbsId);
            foreach (var item in dto.Items) parentById[item.NodeId] = item.ParentWbsId;
            foreach (var node in nodes)
            {
                var visited = new HashSet<Guid> { node.Id };
                var parentId = parentById[node.Id];
                while (parentId.HasValue)
                {
                    if (!parentById.ContainsKey(parentId.Value) || !visited.Add(parentId.Value))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<IReadOnlyList<PlanWbsNodeDto>>.Failure(Error.Validation("The WBS placement would create a cycle or reference a node outside this version"));
                    }
                    parentId = parentById[parentId.Value];
                }
            }

            var previousSummaryIds = await _summaryRollup.RecalculateAsync(version.Id, userId, cancellationToken: cancellationToken);
            var beforeById = new Dictionary<Guid, IReadOnlyDictionary<string, System.Text.Json.JsonElement>>();
            foreach (var item in dto.Items)
            {
                var node = byId[item.NodeId];
                beforeById[node.Id] = PlanGridCommandHistoryService.CaptureWbsFields(node);
                node.ParentWbsId = item.ParentWbsId;
                node.SortOrder = item.SortOrder;
                node.UpdatedBy = userId;
                _context.Entry(node).Property(entity => entity.RowVersion).OriginalValue = item.RowVersion;
            }
            await _summaryRollup.RecalculateAsync(version.Id, userId, previousSummaryIds, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var item in dto.Items)
            {
                var node = byId[item.NodeId];
                _context.Set<AuditEntry>().Add(new AuditEntry
                {
                    TenantId = node.TenantId,
                    EntityType = "PlanWbsNode",
                    EntityId = node.Id,
                    Action = "Move",
                    UserId = TryParseGuid(userId),
                    OriginalValues = System.Text.Json.JsonSerializer.Serialize(beforeById[node.Id]),
                    NewValues = System.Text.Json.JsonSerializer.Serialize(PlanGridCommandHistoryService.CaptureWbsFields(node)),
                    Timestamp = DateTime.UtcNow,
                    CreatedBy = userId
                });
                if (_commandHistory is not null)
                    await _commandHistory.RecordWbsUpdateAsync(planId, version.Id, userId, beforeById[node.Id], node, cancellationToken);
            }

            if (_commandHistory is not null)
                await _commandHistory.CommitCollectionAsync(planId, version.Id, userId, cancellationToken);
            else
                await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<IReadOnlyList<PlanWbsNodeDto>>.Success(dto.Items.Select(item => byId[item.NodeId].ToDto()).ToList());
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<IReadOnlyList<PlanWbsNodeDto>>.Failure(Error.Conflict("The WBS structure was modified concurrently. Reload and try again."));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Result> DeleteAsync(Guid planId, Guid nodeId, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var node = await _context.Set<PlanWbsNode>()
            .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);
        if (node is null) return Result.Failure(Error.NotFound("WBS node not found"));

        var editError = await GetEditErrorAsync(node, planId, cancellationToken);
        if (editError is not null) return Result.Failure(editError);

        var hasChildren = await _context.Set<PlanWbsNode>()
            .AnyAsync(n => n.ParentWbsId == node.Id, cancellationToken);
        if (hasChildren)
        {
            return Result.Failure(Error.Validation("Cannot delete a node that has children. Delete the children first."));
        }

        var previousSummaryIds = await _summaryRollup.RecalculateAsync(node.PlanVersionId, userId, cancellationToken: cancellationToken);
        node.IsDeleted = true;
        node.UpdatedBy = userId;

        await _summaryRollup.RecalculateAsync(node.PlanVersionId, userId, previousSummaryIds, cancellationToken);

        _context.Entry(node).Property(n => n.RowVersion).OriginalValue = rowVersion;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Conflict("The node was modified by another user. Reload and try again."));
        }

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "PlanWbsNode",
            EntityId = node.Id,
            Action = "Delete",
            UserId = TryParseGuid(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);
        if (_commandHistory is not null)
            await _commandHistory.RecordBarrierAsync(planId, node.PlanVersionId, userId, "DeleteWbs", cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<PlanWbsNodeDto>>> GetTreeAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var workingVersion = await GetActiveWorkingVersionAsync(planId, cancellationToken);
        if (workingVersion is null)
        {
            return Result<IReadOnlyList<PlanWbsNodeDto>>.Failure(Error.NotFound(await GetActiveWorkingVersionNotAvailableMessageAsync(planId, cancellationToken)));
        }

        var nodes = await _context.Set<PlanWbsNode>()
            .Where(n => n.PlanVersionId == workingVersion.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<PlanWbsNodeDto>>.Success(nodes.ToTree());
    }

    private async Task<PlanVersion?> GetActiveWorkingVersionAsync(Guid planId, CancellationToken cancellationToken)
    {
        return await _context.Set<PlanVersion>()
            .FirstOrDefaultAsync(v => v.PlanId == planId && v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active, cancellationToken);
    }

    private async Task<string> GetActiveWorkingVersionNotAvailableMessageAsync(Guid planId, CancellationToken cancellationToken)
    {
        var planExists = await _context.Set<Plan>().AnyAsync(p => p.Id == planId, cancellationToken);
        return planExists ? "No active working version exists for this plan" : "Plan not found";
    }

    /// <summary>
    /// Returns a failure <see cref="Error"/> when a node cannot be edited: its plan
    /// is missing, it has no active Working version, or the node lives in a version
    /// other than that Working version (e.g. an immutable Baseline).
    /// </summary>
    private async Task<Error?> GetEditErrorAsync(PlanWbsNode node, Guid planId, CancellationToken cancellationToken)
    {
        var planExists = await _context.Set<Plan>().AnyAsync(p => p.Id == planId, cancellationToken);
        if (!planExists) return Error.NotFound("Plan not found");

        var workingVersion = await _context.Set<PlanVersion>()
            .FirstOrDefaultAsync(v => v.PlanId == planId && v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active, cancellationToken);
        if (workingVersion is null) return Error.NotFound("No active working version exists for this plan");

        if (node.PlanVersionId != workingVersion.Id)
        {
            var nodeVersion = await _context.Set<PlanVersion>()
                .FirstOrDefaultAsync(v => v.Id == node.PlanVersionId, cancellationToken);
            return nodeVersion?.Type == PlanVersionType.Baseline
                ? Error.Validation("Baseline versions are immutable; edits are only allowed in the active Working version")
                : Error.Validation("Only the active Working version is editable");
        }

        return null;
    }

    private async Task<bool> ParentExistsInVersionAsync(Guid parentId, Guid planVersionId, CancellationToken cancellationToken)
    {
        return await _context.Set<PlanWbsNode>()
            .AnyAsync(n => n.Id == parentId && n.PlanVersionId == planVersionId, cancellationToken);
    }

    /// <summary>
    /// Walks up the parent chain starting from <paramref name="newParentId"/> and
    /// returns true when the node being moved would become its own ancestor.
    /// </summary>
    private async Task<bool> WouldCreateCycleAsync(Guid nodeId, Guid newParentId, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid> { nodeId };
        Guid? current = newParentId;

        while (current.HasValue)
        {
            if (!visited.Add(current.Value)) return true;

            var parent = await _context.Set<PlanWbsNode>()
                .FirstOrDefaultAsync(n => n.Id == current.Value, cancellationToken);
            current = parent?.ParentWbsId;
        }

        return false;
    }

    private async Task<int> NextSortOrderAsync(Guid planVersionId, Guid? parentWbsId, CancellationToken cancellationToken)
    {
        var max = await _context.Set<PlanWbsNode>()
            .Where(n => n.PlanVersionId == planVersionId && n.ParentWbsId == parentWbsId)
            .Select(n => (int?)n.SortOrder)
            .MaxAsync(cancellationToken);
        return (max ?? -1) + 1;
    }

    private Guid GetCurrentTenantId()
    {
        var tenantId = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        if (_httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(tenantId, out var parsed)
            && parsed != Guid.Empty)
        {
            return parsed;
        }

        throw new InvalidOperationException("A trusted tenant context is required for project planning operations.");
    }

    private static Guid? TryParseGuid(string value) => GuidHelper.TryParse(value);
}
