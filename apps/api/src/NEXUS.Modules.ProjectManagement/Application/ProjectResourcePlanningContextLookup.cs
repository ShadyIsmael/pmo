using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public sealed class ProjectResourcePlanningContextLookup : IProjectResourcePlanningContextLookup
{
    private readonly DbContext _context;

    public ProjectResourcePlanningContextLookup(DbContext context)
    {
        _context = context;
    }

    public async Task<ProjectResourcePlanningContext?> GetPlanningContextAsync(Guid projectId, Guid resourceId, CancellationToken cancellationToken = default)
    {
        var member = await _context.Set<ProjectMember>()
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.ResourceId == resourceId && !m.IsDeleted && m.Status == "Active")
            .FirstOrDefaultAsync(cancellationToken);

        if (member is null)
            return null;

        var project = await _context.Set<Project>()
            .AsNoTracking()
            .Where(p => p.Id == member.ProjectId)
            .Select(p => new { p.WorkingCalendarId })
            .FirstOrDefaultAsync(cancellationToken);

        var roleIds = await _context.Set<ProjectMemberRole>()
            .AsNoTracking()
            .Where(pmr => pmr.ProjectMemberId == member.Id && !pmr.IsDeleted)
            .Select(pmr => pmr.ProjectRoleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new ProjectResourcePlanningContext(
            member.Id,
            member.ProjectId,
            member.ResourceId,
            member.CalendarPolicy,
            project?.WorkingCalendarId,
            roleIds,
            true);
    }

    public async Task<bool> HasActiveProjectMemberRoleAsync(Guid projectId, Guid resourceId, Guid projectRoleId, CancellationToken cancellationToken = default)
    {
        var member = await _context.Set<ProjectMember>()
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.ResourceId == resourceId && !m.IsDeleted && m.Status == "Active")
            .FirstOrDefaultAsync(cancellationToken);

        if (member is null)
            return false;

        var roleExists = await _context.Set<ProjectMemberRole>()
            .AsNoTracking()
            .AnyAsync(pmr => pmr.ProjectMemberId == member.Id && pmr.ProjectRoleId == projectRoleId && !pmr.IsDeleted, cancellationToken);

        if (!roleExists)
            return false;

        var role = await _context.Set<Lookup>()
            .AsNoTracking()
            .Where(l => l.Id == projectRoleId && l.Category == "ProjectRole" && l.IsActive && !l.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        return role is not null;
    }

    public async Task<IReadOnlyDictionary<Guid, ProjectResourcePlanningContext>> GetPlanningContextsAsync(Guid projectId, IEnumerable<Guid> resourceIds, CancellationToken cancellationToken = default)
    {
        var ids = resourceIds.Distinct().ToList();
        var members = await _context.Set<ProjectMember>()
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId && ids.Contains(m.ResourceId) && !m.IsDeleted && m.Status == "Active")
            .ToListAsync(cancellationToken);

        var memberIds = members.Select(m => m.Id).ToList();
        var roleMap = _context.Set<ProjectMemberRole>()
            .AsNoTracking()
            .Where(pmr => memberIds.Contains(pmr.ProjectMemberId) && !pmr.IsDeleted)
            .GroupBy(pmr => pmr.ProjectMemberId)
            .AsEnumerable()
            .ToDictionary(g => g.Key, g => g.Select(pmr => pmr.ProjectRoleId).Distinct().ToList());

        var project = await _context.Set<Project>()
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.WorkingCalendarId })
            .FirstOrDefaultAsync(cancellationToken);

        return members.ToDictionary(
            m => m.ResourceId,
            m => new ProjectResourcePlanningContext(
                m.Id,
                m.ProjectId,
                m.ResourceId,
                m.CalendarPolicy,
                project?.WorkingCalendarId,
                roleMap.TryGetValue(m.Id, out var roles) ? roles : new List<Guid>(),
                true));
    }
}
