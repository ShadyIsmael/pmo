namespace NEXUS.Api.Finance;

/// <summary>
/// Minimal, assignment-filtered summary for the authenticated user's actionable
/// Annual Budget review steps. ProjectPublicId is safe for project navigation;
/// BudgetId is the existing opaque Annual Budget identifier used by its review API.
/// </summary>
public sealed record ProjectAnnualBudgetPendingApprovalDto(
    Guid RequestId,
    Guid BudgetId,
    Guid ProjectPublicId,
    string ProjectName,
    string ProjectCode,
    int FiscalYear,
    string FinancialReference,
    decimal Amount,
    string Currency,
    string ApprovalMode,
    DateTime RequestedAtUtc,
    DateTime ActionableAtUtc,
    string? RequestedByName);
