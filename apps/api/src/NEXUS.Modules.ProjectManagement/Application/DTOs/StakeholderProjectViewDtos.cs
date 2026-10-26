namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

/// <summary>
/// Deliberately small read model for a user who is an active, canonical project
/// stakeholder. It does not reuse the full project overview because that payload
/// contains operational, financial and governance modules that stakeholders must
/// not receive through this surface.
/// </summary>
public sealed class StakeholderProjectViewDto
{
    public Guid Id { get; init; }
    public Guid PublicId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Health { get; init; } = string.Empty;
    public decimal Progress { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? ProjectManagerName { get; init; }
    public string? SponsorName { get; init; }
    public StakeholderPublishedStatusReportDto? LatestPublishedStatusReport { get; init; }
    public IReadOnlyList<StakeholderPublishedStatusHistoryDto> PublishedStatusHistory { get; init; } = [];
    public IReadOnlyList<StakeholderProjectMilestoneDto> Milestones { get; init; } = [];
}

public sealed class StakeholderPublishedStatusReportDto
{
    public Guid Id { get; init; }
    public DateTime ReportDate { get; init; }
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public string? ProjectLifecycleStatus { get; init; }
    public string? OverallHealth { get; init; }
    public decimal? OverallProgressPercent { get; init; }
    public string? ScheduleHealth { get; init; }
    public string? CostHealth { get; init; }
    public string? ScopeHealth { get; init; }
    public string? QualityHealth { get; init; }
    public string? ResourceHealth { get; init; }
    public string? ExecutiveSummary { get; init; }
    public string? KeyAchievements { get; init; }
    public string? ChallengesConcerns { get; init; }
    public string? NextPeriodPriorities { get; init; }
    public string? PreparedByName { get; init; }
    public string? PublishedByName { get; init; }
    public DateTime? PublishedAt { get; init; }
}

public sealed class StakeholderPublishedStatusHistoryDto
{
    public Guid Id { get; init; }
    public DateTime ReportDate { get; init; }
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public string? ProjectLifecycleStatus { get; init; }
    public string? OverallHealth { get; init; }
    public decimal? OverallProgressPercent { get; init; }
    public DateTime? PublishedAt { get; init; }
}

public sealed class StakeholderProjectMilestoneDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
    public DateTime? CompletedDate { get; init; }
    public string? Status { get; init; }
    public decimal? Progress { get; init; }
}
