using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.WorkItems.Application;
using NEXUS.Modules.WorkItems.Domain;

namespace NEXUS.Persistence;

public sealed class WorkItemCommentAccess : IWorkItemCommentAccess
{
    private readonly DbContext _context;

    public WorkItemCommentAccess(DbContext context)
    {
        _context = context;
    }

    public async Task<bool> CanCommentAsync(WorkItem item, Guid userId, CancellationToken cancellationToken = default)
    {
        if (await _context.Set<WorkItemAssignment>().AnyAsync(assignment => assignment.WorkItemId == item.Id && assignment.UserId == userId && assignment.UnassignedAt == null, cancellationToken)) return true;
        if (!item.ProjectId.HasValue) return false;
        var linkedUserIsActive = await _context.Set<User>().AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);
        if (linkedUserIsActive && await _context.Set<ProjectStakeholder>().AnyAsync(stakeholder => stakeholder.ProjectId == item.ProjectId.Value && stakeholder.UserId.HasValue && stakeholder.UserId == userId && stakeholder.Status == ProjectStakeholderStatus.Active && stakeholder.RelationshipType == "Stakeholder", cancellationToken)) return true;
        if (await _context.Set<ProjectManagerAssignment>().AnyAsync(assignment => assignment.ProjectId == item.ProjectId.Value && assignment.ProjectManagerId == userId && assignment.IsCurrent && assignment.EndDate == null, cancellationToken)) return true;
        return await _context.Set<Project>().AnyAsync(project => project.Id == item.ProjectId.Value && project.ProjectManagerId == userId, cancellationToken);
    }
}
