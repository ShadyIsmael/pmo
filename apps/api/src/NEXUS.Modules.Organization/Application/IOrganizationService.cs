using NEXUS.SharedKernel;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;

namespace NEXUS.Modules.Organization.Application;

public interface IOrganizationService
{
    Task<Result<OrganizationEntity>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<OrganizationEntity>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
