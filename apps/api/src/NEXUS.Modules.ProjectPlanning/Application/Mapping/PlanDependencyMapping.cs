using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.Mapping;

public static class PlanDependencyMapping
{
    public static PlanDependency ToEntity(this CreatePlanDependencyDto dto, Guid tenantId, Guid planVersionId, string userId)
    {
        return new PlanDependency
        {
            TenantId = tenantId,
            PlanVersionId = planVersionId,
            ActivityId = dto.ActivityId,
            PredecessorActivityId = dto.PredecessorActivityId,
            Type = dto.Type,
            Lag = dto.Lag,
            CreatedBy = userId
        };
    }

    public static PlanDependencyDto ToDto(this PlanDependency dependency)
    {
        return new PlanDependencyDto
        {
            Id = dependency.Id,
            PlanVersionId = dependency.PlanVersionId,
            ActivityId = dependency.ActivityId,
            PredecessorActivityId = dependency.PredecessorActivityId,
            Type = dependency.Type,
            Lag = dependency.Lag,
            RowVersion = dependency.RowVersion.ToArray()
        };
    }
}