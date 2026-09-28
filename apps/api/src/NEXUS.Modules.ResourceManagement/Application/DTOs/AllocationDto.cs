using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.ResourceManagement.Application.DTOs;

public class AllocationDto
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string? ResourceJobTitle { get; set; }
    public string? ResourceType { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? ProjectRoleId { get; set; }
    public decimal AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public decimal? PlannedHours { get; set; }
    public decimal? PlannedLaborCost { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsBillable { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateAllocationDto
{
    public Guid ResourceId { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? ProjectRoleId { get; set; }
    public decimal AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = ResourceAllocation.StatusConfirmed;
    public bool IsBillable { get; set; } = true;
    public string? Notes { get; set; }
}

public class UpdateAllocationDto
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? ProjectRoleId { get; set; }
    public decimal? AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Status { get; set; }
    public bool? IsBillable { get; set; }
    public string? Notes { get; set; }
}

public class AllocationConflictDto
{
    public Guid ExistingAllocationId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string ExistingProjectName { get; set; } = string.Empty;
    public string ExistingRole { get; set; } = string.Empty;
    public decimal ExistingPercentage { get; set; }
    public decimal NewTotalPercentage { get; set; }
    public DateTime ExistingStart { get; set; }
    public DateTime ExistingEnd { get; set; }
    public string Severity { get; set; } = "Warning";
    public string Description { get; set; } = string.Empty;
}
