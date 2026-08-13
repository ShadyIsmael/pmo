using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public class CreateProjectChangeRequestDto
{
    public Guid ProjectId { get; set; }
    public ProjectChangeRequest.ChangeTypes Type { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Justification { get; set; }
    public string? Impact { get; set; }
    public string? Attachments { get; set; }
}

public class ApproveProjectChangeRequestDto
{
    public string? DecisionNotes { get; set; }
}

public class RejectProjectChangeRequestDto
{
    public string DecisionNotes { get; set; } = string.Empty;
}

public class ProjectChangeRequestDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int Number { get; set; }
    public ProjectChangeRequest.ChangeTypes Type { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Justification { get; set; }
    public string? Impact { get; set; }
    public string? Attachments { get; set; }
    public ProjectChangeRequest.Statuses Status { get; set; }
    public Guid RequestedBy { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? DecisionNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}