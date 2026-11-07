using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

/// <summary>
/// Project Settings defaults used only when a Project Annual Budget review is
/// submitted. Each submission keeps its plan in its audit event and assignments.
/// </summary>
public sealed class ProjectAnnualBudgetApprovalConfiguration : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string ApprovalMode { get; set; } = string.Empty;
    public ICollection<ProjectAnnualBudgetApprovalApprover> Approvers { get; set; } =
        new List<ProjectAnnualBudgetApprovalApprover>();
}

/// <summary>One configured, mandatory reviewer in the Project Annual Budget defaults.</summary>
public sealed class ProjectAnnualBudgetApprovalApprover : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid ConfigurationId { get; set; }
    public Guid ApproverUserId { get; set; }
    public int? Position { get; set; }
    public ProjectAnnualBudgetApprovalConfiguration? Configuration { get; set; }
}
