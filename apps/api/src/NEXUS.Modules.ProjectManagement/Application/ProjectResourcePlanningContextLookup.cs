using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

/// <summary>
/// Resolves a Resource's project entitlements from a direct Project Member row or
/// from an effective membership in a Team assigned to the project. Direct rows take
/// precedence when both paths include the same Resource.
/// </summary>
public sealed class ProjectResourcePlanningContextLookup : IProjectResourcePlanningContextLookup
{
    private readonly DbContext _context;

    public ProjectResourcePlanningContextLookup(DbContext context) => _context = context;

    public async Task<ProjectResourcePlanningContext?> GetPlanningContextAsync(
        Guid projectId, Guid resourceId, CancellationToken cancellationToken = default)
    {
        var candidate = (await LoadMembershipsAsync(projectId, new[] { resourceId }, cancellationToken))
            .OrderByDescending(item => item.IsDirect)
            .ThenByDescending(item => item.Member.AllocationPercentage)
            .ThenBy(item => item.StartDate)
            .FirstOrDefault();
        return candidate is null ? null : await ToContextAsync(candidate, cancellationToken);
    }

    public async Task<bool> HasActiveProjectMemberRoleAsync(
        Guid projectId, Guid resourceId, Guid projectRoleId, CancellationToken cancellationToken = default)
    {
        var memberships = await LoadMembershipsAsync(projectId, new[] { resourceId }, cancellationToken);
        if (memberships.Count == 0) return false;
        var memberIds = memberships.Select(item => item.Member.Id).Distinct().ToArray();
        var roleExists = await _context.Set<ProjectMemberRole>().AsNoTracking()
            .AnyAsync(role => memberIds.Contains(role.ProjectMemberId)
                && role.ProjectRoleId == projectRoleId && !role.IsDeleted, cancellationToken);
        if (!roleExists) return false;

        return await _context.Set<Lookup>().AsNoTracking()
            .AnyAsync(lookup => lookup.Id == projectRoleId && lookup.Category == "ProjectRole"
                && lookup.IsActive && !lookup.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ProjectResourcePlanningContext>> GetPlanningContextsAsync(
        Guid projectId, IEnumerable<Guid> resourceIds, CancellationToken cancellationToken = default)
    {
        var ids = resourceIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, ProjectResourcePlanningContext>();

        var memberships = await LoadMembershipsAsync(projectId, ids, cancellationToken);
        var selected = memberships
            .GroupBy(item => item.ResourceId)
            .Select(group => group.OrderByDescending(item => item.IsDirect)
                .ThenByDescending(item => item.Member.AllocationPercentage)
                .ThenBy(item => item.StartDate)
                .First())
            .ToList();
        if (selected.Count == 0) return new Dictionary<Guid, ProjectResourcePlanningContext>();

        var memberIds = selected.Select(item => item.Member.Id).Distinct().ToArray();
        var roleRows = await _context.Set<ProjectMemberRole>().AsNoTracking()
            .Where(role => memberIds.Contains(role.ProjectMemberId) && !role.IsDeleted)
            .Select(role => new { role.ProjectMemberId, role.ProjectRoleId })
            .ToListAsync(cancellationToken);
        var rolesByMember = roleRows.GroupBy(item => item.ProjectMemberId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.ProjectRoleId).Distinct().ToList());
        var projectCalendarId = await _context.Set<Project>().AsNoTracking()
            .Where(project => project.Id == projectId && !project.IsDeleted)
            .Select(project => project.WorkingCalendarId)
            .FirstOrDefaultAsync(cancellationToken);

        return selected.ToDictionary(
            item => item.ResourceId,
            item => new ProjectResourcePlanningContext(
                item.Member.Id,
                item.Member.ProjectId,
                item.ResourceId,
                item.Member.CalendarPolicy,
                projectCalendarId,
                rolesByMember.GetValueOrDefault(item.Member.Id) ?? new List<Guid>(),
                true,
                item.Member.AllocationPercentage,
                item.StartDate,
                item.EndDate));
    }

    private async Task<ProjectResourcePlanningContext> ToContextAsync(
        EffectiveProjectMembership membership, CancellationToken cancellationToken)
    {
        var roleIds = await _context.Set<ProjectMemberRole>().AsNoTracking()
            .Where(role => role.ProjectMemberId == membership.Member.Id && !role.IsDeleted)
            .Select(role => role.ProjectRoleId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var projectCalendarId = await _context.Set<Project>().AsNoTracking()
            .Where(project => project.Id == membership.Member.ProjectId && !project.IsDeleted)
            .Select(project => project.WorkingCalendarId)
            .FirstOrDefaultAsync(cancellationToken);

        return new ProjectResourcePlanningContext(
            membership.Member.Id,
            membership.Member.ProjectId,
            membership.ResourceId,
            membership.Member.CalendarPolicy,
            projectCalendarId,
            roleIds,
            true,
            membership.Member.AllocationPercentage,
            membership.StartDate,
            membership.EndDate);
    }

    private async Task<List<EffectiveProjectMembership>> LoadMembershipsAsync(
        Guid projectId, IReadOnlyCollection<Guid> resourceIds, CancellationToken cancellationToken)
    {
        var ids = resourceIds.Distinct().ToArray();
        if (ids.Length == 0) return new List<EffectiveProjectMembership>();

        var members = await _context.Set<ProjectMember>().AsNoTracking()
            .Where(member => member.ProjectId == projectId && !member.IsDeleted && member.Status == "Active"
                && member.StartDate.Date <= member.EndDate.Date)
            .ToListAsync(cancellationToken);
        var candidates = members
            .Where(member => member.ResourceId.HasValue && ids.Contains(member.ResourceId.Value))
            .Select(member => new EffectiveProjectMembership(
                member, member.ResourceId!.Value, true, member.StartDate.Date, member.EndDate.Date))
            .ToList();

        var groupRows = members.Where(member => member.TeamId.HasValue).ToList();
        if (groupRows.Count == 0) return candidates;
        var teamIds = groupRows.Select(member => member.TeamId!.Value).Distinct().ToArray();
        var activeTeamIds = await _context.Set<Team>().AsNoTracking()
            .Where(team => teamIds.Contains(team.Id) && team.IsActive && !team.IsDeleted)
            .Select(team => team.Id)
            .ToArrayAsync(cancellationToken);
        var activeTeamSet = activeTeamIds.ToHashSet();
        groupRows = groupRows.Where(member => activeTeamSet.Contains(member.TeamId!.Value)).ToList();
        if (groupRows.Count == 0) return candidates;

        var teamMemberships = await _context.Set<TeamMember>().AsNoTracking()
            .Where(member => activeTeamSet.Contains(member.TeamId) && ids.Contains(member.ResourceId)
                && member.IsActive && !member.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var group in groupRows)
        {
            foreach (var teamMember in teamMemberships.Where(item => item.TeamId == group.TeamId))
            {
                var from = teamMember.EffectiveFrom.Date > group.StartDate.Date
                    ? teamMember.EffectiveFrom.Date : group.StartDate.Date;
                var groupEnd = group.EndDate.Date;
                var teamEnd = teamMember.EffectiveTo?.Date ?? groupEnd;
                var to = teamEnd < groupEnd ? teamEnd : groupEnd;
                if (from <= to)
                    candidates.Add(new EffectiveProjectMembership(group, teamMember.ResourceId, false, from, to));
            }
        }

        return candidates;
    }

    private sealed record EffectiveProjectMembership(
        ProjectMember Member, Guid ResourceId, bool IsDirect, DateTime StartDate, DateTime EndDate);
}
