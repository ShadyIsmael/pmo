namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public class CreateProjectHandoverRequestDto
{
    public Guid NewProjectManagerId { get; set; }
    public DateTime EffectiveHandoverDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class RejectProjectHandoverRequestDto
{
    public string RejectionReason { get; set; } = string.Empty;
}

public class ProjectHandoverRequestDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public Guid CurrentProjectManagerId { get; set; }
    public string CurrentProjectManagerName { get; set; } = string.Empty;
    public Guid NewProjectManagerId { get; set; }
    public string NewProjectManagerName { get; set; } = string.Empty;
    public DateTime EffectiveHandoverDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid RequestedBy { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public Guid? RespondedBy { get; set; }
    public string? RespondedByName { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? SnapshotId { get; set; }
    public string? SnapshotName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
