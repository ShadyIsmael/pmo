using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkItems.Domain;

public sealed class WorkItemComment : BaseEntity
{
    public Guid WorkItemId { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string CommentText { get; set; } = string.Empty;
}
