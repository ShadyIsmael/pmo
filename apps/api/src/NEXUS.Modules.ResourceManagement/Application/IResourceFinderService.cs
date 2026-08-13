using NEXUS.SharedKernel;
using NEXUS.Modules.ResourceManagement.Application.DTOs;

namespace NEXUS.Modules.ResourceManagement.Application;

public interface IResourceFinderService
{
    Task<PagedResult<ResourceFinderResultDto>> FindAsync(ResourceFinderQueryDto query, CancellationToken cancellationToken = default);
}
