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
        var today = DateTime.UtcNow.Date;
        var directCandidates = await (
            from member in _context.Set<ProjectMember>().AsNoTracking()
            join resource in _context.Set<Resource>().AsNoTracking() on member.ResourceId equals (Guid?)resource.Id
            join user in _context.Set<User>().AsNoTracking()
                on resource.UserId equals (Guid?)user.Id into linkedUsers
            from user in linkedUsers.DefaultIfEmpty()
            where member.ProjectId == projectId
                && member.Status == "Active"
                && !member.IsDeleted
                && member.ResourceId.HasValue
                && member.StartDate.Date <= today && member.EndDate.Date >= today
                && resource.IsActive && resource.Status == "Active" && !resource.IsDeleted
                // Work Items are assigned to tenant-scoped Resources, not system User IDs.
                // An unlinked Resource can still be an eligible project assignee; when a
                // Resource is linked to a system account, keep inactive/missing accounts out.
                && (!resource.UserId.HasValue
                    || resource.UserId.Value == Guid.Empty
                    || (user != null && user.IsActive && !user.IsDeleted))
            orderby resource.Name
            select new WorkItemAssigneeReference(
                resource.Id,
                resource.Name,
                resource.UserId.HasValue && resource.UserId.Value != Guid.Empty && user != null ? resource.Email : null,
                resource.JobTitle)
        ).ToListAsync(cancellationToken);

        var groupCandidates = await (
            from assignment in _context.Set<ProjectMember>().AsNoTracking()
            join teamMember in _context.Set<TeamMember>().AsNoTracking() on assignment.TeamId equals teamMember.TeamId
            join team in _context.Set<Team>().AsNoTracking() on teamMember.TeamId equals team.Id
            join resource in _context.Set<Resource>().AsNoTracking() on teamMember.ResourceId equals resource.Id
            join user in _context.Set<User>().AsNoTracking()
                on resource.UserId equals (Guid?)user.Id into linkedUsers
            from user in linkedUsers.DefaultIfEmpty()
            where assignment.ProjectId == projectId && assignment.Status == "Active"
                && assignment.TeamId.HasValue && !assignment.IsDeleted
                && assignment.StartDate.Date <= today && assignment.EndDate.Date >= today
                && team.IsActive && !team.IsDeleted
                && teamMember.IsActive && !teamMember.IsDeleted
                && teamMember.EffectiveFrom.Date <= today
                && (!teamMember.EffectiveTo.HasValue || teamMember.EffectiveTo.GetValueOrDefault().Date >= today)
                && resource.IsActive && resource.Status == "Active" && !resource.IsDeleted
                && (!resource.UserId.HasValue || resource.UserId.GetValueOrDefault() == Guid.Empty || (user != null && user.IsActive && !user.IsDeleted))
            orderby resource.Name
            select new WorkItemAssigneeReference(
                resource.Id,
                resource.Name,
                resource.UserId.HasValue && resource.UserId.GetValueOrDefault() != Guid.Empty && user != null ? resource.Email : null,
                resource.JobTitle)
        ).ToListAsync(cancellationToken);

        return directCandidates.Concat(groupCandidates)
            .GroupBy(candidate => candidate.ResourceId)
            .Select(group => group.First())
            .ToList();
    }

    public async Task<IReadOnlyList<WorkItemTeamReference>> GetEligibleTeamsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var projectTeamIds = _context.Set<ProjectMember>().AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.TeamId.HasValue
                && member.Status == "Active" && !member.IsDeleted
                && member.StartDate.Date <= today && member.EndDate.Date >= today)
            .Select(member => member.TeamId!.Value);

        return await _context.Set<Team>().AsNoTracking()
            .Where(team => projectTeamIds.Contains(team.Id) && team.IsActive && !team.IsDeleted)
            .OrderBy(team => team.Name)
            .Select(team => new WorkItemTeamReference(team.Id, team.Name, team.Code))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsActiveProjectMemberAsync(Guid projectId, Guid resourceId, CancellationToken cancellationToken = default)
    {
        var candidates = await GetEligibleAsync(projectId, cancellationToken);
        return candidates.Any(candidate => candidate.ResourceId == resourceId);
    }

    public async Task<bool> IsActiveProjectTeamAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _context.Set<ProjectMember>().AnyAsync(member => member.ProjectId == projectId
            && member.TeamId == teamId && member.Status == "Active" && !member.IsDeleted
            && member.StartDate.Date <= today && member.EndDate.Date >= today
            && _context.Set<Team>().Any(team => team.Id == teamId && team.IsActive && !team.IsDeleted),
            cancellationToken);
    }

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
