using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Domain;

public sealed class ProgramHandoverRequest : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;
    public Guid CurrentProgramManagerId { get; set; }
    public Guid NewProgramManagerId { get; set; }
    public DateTime EffectiveHandoverDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Status { get; set; } = ManagerHandoverStatus.Pending;
    public Guid RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? RespondedBy { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? SnapshotId { get; set; }
}
