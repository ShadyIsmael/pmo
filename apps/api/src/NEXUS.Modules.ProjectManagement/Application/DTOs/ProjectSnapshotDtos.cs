namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public class ProjectSnapshotSummaryDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int SnapshotNumber { get; set; }
    public string SnapshotName { get; set; } = string.Empty;
    public string SnapshotType { get; set; } = string.Empty;
    public DateTime SnapshotDate { get; set; }
    public DateTime EffectiveEndDate { get; set; }
    public Guid? PreviousProjectManagerId { get; set; }
    public string? PreviousProjectManagerName { get; set; }
    public Guid? NewProjectManagerId { get; set; }
    public string? NewProjectManagerName { get; set; }
    public string? HandoverReason { get; set; }
    public string? HandoverNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class ProjectSnapshotDto : ProjectSnapshotSummaryDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProgramId { get; set; }
    public string? ProgramName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? Category { get; set; }
    public Guid? SponsorId { get; set; }
    public string? SponsorName { get; set; }
    public Guid? PortfolioId { get; set; }
    public string? PortfolioName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public decimal? Budget { get; set; }
    public string Currency { get; set; } = "GBP";
    public decimal? ActualCost { get; set; }
    public decimal? PlannedValue { get; set; }
    public decimal? EarnedValue { get; set; }
    public decimal? CostVariance { get; set; }
    public decimal? ScheduleVariance { get; set; }
    public decimal Progress { get; set; }
    public string Health { get; set; } = string.Empty;
    public string RagStatus { get; set; } = string.Empty;
    public string ConfidenceLevel { get; set; } = string.Empty;
    public string? Phase { get; set; }
    public string? RiskRating { get; set; }
    public string? BusinessCase { get; set; }
    public string? Notes { get; set; }
    public ProjectSnapshotDataDto? Data { get; set; }
}

/// <summary>
/// Strongly-typed payload serialized into the snapshot <c>Data</c> column. Each collection holds
/// independent copies of the project-owned child data captured at snapshot time.
/// </summary>
public class ProjectSnapshotDataDto
{
    public IReadOnlyList<SnapshotMemberDto> Members { get; set; } = Array.Empty<SnapshotMemberDto>();
    public IReadOnlyList<SnapshotAllocationDto> Allocations { get; set; } = Array.Empty<SnapshotAllocationDto>();
    public IReadOnlyList<SnapshotPhaseDto> Phases { get; set; } = Array.Empty<SnapshotPhaseDto>();
    public IReadOnlyList<SnapshotMilestoneDto> Milestones { get; set; } = Array.Empty<SnapshotMilestoneDto>();
    public IReadOnlyList<SnapshotDeliverableDto> Deliverables { get; set; } = Array.Empty<SnapshotDeliverableDto>();
    public IReadOnlyList<SnapshotStakeholderDto> Stakeholders { get; set; } = Array.Empty<SnapshotStakeholderDto>();
    public IReadOnlyList<SnapshotScheduleDto> Schedules { get; set; } = Array.Empty<SnapshotScheduleDto>();
    public IReadOnlyList<SnapshotWbsDto> WbsItems { get; set; } = Array.Empty<SnapshotWbsDto>();
    public IReadOnlyList<SnapshotBaselineDto> Baselines { get; set; } = Array.Empty<SnapshotBaselineDto>();
    public IReadOnlyList<SnapshotStatusReportDto> StatusReports { get; set; } = Array.Empty<SnapshotStatusReportDto>();
    public IReadOnlyList<SnapshotWorkItemDto> WorkItems { get; set; } = Array.Empty<SnapshotWorkItemDto>();
    public IReadOnlyList<SnapshotRiskDto> Risks { get; set; } = Array.Empty<SnapshotRiskDto>();
    public IReadOnlyList<SnapshotIssueDto> Issues { get; set; } = Array.Empty<SnapshotIssueDto>();
    public IReadOnlyList<SnapshotBudgetDto> Budgets { get; set; } = Array.Empty<SnapshotBudgetDto>();
    public IReadOnlyList<SnapshotCostEntryDto> CostEntries { get; set; } = Array.Empty<SnapshotCostEntryDto>();
    public IReadOnlyList<SnapshotMeetingDto> Meetings { get; set; } = Array.Empty<SnapshotMeetingDto>();
    public IReadOnlyList<SnapshotDocumentDto> Documents { get; set; } = Array.Empty<SnapshotDocumentDto>();
}

