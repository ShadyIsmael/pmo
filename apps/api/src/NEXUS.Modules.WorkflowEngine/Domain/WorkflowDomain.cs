namespace NEXUS.Modules.WorkflowEngine.Domain;

/// <summary>
/// The business domain is the selector boundary for project workflow adoption.
/// EntityType remains the canonical engine adapter key (for example WorkItem),
/// while Domain prevents a workflow for one business area from being assigned to
/// another one.
/// </summary>
public static class WorkflowDomains
{
    public const string Task = "Task";
    public const string Risk = "Risk";
    public const string Finance = "Finance";
    public const string Reporting = "Reporting";
    public const string ChangeRequest = "ChangeRequest";

    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Task, Risk, Finance, Reporting, ChangeRequest
    };

    public static bool IsSupported(string? value) => !string.IsNullOrWhiteSpace(value) && Supported.Contains(value.Trim());

    public static string Normalize(string value) => value.Trim().ToLowerInvariant() switch
    {
        "task" or "tasks" or "workitem" or "workitems" => Task,
        "risk" or "risks" => Risk,
        "finance" or "financial" or "budgetrevision" => Finance,
        "report" or "reports" or "statusreport" or "statusreports" or "reporting" => Reporting,
        "changerequest" or "changerequests" or "change request" or "change requests" => ChangeRequest,
        _ => value.Trim()
    };

    public static string EntityTypeFor(string domain) => Normalize(domain) switch
    {
        Task => "WorkItem",
        Risk => "Risk",
        Finance => "BudgetRevision",
        Reporting => "ProjectStatusReport",
        ChangeRequest => "ProjectChangeRequest",
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unsupported workflow domain.")
    };
}
