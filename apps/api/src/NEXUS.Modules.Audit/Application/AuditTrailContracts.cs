namespace NEXUS.Modules.Audit.Application;

/// <summary>Maps technical entity types onto the business modules shown in the audit UI.</summary>
public static class AuditModuleMap
{
    public const string ProjectManagement = "ProjectManagement";
    public const string Planning = "Planning";
    public const string WorkItems = "WorkItems";
    public const string Meetings = "Meetings";
    public const string Risk = "Risk";
    public const string Issues = "Issues";
    public const string Finance = "Finance";
    public const string Documents = "Documents";
    public const string Resources = "Resources";
    public const string TimeTracking = "TimeTracking";
    public const string Governance = "Governance";
    public const string Workflow = "Workflow";
    public const string Identity = "Identity";
    public const string Other = "Other";

    private static readonly Dictionary<string, string> ByEntityType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Project"] = ProjectManagement,
        ["ProjectMember"] = ProjectManagement,
        ["ProjectPerson"] = ProjectManagement,
        ["ProjectStakeholder"] = ProjectManagement,
        ["ProjectSnapshot"] = ProjectManagement,
        ["ProjectPhase"] = ProjectManagement,
        ["ProjectPhaseGateEvidence"] = ProjectManagement,
        ["ProjectBaselineVersion"] = ProjectManagement,
        ["ProjectChangeRequest"] = Governance,
        ["ProjectStatusReport"] = Governance,
        ["ProjectReportingConfiguration"] = Governance,
        ["ProjectHandoverRequest"] = Governance,
        ["WorkItem"] = WorkItems,
        ["WorkItemAttachment"] = WorkItems,
        ["Plan"] = Planning,
        ["PlanVersion"] = Planning,
        ["PlanActivity"] = Planning,
        ["PlanDependency"] = Planning,
        ["PlanWbsNode"] = Planning,
        ["PlanAssignment"] = Planning,
        ["PlanGridCommand"] = Planning,
        ["PlanCalendar"] = Planning,
        ["CalendarTemplate"] = Planning,
        // Retained for historical rows written against the calendar discriminator.
        ["PlanningCalendar"] = Planning,
        ["Meeting"] = Meetings,
        ["Risk"] = Risk,
        ["RiskAction"] = Risk,
        ["RiskImpactAssessment"] = Risk,
        ["RiskActionUpdate"] = Risk,
        ["Issue"] = Issues,
        ["Budget"] = Finance,
        ["ProjectBudget"] = Finance,
        ["ProjectFinanceConfiguration"] = Finance,
        ["BudgetRevision"] = Finance,
        ["CostEntry"] = Finance,
        ["PurchaseCommitment"] = Finance,
        ["Commitment"] = Finance,
        ["PaymentObligation"] = Finance,
        ["PaymentSettlement"] = Finance,
        ["ProjectPaymentRequest"] = Finance,
        ["Invoice"] = Finance,
        ["Document"] = Documents,
        ["ResourceAllocation"] = Resources,
        ["ProjectRole"] = Resources,
        ["Resource"] = Resources,
        ["ResourceCostProfile"] = Resources,
        ["Team"] = Resources,
        ["Skill"] = Resources,
        ["Vendor"] = Resources,
        ["TimeEntry"] = TimeTracking,
        ["TimerSession"] = TimeTracking,
        ["ProjectWorkflowConfiguration"] = Workflow,
        ["WorkflowTransition"] = Workflow,
        ["WorkflowManagement"] = Workflow,
        ["User"] = Identity,
        ["Role"] = Identity
    };

    public static string Resolve(string? entityType) =>
        entityType is not null && ByEntityType.TryGetValue(entityType, out var module) ? module : Other;

    /// <summary>Entity types that belong to a module, used to translate the UI module filter.</summary>
    public static IReadOnlyList<string> EntityTypesFor(string module) =>
        ByEntityType.Where(pair => string.Equals(pair.Value, module, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Key)
            .ToList();

    public static IReadOnlyList<string> All { get; } =
    [
        ProjectManagement, Planning, WorkItems, Meetings, Risk, Issues, Finance,
        Documents, Resources, TimeTracking, Governance, Workflow, Identity, Other
    ];
}

