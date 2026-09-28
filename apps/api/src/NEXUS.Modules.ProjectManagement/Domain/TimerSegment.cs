using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public sealed class TimerSegment : BaseEntity
{
    public const string Work = "Work";
    public const string Break = "Break";
    public const string Pause = "Pause";
    public const string Idle = "Idle";

    public Guid TimerSessionId { get; set; }
    public string SegmentType { get; set; } = Work;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public TimerSession? TimerSession { get; set; }
}
