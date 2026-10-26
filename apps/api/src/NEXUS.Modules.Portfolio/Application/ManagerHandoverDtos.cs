namespace NEXUS.Modules.Portfolio.Application;

public sealed class CreateManagerHandoverRequestDto
{
    public Guid NewManagerId { get; set; }
    public DateTime EffectiveHandoverDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class RejectManagerHandoverRequestDto
{
    public string RejectionReason { get; set; } = string.Empty;
}

public sealed class ManagerHandoverManagerDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public IReadOnlyList<string> ManagerTypes { get; set; } = Array.Empty<string>();
}

public sealed class ManagerHandoverRequestDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid? CurrentManagerId { get; set; }
    public string CurrentManagerName { get; set; } = string.Empty;
    public Guid NewManagerId { get; set; }
    public string NewManagerName { get; set; } = string.Empty;
    public DateTime EffectiveHandoverDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string InitiationType { get; set; } = string.Empty;
    public Guid RequestedBy { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public Guid? RespondedBy { get; set; }
    public string? RespondedByName { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? SnapshotId { get; set; }
}

public sealed class ManagerHandoverCapabilitiesDto
{
    public Guid? CurrentManagerId { get; set; }
    public string? CurrentManagerName { get; set; }
    public bool CanViewRequests { get; set; }
    public bool CanInitiate { get; set; }
    public bool CanAdminInitiate { get; set; }
    public bool CanAssignInitial { get; set; }
    public bool CanApproveIncoming { get; set; }
    public bool CanCancelOwnPending { get; set; }
    public bool HasPendingRequest { get; set; }
}

public sealed class ManagerAssignmentDto
{
    public Guid Id { get; set; }
    public Guid EntityId { get; set; }
    public Guid ManagerId { get; set; }
    public string ManagerName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime? HandoverDate { get; set; }
    public string? HandoverReason { get; set; }
    public string? HandoverNotes { get; set; }
}

public sealed class ManagerSnapshotDto
{
    public Guid Id { get; set; }
    public Guid EntityId { get; set; }
    public int SnapshotNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime SnapshotDate { get; set; }
    public DateTime EffectiveEndDate { get; set; }
    public Guid? PreviousManagerId { get; set; }
    public string PreviousManagerName { get; set; } = string.Empty;
    public Guid NewManagerId { get; set; }
    public string NewManagerName { get; set; } = string.Empty;
    public string HandoverReason { get; set; } = string.Empty;
    public string? HandoverNotes { get; set; }
    public string? Data { get; set; }
}
