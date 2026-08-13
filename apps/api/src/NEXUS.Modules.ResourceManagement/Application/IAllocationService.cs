using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public interface IAllocationService
{
    Task<PagedResult<AllocationDto>> GetByResourceAsync(Guid resourceId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<AllocationDto>> GetByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<List<AllocationDto>>> GetConflictsAsync(Guid resourceId, DateTime startDate, DateTime endDate, decimal? excludeAllocationId, CancellationToken cancellationToken = default);
    Task<Result<AllocationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<AllocationDto>> CreateAsync(CreateAllocationDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<AllocationDto>> UpdateAsync(Guid id, UpdateAllocationDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<PagedResult<AllocationDto>> GetPagedAsync(int page, int pageSize, Guid? resourceId = null, Guid? projectId = null, string? status = null, CancellationToken cancellationToken = default);
}
