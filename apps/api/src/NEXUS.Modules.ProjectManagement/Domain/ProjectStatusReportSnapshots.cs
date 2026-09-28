using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class ProjectStatusReportRiskSnapshot : BaseEntity
{
    public Guid ProjectStatusReportId { get; set; }
    public ProjectStatusReport ProjectStatusReport { get; set; } = null!;
    public Guid SourceRiskId { get; set; }
    public int Rank { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Impact { get; set; }
    public string? Likelihood { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string RiskStatus { get; set; } = string.Empty;
    public decimal? PotentialCost { get; set; }
    public decimal? PotentialDelay { get; set; }
    public string? RiskResponse { get; set; }
    public Guid? OwnerId { get; set; }
    public string? OwnerNameSnapshot { get; set; }
    public DateTime? IdentifiedDate { get; set; }
    public DateTime? ClosedDate { get; set; }
}

public class ProjectStatusReportIssueSnapshot : BaseEntity
{
    public Guid ProjectStatusReportId { get; set; }
    public ProjectStatusReport ProjectStatusReport { get; set; } = null!;
    public Guid SourceIssueId { get; set; }
    public int Rank { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string IssueStatus { get; set; } = string.Empty;
    public Guid? AssigneeId { get; set; }
    public string? AssigneeNameSnapshot { get; set; }
    public DateTime? IdentifiedDate { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? Resolution { get; set; }
}

public class ProjectStatusReportChangeRequestSnapshot : BaseEntity
{
    public Guid ProjectStatusReportId { get; set; }
    public ProjectStatusReport ProjectStatusReport { get; set; } = null!;
    public Guid SourceChangeRequestId { get; set; }
    public int Rank { get; set; }
    public int Number { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Justification { get; set; }
    public string? Impact { get; set; }
    public string ChangeRequestStatus { get; set; } = string.Empty;
    public Guid RequestedBy { get; set; }
    public string? RequestedByNameSnapshot { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? DecisionNotes { get; set; }
}
