namespace NEXUS.Api.ProgramManagement;

public sealed class ProgramManagementListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? Health { get; init; }
    public string? Status { get; init; }
    public string? ProgramManagerName { get; init; }
    public string? PortfolioName { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int ProjectCount { get; init; }
    public int ProjectsRequiringAttention { get; init; }
    public string ScheduleIndicator { get; init; } = "Unknown";
    public string FinancialIndicator { get; init; } = "Unavailable";
}

public sealed class ProgramManagementOverviewDto
{
    public ProgramManagementIdentityDto Program { get; init; } = new();
    public ProgramManagementSummaryDto Summary { get; init; } = new();
    public ProgramDeliverySummaryDto Delivery { get; init; } = new();
    public ProgramFinancialSummaryDto Financials { get; init; } = new();
    public ProgramRiskSummaryDto Risks { get; init; } = new();
    public ProgramIssueSummaryDto Issues { get; init; } = new();
    public ProgramResourceSummaryDto Resources { get; init; } = new();
    public ProgramStatusReportSummaryDto StatusReports { get; init; } = new();
    public IReadOnlyList<ProgramProjectManagementDto> Projects { get; init; } = [];
    public int ProjectsPage { get; init; }
    public int ProjectsPageSize { get; init; }
    public int ProjectsTotalCount { get; init; }
    public int ProjectsTotalPages => (int)Math.Ceiling(ProjectsTotalCount / (double)Math.Max(ProjectsPageSize, 1));
    public IReadOnlyList<ProgramMilestoneDto> Milestones { get; init; } = [];
    public IReadOnlyList<ProgramRiskDto> TopRisks { get; init; } = [];
    public IReadOnlyList<ProgramIssueDto> TopIssues { get; init; } = [];
    public IReadOnlyList<ProgramAttentionItemDto> AttentionItems { get; init; } = [];
    public IReadOnlyList<ProgramMeetingDto> UpcomingMeetings { get; init; } = [];
}

public sealed class ProgramManagementIdentityDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? Status { get; init; }
    public string? Health { get; init; }
    public string? ProgramManagerName { get; init; }
    public Guid? PortfolioId { get; init; }
    public string? PortfolioName { get; init; }
    public string Currency { get; init; } = string.Empty;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal? Budget { get; init; }
}

public sealed class ProgramManagementSummaryDto
{
    public int TotalProjects { get; init; }
    public int ActiveProjects { get; init; }
    public int CompletedProjects { get; init; }
    public int ProjectsAtRisk { get; init; }
    public int CriticalProjects { get; init; }
    public decimal? AverageProjectProgress { get; init; }
    public IReadOnlyDictionary<string, int> ProjectHealthDistribution { get; init; } = new Dictionary<string, int>();
}

public sealed class ProgramDeliverySummaryDto
{
    public int ProjectsBehindSchedule { get; init; }
    public int ProjectsWithScheduleData { get; init; }
    public int TotalMilestones { get; init; }
    public int OverdueMilestones { get; init; }
    public int DueSoonMilestones { get; init; }
    public int OnTrackMilestones { get; init; }
    public int CompletedMilestones { get; init; }
}

public sealed class ProgramProjectManagementDto
{
    public Guid Id { get; init; }
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Health { get; init; } = string.Empty;
    public string? ProjectManagerName { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal Progress { get; init; }
    public string ScheduleCondition { get; init; } = "Unknown";
    public DateTime? ForecastEndDate { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal? Budget { get; init; }
    public decimal? ActualCost { get; init; }
    public bool RequiresAttention { get; init; }
    public int OpenRiskCount { get; init; }
    public int OpenIssueCount { get; init; }
    public string StatusReportState { get; init; } = "Missing";
    public DateTime? LatestStatusReportDate { get; init; }
}

public sealed class ProgramFinancialSummaryDto
{
    public string BaseCurrency { get; init; } = string.Empty;
    public bool HasMixedCurrencies { get; init; }
    public IReadOnlyList<ProgramFinancialCurrencyGroupDto> Groups { get; init; } = [];
}

public sealed class ProgramFinancialCurrencyGroupDto
{
    public string Currency { get; init; } = string.Empty;
    public bool IsBaseCurrency { get; init; }
    public decimal Budget { get; init; }
    public decimal Actual { get; init; }
    public decimal Committed { get; init; }
    public decimal Forecast { get; init; }
    public decimal? Remaining { get; init; }
    public IReadOnlyList<ProgramFinancialProjectDto> Projects { get; init; } = [];
}

public sealed class ProgramFinancialProjectDto
{
    public Guid ProjectId { get; init; }
    public Guid ProjectPublicId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public decimal Budget { get; init; }
    public decimal Actual { get; init; }
    public decimal Committed { get; init; }
    public decimal Forecast { get; init; }
}

public sealed class ProgramRiskSummaryDto
{
    public int OpenCount { get; init; }
    public int OverdueCount { get; init; }
    public IReadOnlyDictionary<string, int> BySeverity { get; init; } = new Dictionary<string, int>();
}

public sealed class ProgramRiskDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectPublicId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? IdentifiedDate { get; init; }
}

public sealed class ProgramIssueSummaryDto
{
    public int OpenCount { get; init; }
    public int OverdueCount { get; init; }
    public IReadOnlyDictionary<string, int> BySeverity { get; init; } = new Dictionary<string, int>();
}

public sealed class ProgramIssueDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectPublicId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? TargetResolutionDate { get; init; }
}

public sealed class ProgramResourceSummaryDto
{
    public int ActiveAllocationCount { get; init; }
    public int ActiveResourceCount { get; init; }
    public int SharedResourceCount { get; init; }
    public int OverAllocatedResourceCount { get; init; }
    public decimal AllocatedHoursPerWeek { get; init; }
    public IReadOnlyList<ProgramResourcePressureDto> Pressure { get; init; } = [];
}

public sealed class ProgramResourcePressureDto
{
    public Guid ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    public decimal AllocationPercentage { get; init; }
    public int ProjectCount { get; init; }
    public IReadOnlyList<ProgramResourceProjectDto> Projects { get; init; } = [];
}

public sealed class ProgramResourceProjectDto
{
    public Guid ProjectId { get; init; }
    public Guid ProjectPublicId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public decimal AllocationPercentage { get; init; }
}

public sealed class ProgramStatusReportSummaryDto
{
    public int PublishedProjectCount { get; init; }
    public int CurrentCount { get; init; }
    public int StaleCount { get; init; }
    public int MissingCount { get; init; }
    public int FreshnessDays { get; init; }
    public IReadOnlyList<ProgramStatusReportDto> LatestPublished { get; init; } = [];
}

public sealed class ProgramStatusReportDto
{
    public Guid ProjectId { get; init; }
    public Guid ProjectPublicId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public DateTime ReportDate { get; init; }
    public string? Health { get; init; }
    public decimal? Progress { get; init; }
    public string Freshness { get; init; } = string.Empty;
}

public sealed class ProgramMilestoneDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectPublicId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
    public DateTime? CompletedDate { get; init; }
    public string State { get; init; } = string.Empty;
    public string? Status { get; init; }
}

public sealed class ProgramAttentionItemDto
{
    public string Level { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Guid? ProjectId { get; init; }
    public Guid? ProjectPublicId { get; init; }
    public Guid? ItemId { get; init; }
}

public sealed class ProgramMeetingDto
{
    public Guid Id { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ProjectPublicId { get; set; }
    public string? ProjectName { get; set; }
    public string Title { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
}
