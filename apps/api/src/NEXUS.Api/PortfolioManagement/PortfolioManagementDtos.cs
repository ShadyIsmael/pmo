namespace NEXUS.Api.PortfolioManagement;

public sealed class PortfolioManagementListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? OwnerName { get; init; }
    public string? Status { get; init; }
    public string? Health { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int ProgramCount { get; init; }
    public int DirectProjectCount { get; init; }
    public int TotalProjectCount { get; init; }
    public int ProjectsRequiringAttention { get; init; }
    public int ProgramsRequiringAttention { get; init; }
    public string FinancialIndicator { get; init; } = "Unavailable";
    public string RiskIndicator { get; init; } = "Clear";
    public string ReportingIndicator { get; init; } = "Missing";
}

public sealed class PortfolioManagementOverviewDto
{
    public PortfolioManagementIdentityDto Portfolio { get; init; } = new();
    public PortfolioStructureSummaryDto Summary { get; init; } = new();
    public PortfolioDeliverySummaryDto Delivery { get; init; } = new();
    public PortfolioFinancialSummaryDto Financials { get; init; } = new();
    public PortfolioRiskSummaryDto Risks { get; init; } = new();
    public PortfolioIssueSummaryDto Issues { get; init; } = new();
    public PortfolioResourceSummaryDto Resources { get; init; } = new();
    public PortfolioStatusReportSummaryDto StatusReports { get; init; } = new();
    public IReadOnlyList<PortfolioProgramDto> Programs { get; init; } = [];
    public int ProgramsPage { get; init; }
    public int ProgramsPageSize { get; init; }
    public int ProgramsTotalCount { get; init; }
    public int ProgramsTotalPages => (int)Math.Ceiling(ProgramsTotalCount / (double)Math.Max(ProgramsPageSize, 1));
    public IReadOnlyList<PortfolioProjectDto> Projects { get; init; } = [];
    public int ProjectsPage { get; init; }
    public int ProjectsPageSize { get; init; }
    public int ProjectsTotalCount { get; init; }
    public int ProjectsTotalPages => (int)Math.Ceiling(ProjectsTotalCount / (double)Math.Max(ProjectsPageSize, 1));
    public IReadOnlyList<PortfolioMilestoneDto> Milestones { get; init; } = [];
    public IReadOnlyList<PortfolioRiskDto> TopRisks { get; init; } = [];
    public IReadOnlyList<PortfolioIssueDto> TopIssues { get; init; } = [];
    public IReadOnlyList<PortfolioAttentionItemDto> AttentionItems { get; init; } = [];
    public IReadOnlyList<PortfolioMeetingDto> UpcomingMeetings { get; init; } = [];
}

public sealed class PortfolioManagementIdentityDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? Description { get; init; }
    public string? Status { get; init; }
    public string? Health { get; init; }
    public string? OwnerName { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? StrategicObjective { get; init; }
    public string? Priority { get; init; }
    public string? ConfidenceLevel { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public sealed class PortfolioStructureSummaryDto
{
    public int TotalPrograms { get; init; }
    public int TotalProjects { get; init; }
    public int ProgramContainedProjects { get; init; }
    public int DirectPortfolioProjects { get; init; }
    public int ActiveProjects { get; init; }
    public int CompletedProjects { get; init; }
    public int AtRiskProjects { get; init; }
    public int CriticalProjects { get; init; }
    public decimal? AverageProjectProgress { get; init; }
    public IReadOnlyDictionary<string, int> ProgramHealthDistribution { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> ProjectHealthDistribution { get; init; } = new Dictionary<string, int>();
}

public sealed class PortfolioProgramDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? ProgramManagerName { get; init; }
    public string? Status { get; init; }
    public string? Health { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int ProjectCount { get; init; }
    public int ProjectsAtRisk { get; init; }
    public string DeliveryIndicator { get; init; } = "Unknown";
    public string FinancialIndicator { get; init; } = "Unavailable";
    public string RiskIndicator { get; init; } = "Clear";
    public string ReportingIndicator { get; init; } = "Missing";
}

public sealed class PortfolioProjectDto
{
    public Guid Id { get; init; }
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public Guid? ProgramId { get; init; }
    public string? ProgramName { get; init; }
    public bool IsDirectProject { get; init; }
    public string? ProjectManagerName { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Health { get; init; } = "Unknown";
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal Progress { get; init; }
    public string ScheduleCondition { get; init; } = "Unknown";
    public string Currency { get; init; } = string.Empty;
    public string FinancialCondition { get; init; } = "Unavailable";
    public string RiskCondition { get; init; } = "Clear";
    public string StatusReportFreshness { get; init; } = "Missing";
    public DateTime? LatestStatusReportDate { get; init; }
    public bool RequiresAttention { get; init; }
}

public sealed class PortfolioDeliverySummaryDto
{
    public IReadOnlyDictionary<string, int> ProgramDeliveryConditions { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> ProjectScheduleConditions { get; init; } = new Dictionary<string, int>();
    public int DelayedProjects { get; init; }
    public int DelayedPrograms { get; init; }
    public int ProjectsWithScheduleData { get; init; }
    public int TotalMilestones { get; init; }
    public int OverdueMilestones { get; init; }
    public int DueSoonMilestones { get; init; }
    public int OnTrackMilestones { get; init; }
    public int CompletedMilestones { get; init; }
    public IReadOnlyDictionary<string, int> ProjectProgressDistribution { get; init; } = new Dictionary<string, int>();
}

public sealed class PortfolioFinancialSummaryDto
{
    public string BaseCurrency { get; init; } = string.Empty;
    public bool HasMixedCurrencies { get; init; }
    public IReadOnlyList<PortfolioFinancialCurrencyGroupDto> Groups { get; init; } = [];
}

public sealed class PortfolioFinancialCurrencyGroupDto
{
    public string Currency { get; init; } = string.Empty;
    public bool IsBaseCurrency { get; init; }
    public int ProjectCount { get; init; }
    public decimal Budget { get; init; }
    public decimal Allocated { get; init; }
    public decimal Actual { get; init; }
    public decimal Committed { get; init; }
    public decimal Forecast { get; init; }
    public decimal Remaining { get; init; }
    public decimal Variance { get; init; }
    public IReadOnlyList<PortfolioFinancialContributionDto> Contributions { get; init; } = [];
}

public sealed class PortfolioFinancialContributionDto
{
    public Guid ProjectId { get; init; }
    public Guid ProjectPublicId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public Guid? ProgramId { get; init; }
    public string? ProgramName { get; init; }
    public bool IsDirectProject { get; init; }
    public decimal Budget { get; init; }
    public decimal Allocated { get; init; }
    public decimal Actual { get; init; }
    public decimal Committed { get; init; }
    public decimal Forecast { get; init; }
}

public sealed class PortfolioRiskSummaryDto
{
    public string SourceScope { get; init; } = "Project";
    public int OpenCount { get; init; }
    public int CriticalCount { get; init; }
    public int HighCount { get; init; }
    public int OverdueCount { get; init; }
    public IReadOnlyDictionary<string, int> ByProgram { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> ByProject { get; init; } = new Dictionary<string, int>();
}

public sealed class PortfolioIssueSummaryDto
{
    public int OpenCount { get; init; }
    public int CriticalCount { get; init; }
    public int OverdueCount { get; init; }
    public IReadOnlyDictionary<string, int> ByProgram { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> ByProject { get; init; } = new Dictionary<string, int>();
}

public sealed class PortfolioRiskDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectPublicId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public Guid? ProgramId { get; init; }
    public string? ProgramName { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? IdentifiedDate { get; init; }
}

public sealed class PortfolioIssueDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectPublicId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public Guid? ProgramId { get; init; }
    public string? ProgramName { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? TargetResolutionDate { get; init; }
}

public sealed class PortfolioResourceSummaryDto
{
    public int ActiveAllocationCount { get; init; }
    public int ActiveResourceCount { get; init; }
    public int SharedResourceCount { get; init; }
    public int CrossProgramResourceCount { get; init; }
    public int OverAllocatedResourceCount { get; init; }
    public decimal AllocatedHoursPerWeek { get; init; }
    public IReadOnlyList<PortfolioResourcePressureDto> Pressure { get; init; } = [];
}

public sealed class PortfolioResourcePressureDto
{
    public Guid ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    public decimal AllocationPercentage { get; init; }
    public int ProgramCount { get; init; }
    public int ProjectCount { get; init; }
    public IReadOnlyList<PortfolioResourceProjectDto> Projects { get; init; } = [];
}

public sealed class PortfolioResourceProjectDto
{
    public Guid ProjectId { get; init; }
    public Guid ProjectPublicId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public Guid? ProgramId { get; init; }
    public string? ProgramName { get; init; }
    public bool IsDirectProject { get; init; }
    public decimal AllocationPercentage { get; init; }
}

public sealed class PortfolioStatusReportSummaryDto
{
    public int PublishedProjectCount { get; init; }
    public int CurrentCount { get; init; }
    public int StaleCount { get; init; }
    public int MissingCount { get; init; }
    public int FreshnessDays { get; init; }
    public IReadOnlyList<string> ProgramsWithReportingGaps { get; init; } = [];
    public IReadOnlyList<string> ProjectsWithReportingGaps { get; init; } = [];
    public IReadOnlyList<PortfolioStatusReportDto> LatestPublished { get; init; } = [];
}

public sealed class PortfolioStatusReportDto
{
    public Guid ProjectId { get; init; }
    public Guid ProjectPublicId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public Guid? ProgramId { get; init; }
    public string? ProgramName { get; init; }
    public DateTime ReportDate { get; init; }
    public string? Health { get; init; }
    public decimal? Progress { get; init; }
    public string Freshness { get; init; } = string.Empty;
}

public sealed class PortfolioMilestoneDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectPublicId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public Guid? ProgramId { get; init; }
    public string? ProgramName { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
    public DateTime? CompletedDate { get; init; }
    public string State { get; init; } = "Unknown";
    public string? Status { get; init; }
}

public sealed class PortfolioAttentionItemDto
{
    public string Level { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public string EntityName { get; init; } = string.Empty;
    public Guid? ProgramId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ProjectPublicId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class PortfolioMeetingDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ProjectPublicId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? ProgramId { get; set; }
    public string? ProgramName { get; set; }
}
