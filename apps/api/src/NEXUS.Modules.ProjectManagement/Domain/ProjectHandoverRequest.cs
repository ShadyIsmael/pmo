using NEXUS.SharedKernel;
using NEXUS.Modules.Portfolio.Domain;

namespace NEXUS.Modules.ProjectManagement.Domain;

/// <summary>
/// A project manager handover that requires the selected new manager to explicitly accept or
/// reject before any snapshot or assignment change is applied. Only one pending or approved
/// future-dated request is allowed per project. Accepted, rejected and cancelled requests are immutable.
/// </summary>
public class ProjectHandoverRequest : BaseEntity
{
    public static class Statuses
    {
        public const string Pending = "Pending";
        public const string ApprovedScheduled = "ApprovedScheduled";
        public const string Accepted = "Accepted";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";
    }

    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? CurrentProjectManagerId { get; set; }
    public Guid NewProjectManagerId { get; set; }
    public string InitiationType { get; set; } = ManagerHandoverInitiationType.ManagerInitiated;

    public DateTime EffectiveHandoverDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public string Status { get; set; } = Statuses.Pending;

    public Guid RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }

    public Guid? RespondedBy { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? SnapshotId { get; set; }
    public ProjectSnapshot? Snapshot { get; set; }
}
