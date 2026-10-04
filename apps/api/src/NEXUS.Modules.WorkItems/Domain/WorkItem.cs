using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkItems.Domain;

public class WorkItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Severity { get; set; }
    public DateTime? IdentifiedDate { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? Resolution { get; set; }
    public Guid? ProjectId { get; set; }
    /// <summary>Exactly one of AssignedResourceId and AssignedTeamId may be set.</summary>
    public Guid? AssignedResourceId { get; set; }
    /// <summary>Exactly one of AssignedResourceId and AssignedTeamId may be set.</summary>
    public Guid? AssignedTeamId { get; set; }
    public Guid? ReporterId { get; set; }
    public Guid? ParentId { get; set; }
    public int? StoryPoints { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Tags { get; set; }
    public string? Labels { get; set; }
    public int SortOrder { get; set; }

    /// <summary>
    /// Updates the status projection from the Workflow Engine. Callers must not use
    /// the regular WorkItem update path to change lifecycle status.
    /// </summary>
    public void ApplyWorkflowStatusProjection(string statusCode, string userId)
    {
        Status = statusCode;
        UpdatedBy = userId;
    }
}

public enum WorkItemType
{
    Task, Issue, Subtask, Bug, Feature, Epic, Story, Ticket, TestCase
}

public enum WorkItemPriority
{
    Critical, High, Medium, Low, Backlog
}

public enum WorkItemSource
{
    Internal, Jira, ServiceNow, AzureDevOps, Email
}
