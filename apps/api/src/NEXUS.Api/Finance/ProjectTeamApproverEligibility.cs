using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Api.Finance;

/// <summary>
/// Resolves current project participants who can be considered for explicit finance
/// approval assignment. A team relationship only supplies candidacy; each workflow
/// still checks the user's own effective finance permissions before assignment.
/// </summary>
internal static class ProjectTeamApproverEligibility
{
    public static async Task<HashSet<Guid>> GetActiveLinkedUserIdsAsync(
        DbContext db, Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var directResources = db.Set<ProjectMember>().IgnoreQueryFilters().AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.ProjectId == projectId
                && !member.IsDeleted && member.Status == "Active"
                && member.StartDate.Date <= today && member.EndDate.Date >= today
                && member.ResourceId.HasValue)
            .Select(member => member.ResourceId!.Value);

        var teamResources =
            from assignment in db.Set<ProjectMember>().IgnoreQueryFilters().AsNoTracking()
            join team in db.Set<Team>().IgnoreQueryFilters().AsNoTracking()
                on new { assignment.TenantId, TeamId = assignment.TeamId!.Value }
                equals new { team.TenantId, TeamId = team.Id }
            join membership in db.Set<TeamMember>().IgnoreQueryFilters().AsNoTracking()
                on new { team.TenantId, team.Id }
                equals new { membership.TenantId, Id = membership.TeamId }
            where assignment.TenantId == tenantId && assignment.ProjectId == projectId
                && !assignment.IsDeleted && assignment.Status == "Active" && assignment.TeamId.HasValue
                && assignment.StartDate.Date <= today && assignment.EndDate.Date >= today
                && team.IsActive && !team.IsDeleted
                && membership.IsActive && !membership.IsDeleted
                && membership.EffectiveFrom.Date <= today
                && (!membership.EffectiveTo.HasValue || membership.EffectiveTo.Value.Date >= today)
            select membership.ResourceId;

        var resourceIds = await directResources.Concat(teamResources).Distinct().ToArrayAsync(cancellationToken);
        if (resourceIds.Length == 0) return [];

        var userIds = await (
            from resource in db.Set<Resource>().IgnoreQueryFilters().AsNoTracking()
            join user in db.Set<User>().IgnoreQueryFilters().AsNoTracking()
                on new { resource.TenantId, UserId = resource.UserId ?? Guid.Empty }
                equals new { user.TenantId, UserId = user.Id }
            where resource.TenantId == tenantId && resourceIds.Contains(resource.Id)
                && !resource.IsDeleted && resource.IsActive && resource.UserId.HasValue
                && !user.IsDeleted && user.IsActive
            select user.Id).Distinct().ToListAsync(cancellationToken);

        return userIds.ToHashSet();
    }
}
