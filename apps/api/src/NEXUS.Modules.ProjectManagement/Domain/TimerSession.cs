using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

/// <summary>Server-owned live timer state; actual work is materialised into TimeEntry.</summary>
public sealed class TimerSession : BaseEntity
{
    public const string Running = "Running";
    public const string OnBreak = "OnBreak";
    public const string Paused = "Paused";
    public const string IdlePaused = "IdlePaused";
    public const string AwaitingFinalization = "AwaitingFinalization";
    public const string Finished = "Finished";
    public const string Cancelled = "Cancelled";

    public Guid ResourceId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
    public string Status { get; set; } = Running;
    public ICollection<TimerSegment> Segments { get; set; } = new List<TimerSegment>();
}
