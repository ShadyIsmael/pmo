using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Persistence.Lookups;

public sealed class ProjectAudienceLookup(DbContext context) : IProjectAudienceLookup
{
    public async Task<IReadOnlyList<ProjectAudienceUserReference>> SearchTenantUsersAsync(
        Guid tenantId,
        string? search,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return Array.Empty<ProjectAudienceUserReference>();

        var candidates = context.Set<User>().AsNoTracking()
            .Where(user => user.TenantId == tenantId && user.IsActive && !user.IsDeleted);
        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            candidates = candidates.Where(user => user.FirstName.Contains(term)
                || user.LastName.Contains(term)
                || user.Email.Contains(term));

        return await candidates
            .OrderBy(user => user.FirstName).ThenBy(user => user.LastName).ThenBy(user => user.Id)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(user => new ProjectAudienceUserReference(user.Id, user.FirstName + " " + user.LastName, user.Email))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectAudienceUserReference>> SearchProjectUsersAsync(
        Guid tenantId,
        Guid projectId,
        string? search,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty) return Array.Empty<ProjectAudienceUserReference>();

        var projectExists = await context.Set<Project>().AsNoTracking()
            .AnyAsync(project => project.Id == projectId && project.TenantId == tenantId && !project.IsDeleted, cancellationToken);
        if (!projectExists) return Array.Empty<ProjectAudienceUserReference>();

        var userIds = await GetProjectUserIdsAsync(tenantId, projectId, cancellationToken);

        if (userIds.Count == 0) return Array.Empty<ProjectAudienceUserReference>();
        var term = search?.Trim();
        var candidates = context.Set<User>().AsNoTracking()
            .Where(user => userIds.Contains(user.Id) && user.TenantId == tenantId && user.IsActive && !user.IsDeleted);
        if (!string.IsNullOrWhiteSpace(term))
            candidates = candidates.Where(user => user.FirstName.Contains(term) || user.LastName.Contains(term) || user.Email.Contains(term));

        var users = await candidates
            .OrderBy(user => user.FirstName).ThenBy(user => user.LastName).ThenBy(user => user.Id)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(user => new ProjectAudienceUserReference(user.Id, user.FirstName + " " + user.LastName, user.Email))
            .ToListAsync(cancellationToken);
        return users;
    }

