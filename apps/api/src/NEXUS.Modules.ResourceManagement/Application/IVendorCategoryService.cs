using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public interface IVendorCategoryService
{
    Task<Result<IReadOnlyList<LookupDto>>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<LookupDto>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<LookupDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<LookupDto>> CreateAsync(CreateLookupDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<LookupDto>> UpdateAsync(Guid id, UpdateLookupDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
