using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public interface IResourceCostProfileService
{
    Task<Result<IReadOnlyList<ResourceCostProfileDto>>> GetByResourceAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task<Result<ResourceCostProfileDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ResourceCostProfileDto>> CreateAsync(CreateResourceCostProfileDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ResourceCostProfileDto>> UpdateAsync(Guid id, UpdateResourceCostProfileDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