    public async Task<IReadOnlyDictionary<Guid, ProjectAudienceUserReference>> GetProjectUsersAsync(
        Guid tenantId,
        Guid projectId,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var requested = userIds.Where(id => id != Guid.Empty).Distinct().Take(500).ToArray();
        if (tenantId == Guid.Empty || projectId == Guid.Empty || requested.Length == 0)
            return new Dictionary<Guid, ProjectAudienceUserReference>();

        var eligible = await GetProjectUserIdsAsync(tenantId, projectId, cancellationToken);
        var eligibleIds = requested.Where(eligible.Contains).ToArray();
        if (eligibleIds.Length == 0) return new Dictionary<Guid, ProjectAudienceUserReference>();

        return await context.Set<User>().AsNoTracking()
            .Where(user => eligibleIds.Contains(user.Id) && user.TenantId == tenantId && user.IsActive && !user.IsDeleted)
            .Select(user => new ProjectAudienceUserReference(user.Id, user.FirstName + " " + user.LastName, user.Email))
            .ToDictionaryAsync(user => user.UserId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ProjectAudienceUserReference>> GetActiveUsersAsync(
        Guid tenantId,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().Take(500).ToArray();
        if (tenantId == Guid.Empty || ids.Length == 0) return new Dictionary<Guid, ProjectAudienceUserReference>();

        return await context.Set<User>().AsNoTracking()
            .Where(user => ids.Contains(user.Id) && user.TenantId == tenantId && user.IsActive && !user.IsDeleted)
            .Select(user => new ProjectAudienceUserReference(user.Id, user.FirstName + " " + user.LastName, user.Email))
            .ToDictionaryAsync(user => user.UserId, cancellationToken);
    }

    public async Task<bool> IsActiveProjectResourceAsync(
        Guid tenantId,
        Guid projectId,
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || resourceId == Guid.Empty) return false;

        var today = DateTime.UtcNow.Date;
        var directlyAssigned = await context.Set<ProjectMember>().AsNoTracking().AnyAsync(member => member.TenantId == tenantId
            && member.ProjectId == projectId && member.ResourceId == resourceId && !member.IsDeleted
            && member.Status == "Active" && member.StartDate.Date <= today && member.EndDate.Date >= today, cancellationToken);
        var assignedThroughProjectTeam =
            from assignment in context.Set<ProjectMember>().AsNoTracking()
            join team in context.Set<Team>().AsNoTracking() on assignment.TeamId equals (Guid?)team.Id
            join membership in context.Set<TeamMember>().AsNoTracking() on team.Id equals membership.TeamId
            where assignment.TenantId == tenantId && assignment.ProjectId == projectId
                && assignment.TeamId.HasValue && !assignment.IsDeleted && assignment.Status == "Active"
                && assignment.StartDate.Date <= today && assignment.EndDate.Date >= today
                && team.TenantId == tenantId && team.IsActive && !team.IsDeleted
                && membership.TenantId == tenantId && membership.ResourceId == resourceId
                && membership.IsActive && !membership.IsDeleted && membership.EffectiveFrom.Date <= today
                && (!membership.EffectiveTo.HasValue || membership.EffectiveTo.Value.Date >= today)
            select assignment.Id;
        var stakeholder = await context.Set<ProjectStakeholder>().AsNoTracking().AnyAsync(item => item.TenantId == tenantId
            && item.ProjectId == projectId && item.ResourceId == resourceId && !item.IsDeleted
            && item.Status == ProjectStakeholderStatus.Active, cancellationToken);
        var activeResource = await context.Set<Resource>().AsNoTracking().AnyAsync(resource => resource.Id == resourceId
            && resource.TenantId == tenantId && resource.IsActive && resource.Status == "Active" && !resource.IsDeleted, cancellationToken);

        if (!activeResource) return false;
        return directlyAssigned || stakeholder || await assignedThroughProjectTeam.AnyAsync(cancellationToken);
    }

    private async Task<HashSet<Guid>> GetProjectUserIdsAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        var userIds = new HashSet<Guid>();
        var memberUserIds = await (
            from member in context.Set<ProjectMember>().AsNoTracking()
            join resource in context.Set<Resource>().AsNoTracking() on member.ResourceId equals resource.Id
            where member.TenantId == tenantId && member.ProjectId == projectId && !member.IsDeleted && member.Status == "Active"
                && member.StartDate.Date <= DateTime.UtcNow.Date && member.EndDate.Date >= DateTime.UtcNow.Date
                && resource.TenantId == tenantId && !resource.IsDeleted && resource.IsActive && resource.Status == "Active"
                && resource.UserId.HasValue
            select resource.UserId!.Value
        ).Distinct().ToListAsync(cancellationToken);
        userIds.UnionWith(memberUserIds);

        var today = DateTime.UtcNow.Date;
        var teamMemberUserIds = await (
            from assignment in context.Set<ProjectMember>().AsNoTracking()
            join team in context.Set<Team>().AsNoTracking() on assignment.TeamId equals (Guid?)team.Id
            join membership in context.Set<TeamMember>().AsNoTracking() on team.Id equals membership.TeamId
            join resource in context.Set<Resource>().AsNoTracking() on membership.ResourceId equals resource.Id
            join user in context.Set<User>().AsNoTracking() on resource.UserId equals (Guid?)user.Id
            where assignment.TenantId == tenantId && assignment.ProjectId == projectId
                && !assignment.IsDeleted && assignment.Status == "Active"
                && assignment.StartDate.Date <= today && assignment.EndDate.Date >= today
                && team.TenantId == tenantId && team.IsActive && !team.IsDeleted
                && membership.TenantId == tenantId && membership.IsActive && !membership.IsDeleted
                && membership.EffectiveFrom.Date <= today
                && (!membership.EffectiveTo.HasValue || membership.EffectiveTo.Value.Date >= today)
                && resource.TenantId == tenantId && resource.IsActive && !resource.IsDeleted
                && resource.Status == "Active" && resource.UserId.HasValue
                && user.TenantId == tenantId && user.IsActive && !user.IsDeleted
            select user.Id
        ).Distinct().ToListAsync(cancellationToken);
        userIds.UnionWith(teamMemberUserIds);

        var stakeholderUserIds = await context.Set<ProjectStakeholder>().AsNoTracking()
            .Where(stakeholder => stakeholder.TenantId == tenantId && stakeholder.ProjectId == projectId
                && !stakeholder.IsDeleted && stakeholder.Status == ProjectStakeholderStatus.Active && stakeholder.UserId.HasValue)
            .Select(stakeholder => stakeholder.UserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        userIds.UnionWith(stakeholderUserIds);

        var stakeholderResourceUserIds = await (
            from stakeholder in context.Set<ProjectStakeholder>().AsNoTracking()
            join resource in context.Set<Resource>().AsNoTracking() on stakeholder.ResourceId equals (Guid?)resource.Id
            where stakeholder.TenantId == tenantId && stakeholder.ProjectId == projectId
                && !stakeholder.IsDeleted && stakeholder.Status == ProjectStakeholderStatus.Active
                && resource.TenantId == tenantId && resource.IsActive && resource.Status == "Active"
                && !resource.IsDeleted && resource.UserId.HasValue
            select resource.UserId!.Value
        ).Distinct().ToListAsync(cancellationToken);
        userIds.UnionWith(stakeholderResourceUserIds);

        var projectOwners = await context.Set<Project>().AsNoTracking()
            .Where(project => project.Id == projectId && project.TenantId == tenantId && !project.IsDeleted)
            .Select(project => new { project.ProjectManagerId, project.SponsorId })
            .FirstOrDefaultAsync(cancellationToken);
        if (projectOwners?.ProjectManagerId is Guid projectManagerId) userIds.Add(projectManagerId);
        if (projectOwners?.SponsorId is Guid sponsorId) userIds.Add(sponsorId);

        var assignedManagerIds = await context.Set<ProjectManagerAssignment>().AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId && assignment.ProjectId == projectId && !assignment.IsDeleted
                && assignment.IsCurrent && assignment.EndDate == null)
            .Select(assignment => assignment.ProjectManagerId)
            .Distinct()
            .ToListAsync(cancellationToken);
        userIds.UnionWith(assignedManagerIds);
        return userIds;
    }

}