public class SnapshotMemberDto
{
    public Guid OriginalId { get; set; }
    public Guid ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public string? ResourceJobTitle { get; set; }
    public string Role { get; set; } = string.Empty;
    public decimal AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsBillable { get; set; }
    public string? Notes { get; set; }
}

public class SnapshotAllocationDto
{
    public Guid OriginalId { get; set; }
    public Guid ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public Guid? ProjectRoleId { get; set; }
    public decimal AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsBillable { get; set; }
    public string? Notes { get; set; }
}

public class SnapshotPhaseDto
{
    public Guid OriginalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Progress { get; set; }
}

public class SnapshotMilestoneDto
{
    public Guid OriginalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Status { get; set; }
    public decimal? Progress { get; set; }
    public string? Category { get; set; }
    public Guid? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
}

public class SnapshotDeliverableDto
{
    public Guid OriginalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Status { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public Guid? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public string? QualityCriteria { get; set; }
}

public class SnapshotStakeholderDto
{
    public Guid OriginalId { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? Interest { get; set; }
    public string? Influence { get; set; }
}

public class SnapshotScheduleDto
{
    public Guid OriginalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? BaselineStartDate { get; set; }
    public DateTime? BaselineEndDate { get; set; }
    public DateTime? ForecastStartDate { get; set; }
    public DateTime? ForecastEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public int? TotalDays { get; set; }
    public int? RemainingDays { get; set; }
}

public class SnapshotWbsDto
{
    public Guid OriginalId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? Deliverable { get; set; }
    public decimal? PlannedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public decimal? Progress { get; set; }
    public int SortOrder { get; set; }
    public string? Status { get; set; }
}

public class SnapshotBaselineDto
{
    public Guid OriginalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Version { get; set; }
    public DateTime? BaselineDate { get; set; }
    public bool IsCurrent { get; set; }
}

public class SnapshotStatusReportDto
{
    public Guid OriginalId { get; set; }
    public string? Status { get; set; }
    public string? Summary { get; set; }
    public string? Accomplishments { get; set; }
    public string? NextSteps { get; set; }
    public string? Blockers { get; set; }
    public decimal? ScheduleVariance { get; set; }
    public decimal? CostVariance { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime ReportedAt { get; set; }
    public Guid? ReportedBy { get; set; }
    public string? ReportedByName { get; set; }
}

public class SnapshotWorkItemDto
{
    public Guid OriginalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public Guid? ParentId { get; set; }
    public int? StoryPoints { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int SortOrder { get; set; }
}

public class SnapshotRiskDto
{
    public Guid OriginalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Impact { get; set; } = string.Empty;
    public string Likelihood { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? PotentialCost { get; set; }
    public decimal? PotentialDelay { get; set; }
    public string? RiskResponse { get; set; }
    public Guid? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public DateTime? IdentifiedDate { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
    public DateTime? ClosedDate { get; set; }
}

public class SnapshotIssueDto
{
    public Guid OriginalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public DateTime? IdentifiedDate { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? Resolution { get; set; }
}

public class SnapshotBudgetDto
{
    public Guid OriginalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal CommittedAmount { get; set; }
    public decimal ForecastAmount { get; set; }
    public string Currency { get; set; } = "GBP";
    public int FiscalYear { get; set; }
    public int? FiscalQuarter { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class SnapshotCostEntryDto
{
    public Guid OriginalId { get; set; }
    public Guid? BudgetId { get; set; }
    public Guid? ResourceId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public decimal? TaxAmount { get; set; }
    public DateTime IncurredDate { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? VendorName { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class SnapshotMeetingDto
{
    public Guid OriginalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? MeetingType { get; set; }
    public string? Location { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public Guid? OrganizerId { get; set; }
    public string? OrganizerName { get; set; }
    public string? Recurrence { get; set; }
}

public class SnapshotDocumentDto
{
    public Guid OriginalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FileName { get; set; }
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public string? Tags { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public Guid? OwnerId { get; set; }
    public string? OwnerName { get; set; }
}
