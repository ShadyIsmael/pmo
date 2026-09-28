namespace NEXUS.Modules.ProjectPlanning.Domain;

/// <summary>
/// The version container type for a <see cref="Plan"/>. A Working version is the
/// editable draft; a Baseline is an immutable snapshot of a point-in-time plan.
/// </summary>
public enum PlanVersionType
{
    Working,
    Baseline
}

/// <summary>
/// Lifecycle status of a <see cref="PlanVersion"/>. Working versions are Active
/// (editable); Baseline versions are Approved (immutable) or Superseded. The
/// approval-path statuses (PendingApproval/Rejected) support the deferred CR flow.
/// </summary>
public enum PlanVersionStatus
{
    Active,
    PendingApproval,
    Rejected,
    Approved,
    Superseded
}

/// <summary>
/// Type of a <see cref="PlanActivity"/>. Milestones are zero-duration activities.
/// </summary>
public enum PlanActivityType
{
    Task,
    Milestone
}

/// <summary>Controls whether an activity's dates are calculated by auto scheduling.</summary>
public enum PlanSchedulingMode
{
    Automatic,
    Manual
}

/// <summary>Rule applied to an automatically scheduled activity's dates.</summary>
public enum PlanConstraintType
{
    AsSoonAsPossible,
    AsLateAsPossible,
    MustStartOn,
    MustFinishOn,
    StartNoEarlierThan,
    StartNoLaterThan,
    FinishNoEarlierThan,
    FinishNoLaterThan
}

/// <summary>
/// Relationship type of a <see cref="PlanDependency"/> between two activities.
/// </summary>
public enum PlanDependencyType
{
    FinishToFinish,
    StartToStart,
    FinishToStart,
    StartToFinish
}

/// <summary>
/// Status of a <see cref="Plan"/>.
/// </summary>
public enum PlanStatus
{
    Draft,
    Planned,
    InProgress,
    Completed,
    OnHold
}

/// <summary>
/// First-class, extensible type of a <see cref="Plan"/> (ADR-007). Drives
/// behaviour, governing rules, and UI templates.
/// </summary>
public enum PlanType
{
    Execution,
    Recovery,
    Tender,
    Construction,
    Shutdown,
    Scenario,
    Custom
}

/// <summary>
/// Approved semantic text colors for Planning row display. These are tokens,
/// rather than arbitrary CSS values, so the table and Gantt can render them
/// consistently across themes.
/// </summary>
public enum PlanTextColorToken
{
    Slate,
    Blue,
    Teal,
    Green,
    Amber,
    Red,
    Violet
}

/// <summary>Approved semantic highlight colors for Planning row display.</summary>
public enum PlanHighlightColorToken
{
    Yellow,
    Blue,
    Green,
    Orange,
    Pink
}

/// <summary>Limited, accessible font-size choices for Planning rows.</summary>
public enum PlanFontSizeToken
{
    Small,
    Normal,
    Large,
    ExtraLarge
}
