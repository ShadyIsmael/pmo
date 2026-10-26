using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public class CreateProjectChangeRequestDto
{
    public Guid ProjectId { get; set; }
    public ProjectChangeRequest.ChangeTypes Type { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Justification { get; set; }
    public string? Impact { get; set; }
    public decimal? BudgetChangeAmount { get; set; }
    public string? Attachments { get; set; }
    public string ApprovalStrategy { get; set; } = ProjectChangeRequestApprovalStrategies.AllRequired;
    public List<Guid> ApproverUserIds { get; set; } = [];
    public byte[]? RowVersion { get; set; }
}

public sealed class UpdateProjectChangeRequestDto : CreateProjectChangeRequestDto { }

public sealed class SubmitProjectChangeRequestDto
{
    public byte[]? RowVersion { get; set; }
}

public sealed class ReassignProjectChangeRequestApprovalDto
{
    public Guid NewApproverUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed record ProjectChangeRequestApproverDto(
    Guid UserId,
    string FullName,
    string ProjectRole,
    bool IsSponsor,
    bool IsMandatory);

public sealed record ProjectChangeRequestApprovalOptionsDto(
    IReadOnlyList<ProjectChangeRequestApproverDto> Approvers,
    IReadOnlyList<string> AllowedStrategies,
    Guid? SponsorUserId,
    string? PolicyMessage);

public sealed record ProjectChangeRequestApprovalDto(
    Guid Id,
    Guid ApproverUserId,
    string ApproverName,
    Guid OriginalApproverUserId,
    string OriginalApproverName,
    string ProjectRole,
    bool IsSponsor,
    bool IsMandatory,
    int Sequence,
    string Status,
    DateTime? DecidedAt,
    Guid? DecisionActorId,
    string? DecisionComment,
    IReadOnlyList<ProjectChangeRequestApprovalHistoryDto> ReassignmentHistory);

public sealed record ProjectChangeRequestApprovalHistoryDto(
    Guid FromApproverUserId,
    string FromApproverName,
    Guid ToApproverUserId,
    string ToApproverName,
    Guid ChangedByUserId,
    string ChangedByName,
    string Reason,
    DateTime ChangedAt);

public sealed record PendingProjectChangeApprovalDto(
    Guid Id,
    string Status,
    string ProjectName,
    int Number,
    ProjectChangeRequest.ChangeTypes Type,
    string Reason,
    string RequestedByName,
    DateTime RequestedAt,
    int ApprovalCount,
    int RequiredApprovalCount);

public sealed record ProjectChangeRequestDecisionDto(Guid Id, int Number, string Status);

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
    public decimal? BudgetBeforeChange { get; set; }
    public decimal? BudgetChangeAmount { get; set; }
    public decimal? ProposedProjectBudget { get; set; }
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectPublicId { get; set; }
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
    public string ApprovalStrategy { get; set; } = ProjectChangeRequestApprovalStrategies.AllRequired;
    public Guid[] SelectedApproverUserIds { get; set; } = [];
    public DateTime? SubmittedAt { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public string[] PendingWith { get; set; } = [];
    public int ApprovalCount { get; set; }
    public int RequiredApprovalCount { get; set; }
    public bool RequiresApprovalSetup { get; set; }
    public List<ProjectChangeRequestApprovalDto> Approvals { get; set; } = [];
    public byte[] RowVersion { get; set; } = [];
}
