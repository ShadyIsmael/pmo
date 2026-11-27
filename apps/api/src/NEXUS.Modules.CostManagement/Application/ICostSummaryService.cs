using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

/// <summary>
/// Read-only cost summary across the caller's management scope. Every amount is resolved
/// from the authoritative source for the Project currency; no cross-currency aggregation
/// and no substitution of one measure for another is performed.
/// </summary>
public interface ICostSummaryService
{
    Task<GlobalFinancialSummaryDto> GetAsync(CancellationToken cancellationToken = default);
}