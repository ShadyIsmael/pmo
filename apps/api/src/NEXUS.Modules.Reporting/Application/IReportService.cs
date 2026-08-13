using NEXUS.SharedKernel;
using NEXUS.Modules.Reporting.Domain;

namespace NEXUS.Modules.Reporting.Application;

public interface IReportService
{
    Task<Result<ReportDefinition>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ReportDefinition>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
