namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public class ProjectBaselineVersionDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int VersionNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCurrent { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? SourceChangeRequestId { get; set; }
    public Guid? BaselineSnapshotId { get; set; }
}