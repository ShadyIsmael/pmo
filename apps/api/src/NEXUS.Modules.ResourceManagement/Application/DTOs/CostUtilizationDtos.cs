namespace NEXUS.Modules.ResourceManagement.Application.DTOs;

public sealed class CostUtilizationFilterDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public Guid? ResourceId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? VendorId { get; set; }
    public Guid? ProjectRoleId { get; set; }
    public Guid? PlanActivityId { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? DepartmentId { get; set; }
}

public sealed class CostUtilizationReportDto
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public bool CanViewCosts { get; init; }
    public CostUtilizationMetricDto Operational { get; init; } = new();
    public IReadOnlyList<CostUtilizationMetricDto> ByCurrency { get; init; } = [];
    public IReadOnlyList<CostUtilizationResourceDto> Resources { get; init; } = [];
    public IReadOnlyList<CostUtilizationAttributionDto> Projects { get; init; } = [];
    public IReadOnlyList<CostUtilizationAttributionDto> Vendors { get; init; } = [];
    public IReadOnlyList<CostUtilizationAttributionDto> Roles { get; init; } = [];
    public IReadOnlyList<CostUtilizationDailyDto> Daily { get; init; } = [];
    public IReadOnlyList<CostUtilizationWarningDto> Warnings { get; init; } = [];
}

public class CostUtilizationMetricDto
{
    public string? Currency { get; init; }
    public decimal CapacityHours { get; init; }
    public decimal PlannedHours { get; init; }
    public decimal ActualHours { get; init; }
    public decimal RegularHours { get; init; }
    public decimal PlanningGapHours { get; init; }
    public decimal ExecutionGapHours { get; init; }
    public decimal UnusedCapacityHours { get; init; }
    public decimal ExcessHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal? PlannedUtilization { get; init; }
    public decimal? ActualUtilization { get; init; }
    public decimal? PlannedLaborCost { get; init; }
    public decimal? ActualLaborCost { get; init; }
    public decimal? LaborCostVariance { get; init; }
    public decimal? OvertimeCost { get; init; }
    public decimal? UnusedCapacityCost { get; init; }
    public decimal? CommittedCapacityCost { get; init; }
    public decimal? TotalResourceCost { get; init; }
    public bool CostProfileMissing { get; init; }
    public bool CostCoverageComplete { get; init; } = true;
    public decimal UncostedActualHours { get; init; }
    public decimal UncostedPlannedHours { get; init; }
}

public sealed class CostUtilizationResourceDto
{
    public Guid ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    public string ResourceType { get; init; } = string.Empty;
    public string? DepartmentName { get; init; }
    public Guid? VendorId { get; init; }
    public string? VendorName { get; init; }
    public CostUtilizationMetricDto Metrics { get; init; } = new();
}

public sealed class CostUtilizationAttributionDto
{
    public Guid? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Currency { get; init; }
    public decimal PlannedHours { get; init; }
    public decimal ActualHours { get; init; }
    public decimal RegularHours { get; init; }
    public decimal ExcessHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal? PlannedLaborCost { get; init; }
    public decimal? ActualLaborCost { get; init; }
    public decimal? OvertimeCost { get; init; }
    public decimal? LaborCostVariance { get; init; }
    public bool CostProfileMissing { get; init; }
    public bool CostCoverageComplete { get; init; } = true;
}

public sealed class CostUtilizationDailyDto : CostUtilizationMetricDto
{
    public Guid ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    public DateTime WorkDate { get; init; }
}

public sealed class CostUtilizationWarningDto
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Guid? ResourceId { get; init; }
    public string? ResourceName { get; init; }
}
