using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class ProjectChangeRequest : BaseEntity
{
    public enum ChangeTypes
    {
        Administrative,
        Baseline,
        BudgetChange
    }

    public enum Statuses
    {
        Draft,
        Pending,
        Approved,
        Rejected,
        Cancelled
    }

    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public int Number { get; set; }
    public ChangeTypes Type { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string? Justification { get; set; }
    public string? Impact { get; set; }
    // Null on historical/non-financial CRs. Immutable request-time ceiling and signed impact.
    public decimal? BudgetBeforeChange { get; set; }
    public decimal? BudgetChangeAmount { get; set; }
    public string? Attachments { get; set; }

    /// <summary>Approval strategy selected for this request and frozen when submitted.</summary>
    public string ApprovalStrategy { get; set; } = ProjectChangeRequestApprovalStrategies.AllRequired;
    /// <summary>Approver selection saved while the request is still a draft.</summary>
    public string SelectedApproverUserIdsJson { get; set; } = "[]";
    /// <summary>Canonical workflow instance for this request's lifecycle.</summary>
    public Guid? WorkflowInstanceId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public ICollection<ProjectChangeRequestApproval> ApprovalAssignments { get; set; } = new List<ProjectChangeRequestApproval>();

    public Statuses Status { get; set; } = Statuses.Draft;

    public Guid RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? DecisionNotes { get; set; }

    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public new byte[] RowVersion { get; set; } = new byte[8];

    public bool IsBaselineChange => Type == ChangeTypes.Baseline;
    public bool IsAdminChange => Type == ChangeTypes.Administrative;
    public bool IsPending => Status == Statuses.Pending;
}

public static class ProjectChangeRequestApprovalStrategies
{
    public const string AllRequired = "AllRequired";
    public const string AnyOneOptional = "AnyOneOptional";
    public const string Sequential = "Sequential";

    public static bool IsSupported(string? value) => value is not null
        && (value.Equals(AllRequired, StringComparison.OrdinalIgnoreCase)
            || value.Equals(AnyOneOptional, StringComparison.OrdinalIgnoreCase)
            || value.Equals(Sequential, StringComparison.OrdinalIgnoreCase));
}
