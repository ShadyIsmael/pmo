using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain.Entities;

/// <summary>
/// A working draft of a project plan. Once an approved baseline exists, further
/// (not-yet-approved) plan edits are persisted here as a separate, isolated
/// version — never by mutating the approved baseline itself.
/// </summary>
public class ProjectPlanDraft : BaseEntity
{
    /// <summary>Lifecycle of a working plan draft.</summary>
    public enum DraftStatus
    {
        Draft,
        Submitted,
        Rejected,
        Applied
    }

    public Guid ProjectId { get; set; }
    public virtual Project? Project { get; set; }

    /// <summary>The approved baseline this draft was derived from.</summary>
    public Guid SourceBaselineVersionId { get; set; }
    public virtual ProjectBaselineVersion? SourceBaselineVersion { get; set; }

    public DraftStatus Status { get; set; } = DraftStatus.Draft;

    /// <summary>
    /// Whether this draft is the single live working version for its tenant and
    /// project. Set to false when a draft leaves the working set (superseded by a
    /// newer draft, or terminal). A filtered unique index enforces at most one
    /// active draft per tenant/project.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The change request that submitted this draft, once one has been raised.</summary>
    public Guid? SubmittedChangeRequestId { get; set; }
    public virtual ProjectChangeRequest? SubmittedChangeRequest { get; set; }
}