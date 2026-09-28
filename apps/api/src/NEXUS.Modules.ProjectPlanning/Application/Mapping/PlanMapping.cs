using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.Mapping;

/// <summary>
/// Extension methods mapping <see cref="Plan"/> and <see cref="PlanVersion"/>
/// entities to their API projections.
/// </summary>
public static class PlanMapping
{
    public static PlanDto ToDto(this Plan plan)
    {
        return new PlanDto
        {
            Id = plan.Id,
            TenantId = plan.TenantId,
            ProjectId = plan.ProjectId,
            PlanType = plan.PlanType,
            Name = plan.Name,
            Description = plan.Description,
            Status = plan.Status,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt,
            CreatedBy = plan.CreatedBy,
            Versions = plan.Versions?.Select(v => v.ToDto()).ToList() ?? []
        };
    }

    public static PlanSummaryDto ToSummaryDto(this Plan plan, int versionCount)
    {
        return new PlanSummaryDto
        {
            Id = plan.Id,
            ProjectId = plan.ProjectId,
            PlanType = plan.PlanType,
            Name = plan.Name,
            Description = plan.Description,
            Status = plan.Status,
            VersionCount = versionCount,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt
        };
    }

    public static PlanVersionDto ToDto(this PlanVersion version)
    {
        return new PlanVersionDto
        {
            Id = version.Id,
            PlanId = version.PlanId,
            VersionNumber = version.VersionNumber,
            Type = version.Type,
            Status = version.Status,
            Name = version.Name,
            SourceVersionId = version.SourceVersionId,
            SourceBaselineId = version.SourceBaselineId,
            CreatedBy = version.CreatedBy,
            CreatedAt = version.CreatedAt,
            UpdatedAt = version.UpdatedAt,
            RowVersion = version.RowVersion.ToArray()
        };
    }
}
