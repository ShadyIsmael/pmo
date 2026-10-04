using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Persistence;

public sealed class WorkItemAssigneeLookup : IWorkItemAssigneeLookup
{
    private readonly DbContext _context;

    public WorkItemAssigneeLookup(DbContext context) => _context = context;

    public async Task<IReadOnlyList<WorkItemAssigneeReference>> GetEligibleAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var candidates = await (
            from member in _context.Set<ProjectMember>().AsNoTracking()
            join resource in _context.Set<Resource>().AsNoTracking() on member.ResourceId equals resource.Id
            join user in _context.Set<User>().AsNoTracking() on resource.UserId equals user.Id
            where member.ProjectId == projectId
                && member.Status == "Active"
                && resource.IsActive
                && resource.Status == "Active"
                && resource.UserId.HasValue
                && user.IsActive
            orderby resource.Name
            select new WorkItemAssigneeReference(resource.Id, resource.Name, resource.Email, resource.JobTitle)
        ).ToListAsync(cancellationToken);

        return candidates
            .GroupBy(candidate => candidate.ResourceId)
            .Select(group => group.First())
            .ToList();
    }

    public async Task<IReadOnlyList<WorkItemTeamReference>> GetEligibleTeamsAsync(CancellationToken cancellationToken = default)
        => await _context.Set<Team>().AsNoTracking()
            .Where(team => team.IsActive && !team.IsDeleted)
            .OrderBy(team => team.Name)
            .Select(team => new WorkItemTeamReference(team.Id, team.Name, team.Code))
            .ToListAsync(cancellationToken);

    public async Task<bool> IsActiveProjectMemberAsync(Guid projectId, Guid resourceId, CancellationToken cancellationToken = default)
    {
        var candidates = await GetEligibleAsync(projectId, cancellationToken);
        return candidates.Any(candidate => candidate.ResourceId == resourceId);
    }

    public Task<bool> IsActiveTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
        => _context.Set<Team>().AnyAsync(team => team.Id == teamId && team.IsActive && !team.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetActiveTeamIdsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await (
            from resource in _context.Set<Resource>().AsNoTracking()
            join member in _context.Set<TeamMember>().AsNoTracking() on resource.Id equals member.ResourceId
            join team in _context.Set<Team>().AsNoTracking() on member.TeamId equals team.Id
            where resource.UserId == userId && resource.IsActive && !resource.IsDeleted
                && member.IsActive && !member.IsDeleted
                && (member.EffectiveTo == null || member.EffectiveTo > DateTime.UtcNow)
                && team.IsActive && !team.IsDeleted
            select team.Id
        ).Distinct().ToListAsync(cancellationToken);

    public Task<bool> IsCurrentTeamMemberAsync(Guid teamId, Guid resourceId, CancellationToken cancellationToken = default)
        => _context.Set<TeamMember>().AnyAsync(member => member.TeamId == teamId && member.ResourceId == resourceId && member.IsActive && !member.IsDeleted && (member.EffectiveTo == null || member.EffectiveTo > DateTime.UtcNow), cancellationToken);
}
