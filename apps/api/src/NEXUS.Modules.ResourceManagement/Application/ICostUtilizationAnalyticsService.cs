using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public interface ICostUtilizationAnalyticsService
{
    Task<Result<CostUtilizationReportDto>> GetAsync(
        CostUtilizationFilterDto filter,
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result<CostUtilizationReportDto>> GetForExportAsync(
        CostUtilizationFilterDto filter,
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
