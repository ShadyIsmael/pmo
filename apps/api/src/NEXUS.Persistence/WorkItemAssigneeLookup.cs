using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
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
            select new WorkItemAssigneeReference(user.Id, resource.Id, resource.Name, resource.Email, resource.JobTitle)
        ).ToListAsync(cancellationToken);

        return candidates
            .GroupBy(candidate => candidate.AssigneeId)
            .Select(group => group.First())
            .ToList();
    }

    public async Task<bool> IsActiveProjectMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        var candidates = await GetEligibleAsync(projectId, cancellationToken);
        return candidates.Any(candidate => candidate.AssigneeId == userId);
    }
}
