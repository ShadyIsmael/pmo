using NEXUS.SharedKernel;

namespace NEXUS.Modules.RiskManagement.Domain;

public enum RiskActionStatus
{
    Planned,
    InProgress,
    Completed,
    Cancelled
}

public enum RiskActionTimelineEntryType
{
    Comment,
    SystemChange
}

public sealed class RiskAction : BaseEntity
{
    public Guid RiskId { get; set; }
    public Risk Risk { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? OwnerResourceId { get; set; }
    public DateTime? TargetDate { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public RiskActionStatus Status { get; set; } = RiskActionStatus.Planned;
    public int SortOrder { get; set; }
    public ICollection<RiskActionUpdate> Updates { get; set; } = new List<RiskActionUpdate>();
}

public sealed class RiskActionUpdate : BaseEntity
{
    public Guid RiskActionId { get; set; }
    public RiskAction RiskAction { get; set; } = null!;
    public string UpdateText { get; set; } = string.Empty;
    public RiskActionTimelineEntryType EntryType { get; set; } = RiskActionTimelineEntryType.Comment;
    public string? ChangeDetailsJson { get; set; }
}
