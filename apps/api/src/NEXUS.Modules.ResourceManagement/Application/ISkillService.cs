using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public interface ISkillService
{
    Task<PagedResult<SkillDto>> GetPagedAsync(int page, int pageSize, string? search = null, CancellationToken cancellationToken = default);
    Task<Result<SkillDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<SkillDto>> CreateAsync(CreateSkillDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<SkillDto>> UpdateAsync(Guid id, UpdateSkillDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
