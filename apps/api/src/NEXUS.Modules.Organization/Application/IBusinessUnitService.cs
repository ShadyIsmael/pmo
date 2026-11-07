using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Application;

public interface IBusinessUnitService
{
    Task<Result<BusinessUnitResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<BusinessUnitResponseDto>> GetByOrganizationAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken = default);
}