/// <summary>Server-side filter and paging options for a project audit trail.</summary>
public sealed record AuditTrailQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public string? Module { get; init; }
    public string? Action { get; init; }
    public Guid? ActorId { get; init; }
    public string? EntityType { get; init; }
    public string? Search { get; init; }
    public bool NewestFirst { get; init; } = true;
}

public sealed record AuditFieldChange(string? From, string? To);

public sealed record AuditTrailEvent(
    Guid Id,
    Guid? ProjectId,
    string EntityType,
    string Module,
    Guid EntityId,
    string Action,
    Guid? ActorId,
    string? ActorName,
    string Source,
    DateTime TimestampUtc,
    string? Description,
    string? CorrelationId,
    IReadOnlyList<string> ChangedFields,
    IReadOnlyDictionary<string, AuditFieldChange> Changes,
    string? Before,
    string? After,
    bool DetailsRestricted);

public sealed record AuditTrailPage(
    IReadOnlyList<AuditTrailEvent> Items,
    int Page,
    int PageSize,
    int Total,
    bool HasMore);

public sealed record AuditActorOption(Guid Id, string Name);

public sealed record AuditTrailFacets(
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Actions,
    IReadOnlyList<AuditActorOption> Actors,
    IReadOnlyList<string> EntityTypes,
    DateTime? CoverageStartUtc);

/// <summary>
/// Request-scoped permissions used to decide whether monetary or protected
/// module detail may be returned from the project audit trail.
/// </summary>
public sealed record AuditTrailAccess(bool IncludeFinancialDetail, IReadOnlySet<string> DetailPermissionKeys)
{
    private static readonly IReadOnlyDictionary<string, string> SensitiveEntityPermissions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Project"] = "Project.View",
            ["ProjectSnapshot"] = "Project.View",
            ["ProjectBaselineVersion"] = "Project.View",
            ["ProjectChangeRequest"] = "ChangeRequest.View",
            ["ProjectStatusReport"] = "StatusReport.View",
            ["ProjectReportingConfiguration"] = "StatusReport.View",
            ["ProjectHandoverRequest"] = "Handover.View",
            ["ProjectStakeholder"] = "Stakeholders.Read",
            ["ProjectPerson"] = "Project.View",
            ["ProjectMember"] = "Project.View",
            ["ProjectPhase"] = "Project.View",
            ["ProjectPhaseGateEvidence"] = "Document.View"
        };

    private static readonly string[] GovernancePermissionKeys =
    [
        "ChangeRequest.View",
        "StatusReport.View",
        "Handover.View",
        "Stakeholders.Read"
    ];

    public bool IncludeGovernanceDetail => GovernancePermissionKeys.Any(DetailPermissionKeys.Contains);

    public bool CanViewSensitiveDetailsFor(string entityType) =>
        !SensitiveEntityPermissions.TryGetValue(entityType, out var requiredPermission)
        || DetailPermissionKeys.Contains(requiredPermission);

    public static AuditTrailAccess Restricted { get; } =
        new(false, new HashSet<string>(StringComparer.Ordinal));
}

/// <summary>Project audit trail page, including the caller's detail entitlements.</summary>
public sealed record ProjectAuditTrailResponse(
    IReadOnlyList<AuditTrailEvent> Items,
    int Page,
    int PageSize,
    int Total,
    bool HasMore,
    bool IncludeFinancialDetail,
    bool IncludeGovernanceDetail,
    Guid ProjectPublicId,
    string ProjectName);

/// <summary>Available filter values and the first captured event in project scope.</summary>
public sealed record ProjectAuditFacetsResponse(
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Actions,
    IReadOnlyList<AuditActorOption> Actors,
    IReadOnlyList<string> EntityTypes,
    DateTime? CoverageStartUtc,
    bool IncludeFinancialDetail,
    bool IncludeGovernanceDetail,
    Guid ProjectPublicId,
    string ProjectName);
