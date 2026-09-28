namespace NEXUS.Modules.ProjectPlanning.Application.DTOs;

public class CreatePlanAssignmentDto
{
    public Guid ActivityId { get; set; }
    public Guid ResourceId { get; set; }
    public decimal? AllocationPct { get; set; }
    public decimal? HoursPerWeek { get; set; }
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

public class PlanAssignmentDto
{
    public Guid Id { get; set; }
    public Guid PlanVersionId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid ResourceId { get; set; }
    public decimal? AllocationPct { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public decimal? PlannedHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? ProjectRoleId { get; set; }
    public Guid? ResourceAllocationId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
