using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public interface IResourceCostProfileService
{
    Task<Result<IReadOnlyList<ResourceCostProfileDto>>> GetByResourceAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task<Result<ResourceCostProfileDto>> GetByIdAsync(Guid resourceId, Guid id, CancellationToken cancellationToken = default);
    Task<Result<ResourceCostProfileDto>> CreateAsync(CreateResourceCostProfileDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ResourceCostProfileDto>> UpdateAsync(Guid resourceId, Guid id, UpdateResourceCostProfileDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid resourceId, Guid id, string userId, CancellationToken cancellationToken = default);
}
