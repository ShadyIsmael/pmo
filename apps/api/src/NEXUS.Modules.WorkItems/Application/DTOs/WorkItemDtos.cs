using NEXUS.Modules.WorkItems.Domain;

namespace NEXUS.Modules.WorkItems.Application.DTOs;

public sealed class CreateWorkItemDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkItemType Type { get; set; } = WorkItemType.Task;
    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Medium;
    public string? Status { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? AssignedResourceId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public Guid? ParentId { get; set; }
    public int? StoryPoints { get; set; }
    public decimal? EstimatedHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Severity { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
    public string? Tags { get; set; }
    public string? Labels { get; set; }
}

public sealed class UpdateWorkItemDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkItemType Type { get; set; } = WorkItemType.Task;
    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Medium;
    public string? Status { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? AssignedResourceId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public Guid? ParentId { get; set; }
    public int? StoryPoints { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Severity { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? Resolution { get; set; }
    public string? Tags { get; set; }
    public string? Labels { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class WorkItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkItemType Type { get; set; }
    public WorkItemPriority Priority { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public Guid? ProjectPublicId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? AssignedResourceId { get; set; }
    public string? AssignedResourceName { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public string? AssignedTeamName { get; set; }
    public Guid? ReporterId { get; set; }
    public Guid? ParentId { get; set; }
    public int? StoryPoints { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Severity { get; set; }
    public DateTime? IdentifiedDate { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? Resolution { get; set; }
    public string? Tags { get; set; }
    public string? Labels { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class WorkItemAssigneeDto
{
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public Guid? ResourceId { get; set; }
    public Guid? TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? JobTitle { get; set; }
}

public sealed class WorkItemInsightsDto
{
    public int Total { get; set; }
    public int Open { get; set; }
    public int Overdue { get; set; }
    public int Unassigned { get; set; }
    public int DueThisWeek { get; set; }
    public IReadOnlyList<WorkItemInsightBucketDto> ByStatus { get; set; } = Array.Empty<WorkItemInsightBucketDto>();
    public IReadOnlyList<WorkItemInsightBucketDto> ByPriority { get; set; } = Array.Empty<WorkItemInsightBucketDto>();
}

public sealed class WorkItemInsightBucketDto
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class WorkItemActivityDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Changes { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class CreateWorkItemCommentDto
{
    public string CommentText { get; set; } = string.Empty;
    public Guid? ParentCommentId { get; set; }
}

public sealed class WorkItemCommentDto
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class WorkItemAttachmentDto
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class WorkItemAttachmentContent
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
}
