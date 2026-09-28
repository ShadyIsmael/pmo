using NEXUS.SharedKernel;

namespace NEXUS.Api.ProgramManagement;

public interface IProgramManagementService
{
    Task<PagedResult<ProgramManagementListItemDto>> GetProgramsAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    Task<Result<ProgramManagementOverviewDto>> GetOverviewAsync(
        Guid programId,
        int projectsPage,
        int projectsPageSize,
        CancellationToken cancellationToken = default);
}
