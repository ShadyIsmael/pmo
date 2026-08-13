using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public interface IVendorService
{
    Task<PagedResult<VendorDto>> GetPagedAsync(int page, int pageSize, string? search = null, CancellationToken cancellationToken = default);
    Task<Result<VendorDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<VendorDto>> CreateAsync(CreateVendorDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<VendorDto>> UpdateAsync(Guid id, UpdateVendorDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
