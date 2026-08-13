using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.Mapping;

public static class PlanWbsMapping
{
    public static PlanWbsNode ToEntity(this CreatePlanWbsNodeDto dto, Guid tenantId, Guid planVersionId, string userId)
    {
        return new PlanWbsNode
        {
            TenantId = tenantId,
            PlanVersionId = planVersionId,
            ParentWbsId = dto.ParentWbsId,
            SortOrder = dto.SortOrder ?? 0,
            Code = dto.Code?.Trim() ?? string.Empty,
            Name = dto.Name?.Trim() ?? string.Empty,
            CreatedBy = userId
        };
    }

    public static PlanWbsNodeDto ToDto(this PlanWbsNode node)
    {
        return new PlanWbsNodeDto
        {
            Id = node.Id,
            PlanVersionId = node.PlanVersionId,
            ParentWbsId = node.ParentWbsId,
            SortOrder = node.SortOrder,
            Code = node.Code,
            Name = node.Name,
            ActivityId = node.ActivityId,
            DeliverableId = node.DeliverableId,
            RowVersion = node.RowVersion.ToArray(),
            Children = []
        };
    }

    /// <summary>
    /// Builds the full WBS tree for a set of nodes ordered by <see cref="SortOrder"/>.
    /// Nodes with no parent, or whose parent is not in the collection, are roots.
    /// </summary>
    public static IReadOnlyList<PlanWbsNodeDto> ToTree(this ICollection<PlanWbsNode> nodes)
    {
        var ordered = nodes
            .OrderBy(n => n.SortOrder)
            .ThenBy(n => n.CreatedAt)
            .ToList();

        var byId = ordered.ToDictionary(n => n.Id);
        var childrenByParent = ordered
            .Where(n => n.ParentWbsId.HasValue)
            .GroupBy(n => n.ParentWbsId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.SortOrder).ThenBy(n => n.CreatedAt).ToList());

        return ordered
            .Where(n => !n.ParentWbsId.HasValue || !byId.ContainsKey(n.ParentWbsId.Value))
            .Select(n => ToDtoWithChildren(n, byId, childrenByParent))
            .ToList();
    }

    private static PlanWbsNodeDto ToDtoWithChildren(
        PlanWbsNode node,
        IReadOnlyDictionary<Guid, PlanWbsNode> byId,
        IReadOnlyDictionary<Guid, List<PlanWbsNode>> childrenByParent)
    {
        var dto = node.ToDto();
        if (childrenByParent.TryGetValue(node.Id, out var children))
        {
            dto.Children = children.Select(c => ToDtoWithChildren(c, byId, childrenByParent)).ToList();
        }

        return dto;
    }
}