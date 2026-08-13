using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public interface IResourceService
{
    Task<Result<ResourceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ResourceDto>> GetPagedAsync(ResourceFilterDto filter, CancellationToken cancellationToken = default);
    Task<Result<ResourceDto>> CreateAsync(CreateResourceDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ResourceDto>> UpdateAsync(Guid id, UpdateResourceDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
