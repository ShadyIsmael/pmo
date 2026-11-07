namespace NEXUS.Modules.ProjectPlanning.Application.DTOs;

public class CreatePlanAssignmentDto
{
    public Guid ActivityId { get; set; }
    public Guid ResourceId { get; set; }
    /// <summary>Total work hours assigned to this resource for the task's scheduled period.</summary>
    public decimal? AssignedWorkHours { get; set; }
    /// <summary>Legacy compatibility field. New clients should use AssignedWorkHours.</summary>
    public decimal? AllocationPct { get; set; }
    /// <summary>Legacy compatibility field. New clients should use AssignedWorkHours.</summary>
    public decimal? HoursPerWeek { get; set; }
    /// <summary>Legacy planning/import field, mapped to the same canonical PlannedHours value.</summary>
    public decimal? PlannedHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? ProjectRoleId { get; set; }
}

public class UpdatePlanAssignmentDto : CreatePlanAssignmentDto
{
    /// <summary>The RowVersion observed by the client; enforces optimistic concurrency.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class CreatePlanAssignmentsBatchDto
{
    public Guid ActivityId { get; set; }
    public List<CreatePlanAssignmentBatchItemDto> Assignments { get; set; } = [];
}

public sealed class CreatePlanAssignmentBatchItemDto
{
    public Guid ResourceId { get; set; }
    public decimal AssignedWorkHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? ProjectRoleId { get; set; }
}

public class PlanAssignmentDto
{
    public Guid Id { get; set; }
    public Guid PlanVersionId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid ResourceId { get; set; }
    public decimal? AssignedWorkHours { get; set; }
    public decimal? AllocationPct { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public decimal? PlannedHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? ProjectRoleId { get; set; }
    public Guid? ResourceAllocationId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class PlanAssignmentCapacityDto
{
    public Guid ResourceId { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public DateTime MembershipStartDate { get; init; }
    public DateTime MembershipEndDate { get; init; }
    public bool IsScheduleWithinMembership { get; init; }
    public decimal ProjectAllocationPercentage { get; init; }
    public decimal ResourceCapacityHoursPerWeek { get; init; }
    public decimal ProjectCapacityHoursPerWeek { get; init; }
    public decimal ProjectCapacityHours { get; init; }
    public decimal AssignedWorkHours { get; init; }
    public decimal RemainingAssignableHours { get; init; }
}
