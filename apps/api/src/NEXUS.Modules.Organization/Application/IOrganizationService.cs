using NEXUS.SharedKernel;
namespace NEXUS.Modules.Organization.Application;

public interface IOrganizationService
{
    Task<Result<OrganizationResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<OrganizationResponseDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationProfileDto>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task<OrganizationProfileUpdateResult> UpdateProfileAsync(Guid id, UpdateOrganizationProfileRequest request, CancellationToken cancellationToken = default);
}
