using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public static class ProjectAnnualBudgetStatuses
{
    public const string Draft = "Draft";
    public const string Approved = "Approved";
}

/// <summary>
/// Project-owned annual budget allocation. New Payment Requests reserve against this
/// approved fiscal-year amount; Project.Budget is a separate lifetime ceiling and is
/// never used as an annual authorization fallback.
/// </summary>
public sealed class ProjectAnnualBudget : BaseEntity
{
    public Guid ProjectId { get; set; }
    public int FiscalYear { get; set; }
    public DateOnly FiscalYearStartDate { get; set; }
    public DateOnly FiscalYearEndDate { get; set; }
    public string FinancialReference { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal NewApprovedFunding { get; set; }

    /// <summary>
    /// Optional pre-submission approval plan for this Draft only. Null means that
    /// the current Project Settings defaults remain authoritative.
    /// </summary>
    public string? DraftApprovalConfigurationJson { get; set; }

    /// <summary>Reserved for a separately approved carry-forward workflow; Sprint 1 keeps it at zero.</summary>
    public decimal ApprovedCarryForward { get; set; }

    /// <summary>Subset of ApprovedCarryForward reserved for carried Payment Request obligations.</summary>
    public decimal ApprovedReservedCarryForward { get; set; }

    public decimal ApprovedAdjustments { get; set; }
    public string Status { get; set; } = ProjectAnnualBudgetStatuses.Draft;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ClosedByClosingId { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public Guid? ClosedBy { get; set; }

    public decimal ApprovedFreeCarryForward => ApprovedCarryForward - ApprovedReservedCarryForward;

    public decimal TotalAnnualBudget => NewApprovedFunding + ApprovedCarryForward + ApprovedAdjustments;
}
