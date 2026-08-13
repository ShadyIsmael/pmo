using NEXUS.SharedKernel;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Modules.Organization.Application;

public interface IBusinessUnitService
{
    Task<Result<BusinessUnit>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<BusinessUnit>> GetByOrganizationAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken = default);
}
