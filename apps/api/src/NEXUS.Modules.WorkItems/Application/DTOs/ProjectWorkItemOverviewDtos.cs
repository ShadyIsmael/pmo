namespace NEXUS.Modules.WorkItems.Application.DTOs;

public sealed class ProjectWorkItemOverviewDto
{
    public int OpenIssueCount { get; init; }
    public int OverdueOpenIssueCount { get; init; }
    public IReadOnlyList<ProjectWorkItemOverviewItemDto> TopIssues { get; init; } = [];
}

public sealed class ProjectWorkItemOverviewItemDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
}
