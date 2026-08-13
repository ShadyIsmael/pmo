using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.Mapping;

public static class PlanActivityMapping
{
    public static PlanActivity ToEntity(this CreatePlanActivityDto dto, Guid tenantId, Guid planVersionId, string userId)
    {
        return new PlanActivity
        {
            TenantId = tenantId,
            PlanVersionId = planVersionId,
            Code = dto.Code?.Trim() ?? string.Empty,
            Name = dto.Name?.Trim() ?? string.Empty,
            Description = dto.Description?.Trim(),
            Type = dto.Type,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Duration = dto.Duration,
            EstimateHours = dto.EstimateHours,
            Progress = dto.Progress,
            WbsId = dto.WbsId,
            CreatedBy = userId
        };
    }

    public static PlanActivityDto ToDto(this PlanActivity activity)
    {
        return new PlanActivityDto
        {
            Id = activity.Id,
            PlanVersionId = activity.PlanVersionId,
            Code = activity.Code,
            Name = activity.Name,
            Description = activity.Description,
            Type = activity.Type,
            StartDate = activity.StartDate,
            EndDate = activity.EndDate,
            Duration = activity.Duration,
            SortOrder = activity.SortOrder,
            EstimateHours = activity.EstimateHours,
            Progress = activity.Progress,
            WbsId = activity.WbsId,
            RowVersion = activity.RowVersion.ToArray()
        };
    }
}