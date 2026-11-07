namespace NEXUS.Modules.CostManagement.Application.DTOs;

public sealed record ProjectFinancialFoundationDto(
    Guid ProjectId,
    string? ProjectCurrency,
    decimal? ApprovedBudget,
    decimal? ApprovedPaymentRequestAllocations,
    decimal? AvailableBudget,
    decimal? ActualPaid,
    decimal? OutstandingPaymentBalance,
    string? FinanceApprovalMode,
    string RowVersion);

public sealed record UpdateProjectFinanceApprovalConfigurationDto(
    string? Mode,
    string RowVersion);

public static class FinanceApprovalModes
{
    public const string Sequential = "Sequential";
    public const string Parallel = "Parallel";
    public const string Both = "Both";

    public static bool IsValid(string? value) => value is null or Sequential or Parallel or Both;
}
