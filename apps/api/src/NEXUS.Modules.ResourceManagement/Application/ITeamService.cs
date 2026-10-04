using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public interface ITeamService
{
    Task<PagedResult<TeamDto>> GetPagedAsync(int page, int pageSize, string? search = null, Guid? departmentId = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> GetByIdAsync(Guid id, bool includeMembers = true, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> CreateAsync(CreateTeamDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> UpdateAsync(Guid id, UpdateTeamDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> DeactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> ReactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    /// <summary>Lightweight list for selectors (Resource form, My Work filters).</summary>
    Task<IReadOnlyList<TeamLookupDto>> GetLookupAsync(bool activeOnly = true, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TeamMemberDto>>> GetMembersAsync(Guid teamId, bool currentOnly = true, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TeamMemberDto>>> AddMembersAsync(Guid teamId, AddTeamMembersDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TeamMemberDto>>> SetMembersAsync(Guid teamId, SetTeamMembersDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TeamMemberDto>>> RemoveMemberAsync(Guid teamId, Guid resourceId, RemoveTeamMemberDto dto, string userId, CancellationToken cancellationToken = default);
}

public class TeamLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; }
}
