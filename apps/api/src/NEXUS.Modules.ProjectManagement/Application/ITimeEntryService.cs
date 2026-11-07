using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface ITimeEntryService
{
    Task<Result<TimeEntryDto>> CreateManualAsync(CreateTimeEntryDto dto, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TimeEntryDto>> UpdateManualAsync(Guid id, UpdateTimeEntryDto dto, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TimeEntryDto>> GetByIdAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<TimeEntryDto>>> ListAsync(TimeEntryFilterDto filter, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TimeEntrySummaryDto>> GetSummaryAsync(TimeEntryFilterDto filter, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
}
