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

    public PlanWbsService(DbContext context, IAuditService auditService, IHttpContextAccessor httpContextAccessor, SummaryTaskRollupService? summaryRollup = null)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _summaryRollup = summaryRollup ?? new SummaryTaskRollupService(context);
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
        var previousSummaryIds = await _summaryRollup.RecalculateAsync(node.PlanVersionId, userId, cancellationToken: cancellationToken);
        node.Name = name;
        node.Code = code;
        node.ParentWbsId = dto.ParentWbsId;
        node.SortOrder = dto.SortOrder;
        node.TextFormat = dto.TextFormat.ToEntity();
        node.UpdatedBy = userId;

        await _summaryRollup.RecalculateAsync(node.PlanVersionId, userId, previousSummaryIds, cancellationToken);

        _context.Entry(node).Property(n => n.RowVersion).OriginalValue = dto.RowVersion;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PlanWbsNodeDto>.Failure(Error.Conflict("The node was modified by another user. Reload and try again."));
        }

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

        return Result<PlanWbsNodeDto>.Success(node.ToDto());
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
        if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var parsed))
        {
            return parsed;
        }

        return TenantConstants.DemoTenantId;
    }

    private static Guid? TryParseGuid(string value) => GuidHelper.TryParse(value);
}
