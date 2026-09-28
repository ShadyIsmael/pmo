using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.ProjectManagement.Application;

/// <summary>
/// Keeps the existing ProjectMember/Resource representation aligned with the
/// authoritative ProjectManagerAssignment relationship during a manager handover.
/// </summary>
public sealed class ProjectManagerMembershipSynchronizer
{
    private readonly DbContext _context;

    public ProjectManagerMembershipSynchronizer(DbContext context) => _context = context;

    public async Task SynchronizeAsync(
        Project project,
        Guid previousManagerId,
        Guid newManagerId,
        DateTime handoverDate,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var managerResources = await _context.Set<Resource>()
            .Where(resource => resource.UserId.HasValue &&
                               (resource.UserId == previousManagerId || resource.UserId == newManagerId) &&
                               !resource.IsDeleted)
            .OrderBy(resource => resource.CreatedAt)
            .ToListAsync(cancellationToken);

        var previousResourceId = managerResources.FirstOrDefault(resource => resource.UserId == previousManagerId)?.Id;
        if (previousResourceId.HasValue)
        {
            var previousMembers = await _context.Set<ProjectMember>()
                .Where(member => member.ProjectId == project.Id && member.ResourceId == previousResourceId.Value && !member.IsDeleted)
                .ToListAsync(cancellationToken);

            // End only the PM membership relation. Other active roles for the same
            // Resource remain unchanged, and the historical row is retained.
            foreach (var member in previousMembers.Where(member => member.Status == "Active" && IsProjectManagerRole(member.Role)))
            {
                member.Status = "Inactive";
                member.EndDate = handoverDate;
                member.UpdatedBy = userId;
            }
        }

        var newResourceId = managerResources.FirstOrDefault(resource => resource.UserId == newManagerId)?.Id;
        if (!newResourceId.HasValue)
        {
            // The assignment remains authoritative. ProjectPeopleService renders a
            // governance person from the User when no Resource link exists yet.
            return;
        }

        var newMembers = await _context.Set<ProjectMember>()
            .Where(member => member.ProjectId == project.Id && member.ResourceId == newResourceId.Value && !member.IsDeleted)
            .OrderBy(member => member.CreatedAt)
            .ToListAsync(cancellationToken);

        var activeMembers = newMembers.Where(member => member.Status == "Active").ToList();
        var activeManagerMembers = activeMembers.Where(member => IsProjectManagerRole(member.Role)).ToList();
        foreach (var duplicate in activeManagerMembers.Skip(1))
        {
            duplicate.Status = "Inactive";
            duplicate.EndDate = handoverDate;
            duplicate.UpdatedBy = userId;
        }

        if (activeManagerMembers.Count > 0 || activeMembers.Count > 0)
        {
            // Reuse an existing membership. Do not overwrite another valid team role;
            // ProjectManagerAssignment records the PM relationship.
            return;
        }

        var reusableManagerMember = newMembers.FirstOrDefault(member => IsProjectManagerRole(member.Role));
        if (reusableManagerMember is not null)
        {
            reusableManagerMember.Status = "Active";
            reusableManagerMember.StartDate = handoverDate;
            reusableManagerMember.EndDate = ProjectMemberEndDate(project, handoverDate);
            reusableManagerMember.UpdatedBy = userId;
            return;
        }

        _context.Set<ProjectMember>().Add(new ProjectMember
        {
            TenantId = project.TenantId,
            ProjectId = project.Id,
            ResourceId = newResourceId.Value,
            Role = ProjectRelationshipType.ProjectManager,
            AllocationPercentage = 100m,
            HoursPerWeek = 40m,
            StartDate = handoverDate,
            EndDate = ProjectMemberEndDate(project, handoverDate),
            Status = "Active",
            IsBillable = true,
            Notes = "Project Manager membership synchronized by handover",
            CreatedBy = userId
        });
    }

    public static bool IsProjectManagerRole(string? role) =>
        string.Equals(role?.Trim(), ProjectRelationshipType.ProjectManager, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role?.Trim(), "Project Manager", StringComparison.OrdinalIgnoreCase);

    private static DateTime ProjectMemberEndDate(Project project, DateTime handoverDate)
    {
        var endDate = project.EndDate?.Date ?? handoverDate.AddYears(1).Date;
        return endDate > handoverDate ? endDate : handoverDate.AddDays(1);
    }
}
