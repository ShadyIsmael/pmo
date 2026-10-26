using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public sealed class ProjectChangeRequestApproval : BaseEntity
{
    public Guid ProjectChangeRequestId { get; set; }
    public ProjectChangeRequest ProjectChangeRequest { get; set; } = null!;
    public Guid WorkflowInstanceId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid OriginalApproverUserId { get; set; }
    public string OriginalApproverNameSnapshot { get; set; } = string.Empty;
    public Guid AssignedToUserId { get; set; }
    public string ApproverNameSnapshot { get; set; } = string.Empty;
    public string RoleSnapshot { get; set; } = string.Empty;
    public bool IsSponsor { get; set; }
    public bool IsMandatory { get; set; }
    public int Sequence { get; set; }
    public string Status { get; set; } = Statuses.Pending;
    public DateTime? DecidedAt { get; set; }
    public Guid? DecisionActorId { get; set; }
    public string? DecisionComment { get; set; }

    public static class Statuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Satisfied = "Satisfied";
        public const string Reassigned = "Reassigned";
        public const string Cancelled = "Cancelled";
    }
}

public sealed class ProjectChangeRequestApprovalHistory : BaseEntity
{
    public Guid ProjectChangeRequestApprovalId { get; set; }
    public ProjectChangeRequestApproval Assignment { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public Guid FromApproverUserId { get; set; }
    public string FromApproverNameSnapshot { get; set; } = string.Empty;
    public Guid ToApproverUserId { get; set; }
    public string ToApproverNameSnapshot { get; set; } = string.Empty;
    public Guid ChangedByUserId { get; set; }
    public string ChangedByNameSnapshot { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
