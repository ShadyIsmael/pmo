using NEXUS.Modules.CostManagement.Application.DTOs;

namespace NEXUS.Modules.CostManagement.Application;

public interface IGlobalFinancialSummaryService
{
    Task<GlobalFinancialSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
