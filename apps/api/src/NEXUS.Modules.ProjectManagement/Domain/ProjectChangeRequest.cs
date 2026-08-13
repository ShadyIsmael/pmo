using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class ProjectChangeRequest : BaseEntity
{
    public enum ChangeTypes
    {
        Administrative,
        Baseline
    }

    public enum Statuses
    {
        Pending,
        Approved,
        Rejected,
        Cancelled
    }

    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public int Number { get; set; }
    public ChangeTypes Type { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string? Justification { get; set; }
    public string? Impact { get; set; }
    public string? Attachments { get; set; }

    public Statuses Status { get; set; } = Statuses.Pending;

    public Guid RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? DecisionNotes { get; set; }

    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public new byte[] RowVersion { get; set; } = new byte[8];

    public bool IsBaselineChange => Type == ChangeTypes.Baseline;
    public bool IsAdminChange => Type == ChangeTypes.Administrative;
    public bool IsPending => Status == Statuses.Pending;
}