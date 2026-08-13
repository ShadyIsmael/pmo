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
    public Guid? ProjectId { get; set; }
    public Guid? AssigneeId { get; set; }
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
}

public enum WorkItemType
{
    Task, Subtask, Bug, Feature, Epic, Story, Ticket, TestCase
}

public enum WorkItemPriority
{
    Critical, High, Medium, Low, Backlog
}

public enum WorkItemStatus
{
    Backlog, ToDo, InProgress, InReview, Done, Cancelled, OnHold
}

public enum WorkItemSource
{
    Internal, Jira, ServiceNow, AzureDevOps, Email
}
