using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Domain;

public enum PlanImportSessionStatus
{
    Parsed,
    NeedsReview,
    ReadyToCommit,
    Committed,
    Cancelled,
    Failed
}

/// <summary>
/// Durable staging boundary for external planning imports. Parsed source data
/// and review decisions live here until an atomic commit creates canonical
/// Planning rows.
/// </summary>
public sealed class PlanImportSession : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string SuggestedPlanName { get; set; } = string.Empty;
    public PlanImportSessionStatus Status { get; set; } = PlanImportSessionStatus.Parsed;
    public string ParsedDocumentJson { get; set; } = string.Empty;
    public string ResolutionJson { get; set; } = string.Empty;
    public string? CommitResultJson { get; set; }
    public DateTime? ParsedAt { get; set; }
    public DateTime? CommittedAt { get; set; }
    public ICollection<PlanImportIssue> Issues { get; set; } = new List<PlanImportIssue>();
}

public sealed class PlanImportIssue : BaseEntity
{
    public Guid PlanImportSessionId { get; set; }
    public PlanImportSession? PlanImportSession { get; set; }
    public string Severity { get; set; } = "Warning";
    public string Category { get; set; } = "General";
    public string EntityType { get; set; } = "Document";
    public string? SourceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool IsBlocking { get; set; }
}
