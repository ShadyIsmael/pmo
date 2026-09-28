using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.Mapping;

public static class PlanAssignmentMapping
{
    public static PlanAssignment ToEntity(this CreatePlanAssignmentDto dto, Guid tenantId, Guid planVersionId, string userId)
    {
        return new PlanAssignment
        {
            TenantId = tenantId,
            PlanVersionId = planVersionId,
            ActivityId = dto.ActivityId,
            ResourceId = dto.ResourceId,
            AllocationPct = dto.AllocationPct,
            HoursPerWeek = dto.HoursPerWeek,
            PlannedHours = dto.PlannedHours,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ProjectRoleId = dto.ProjectRoleId,
            CreatedBy = userId
        };
    }

    public static PlanAssignmentDto ToDto(this PlanAssignment assignment)
    {
        return new PlanAssignmentDto
        {
            Id = assignment.Id,
            PlanVersionId = assignment.PlanVersionId,
            ActivityId = assignment.ActivityId,
            ResourceId = assignment.ResourceId,
            AllocationPct = assignment.AllocationPct,
            HoursPerWeek = assignment.HoursPerWeek,
            PlannedHours = assignment.PlannedHours,
            StartDate = assignment.StartDate,
            EndDate = assignment.EndDate,
            ProjectRoleId = assignment.ProjectRoleId,
            ResourceAllocationId = assignment.ResourceAllocationId,
            RowVersion = assignment.RowVersion.ToArray()
        };
    }
}
