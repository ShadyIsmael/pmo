namespace NEXUS.Modules.ResourceManagement.Application.DTOs;

public class CapacityMatrixDto
{
    public List<CapacityMatrixRowDto> Rows { get; set; } = new();
    public List<CapacityPeriodDto> Periods { get; set; } = new();
    public CapacitySummaryDto Summary { get; set; } = new();
}

public class CapacityMatrixRowDto
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? DepartmentName { get; set; }
    public string? Seniority { get; set; }
    public decimal WeeklyCapacityHours { get; set; }
    public decimal CurrentUtilization { get; set; }
    public DateTime? EarliestAvailableDate { get; set; }
    public decimal? CostPerHour { get; set; }
    public string? Currency { get; set; }
    public bool HasCapacityData { get; set; }
    public List<CapacityCellDto> Cells { get; set; } = new();
}

public sealed class ProjectTeamCapacityMemberInputDto
{
    public Guid ResourceId { get; init; }
    public Guid? ProjectId { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal? AllocationPercentage { get; init; }
    public decimal? HoursPerWeek { get; init; }
    public bool UseProjectCalendar { get; init; }
    public Guid? ProjectCalendarId { get; init; }
}

public sealed class ProjectTeamCapacityOverviewDto
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int ActiveResourceCount { get; init; }
    public int? IdleResourceCount { get; init; }
    public decimal? TotalCapacityHours { get; init; }
    public decimal? UtilizedHours { get; init; }
    public decimal? AvailableHours { get; init; }
    public decimal? OverallocatedHours { get; init; }
    public decimal? UtilizedPercent { get; init; }
    public decimal? AvailablePercent { get; init; }
    public bool IsCapacityDataAvailable { get; init; }
    public bool IsAllocationDataAvailable { get; init; }
}

public class CapacityCellDto
{
    public string PeriodKey { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal GrossCapacityHours { get; set; }
    public decimal NetCapacityHours { get; set; }
    public decimal LeaveHours { get; set; }
    public decimal ConfirmedHours { get; set; }
    public decimal ProposedHours { get; set; }
    public decimal AvailableHours { get; set; }
    public decimal UtilizationPercent { get; set; }
    public string HeatmapState { get; set; } = "Available";
    public decimal OverallocatedHours { get; set; }
    public List<AllocationBreakdownDto> Allocations { get; set; } = new();
}

public class AllocationBreakdownDto
{
    public Guid AllocationId { get; set; }
    public Guid? ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
    public decimal Hours { get; set; }
    public decimal? PlannedHours { get; set; }
    public decimal? PlannedLaborCost { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsBillable { get; set; }
    public string? Currency { get; set; }
}

public class CapacityPeriodDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}

public class CapacitySummaryDto
{
    public decimal TotalGrossCapacityHours { get; set; }
    public decimal TotalNetCapacityHours { get; set; }
    public decimal TotalConfirmedHours { get; set; }
    public decimal TotalProposedHours { get; set; }
    public decimal TotalAvailableHours { get; set; }
    public decimal OverallUtilizationPercent { get; set; }
}

public class ResourceAvailabilityDto
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? DepartmentName { get; set; }
    public decimal WeeklyCapacityHours { get; set; }
    public bool HasCapacityData { get; set; }
    public decimal CurrentUtilization { get; set; }
    public decimal AvailableCapacityHours { get; set; }
    public DateTime? EarliestAvailableDate { get; set; }
    public decimal? CostPerHour { get; set; }
    public string? Currency { get; set; }
    public List<AllocationConflictDto> Conflicts { get; set; } = new();
}

public class CapacityFilterDto
{
    public string PeriodType { get; set; } = "Week";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? DepartmentId { get; set; }
    public string? LocationId { get; set; }
    public string? ResourceType { get; set; }
    public Guid? ResourceId { get; set; }
    public bool IncludeProposed { get; set; } = true;
}

public class DemandVsCapacityDto
{
    public List<DemandVsCapacityGroupDto> Groups { get; set; } = new();
    public DemandVsCapacityTotalDto Total { get; set; } = new();
}

public class DemandVsCapacityGroupDto
{
    public string GroupType { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public decimal DemandHours { get; set; }
    public decimal CapacityHours { get; set; }
    public decimal GapHours { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class DemandVsCapacityTotalDto
{
    public decimal TotalDemandHours { get; set; }
    public decimal TotalCapacityHours { get; set; }
    public decimal TotalGapHours { get; set; }
}
