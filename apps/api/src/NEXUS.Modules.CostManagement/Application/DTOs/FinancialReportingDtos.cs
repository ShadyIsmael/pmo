namespace NEXUS.Modules.CostManagement.Application.DTOs;

/// <summary>
/// Consolidated project financial reporting foundation.
/// All amounts are authoritative for the project currency; a null amount means
/// the measure is unavailable and must not be shown as zero.
/// </summary>
public sealed class ProjectFinancialReportingDto
{
    public Guid ProjectId { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;

    /// <summary>Lifetime financial ceiling from Project.Budget (approved).</summary>
    public decimal? LifetimeCeiling { get; init; }

    /// <summary>Sum of Approved Annual Budgets within the reporting scope.</summary>
    public decimal? AnnualApprovedBudget { get; init; }

    /// <summary>Active Planned Payment lifetime reservations (ReservedAmount - ReleasedAmount - Settled).</summary>
    public decimal LifetimeReserved { get; init; }

    /// <summary>Outstanding commitments from active Payment Obligations and Planned Payment installments.</summary>
    public decimal OutstandingCommitments { get; init; }

    /// <summary>Confirmed PaymentSettlement totals across both ownership rails in project currency.</summary>
    public decimal? GrossActualPaid { get; init; }

    /// <summary>Executed refund and correction adjustments in project currency (negative for refunds).</summary>
    public decimal? ConfirmedRefundsAndCorrections { get; init; }

    /// <summary>Gross Actual Paid plus executed adjustments. Null when Gross Actual Paid is unavailable.</summary>
    public decimal? NetActualPaid { get; init; }

    /// <summary>LifetimeCeiling minus LifetimeConsumed minus LifetimeReserved.</summary>
    public decimal? AvailableLifetimeBudget { get; init; }

    /// <summary>Annual Budget utilization: EffectiveActiveAllocations / TotalAnnualBudget * 100.</summary>
    public decimal? AnnualBudgetUtilizationPercentage { get; init; }

    /// <summary>Budget Line utilization: ReservedAmount / AuthorizedLimit * 100 per line.</summary>
    public IReadOnlyList<BudgetLineUtilizationDto> BudgetLineUtilizations { get; init; } = [];

    /// <summary>Data quality metadata for transparent reporting.</summary>
    public FinancialReportingDataQualityDto DataQuality { get; init; } = new();
}

public sealed class BudgetLineUtilizationDto
{
    public Guid BudgetLineId { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal AuthorizedLimit { get; init; }
    public decimal ReservedAmount { get; init; }
    public decimal UtilizationPercentage { get; init; }
    public bool HasCurrencyMismatch { get; init; }
}

public sealed class FinancialReportingDataQualityDto
{
    public bool HasUnresolvedCurrency { get; init; }
    public bool HasCurrencyMismatch { get; init; }
    public bool ProjectCurrencyUnavailable { get; init; }
    public bool HasMissingFinancialReferences { get; init; }
    public bool HasIncompleteSettlementHistory { get; init; }
    public bool HasMissingAnnualBudgetAllocations { get; init; }
    public IReadOnlyList<string> Details { get; init; } = [];

    public bool HasAnyGap => HasUnresolvedCurrency || HasCurrencyMismatch || ProjectCurrencyUnavailable
        || HasMissingFinancialReferences || HasIncompleteSettlementHistory || HasMissingAnnualBudgetAllocations;

    public string OverallStatus => !HasAnyGap ? "Available"
        : ProjectCurrencyUnavailable ? "Unavailable"
        : HasUnresolvedCurrency || HasCurrencyMismatch ? "Partial"
        : "Partial";
}