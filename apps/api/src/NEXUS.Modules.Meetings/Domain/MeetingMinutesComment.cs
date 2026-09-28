using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Domain;

public class MeetingMinutesComment : BaseEntity
{
    public Guid MeetingMinutesId { get; set; }
    public MeetingMinutes MeetingMinutes { get; set; } = null!;
    public Guid? ParentCommentId { get; set; }
    public MeetingMinutesComment? ParentComment { get; set; }
    public string CommentText { get; set; } = string.Empty;
}
