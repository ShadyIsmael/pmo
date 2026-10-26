using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

/// <summary>
/// Projects the canonical Project.SponsorId relationship into an existing linked
/// Resource's ProjectMember record. It never creates identity or Resource records.
/// </summary>
public sealed class ProjectSponsorMembershipSynchronizer
{
    private const string ManagedMembershipNote = "[system-managed-project-sponsor]";
    private readonly DbContext _context;

    public ProjectSponsorMembershipSynchronizer(DbContext context) => _context = context;

    public async Task<Error?> ValidateAssignmentAsync(Guid? userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (!userId.HasValue) return null;

        var activeUser = await _context.Set<User>().AsNoTracking().AnyAsync(user =>
            user.Id == userId.Value && user.TenantId == tenantId && user.IsActive && !user.IsDeleted, cancellationToken);
        if (!activeUser) return Error.Validation("The Sponsor must be an active user in the current tenant.");

        // SponsorId is a user relationship. A linked Resource enables team-membership
        // projection, but the canonical governance assignment must not fabricate or
        // require a Resource record; ProjectPeopleService can still show the User-backed
        // governance row when no operational Resource exists.
        return null;
    }

    public async Task SynchronizeAsync(
        Project project,
        Guid? previousSponsorUserId,
        Guid? newSponsorUserId,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (previousSponsorUserId == newSponsorUserId) return;

        var today = DateTime.UtcNow.Date;
        if (previousSponsorUserId.HasValue)
        {
            var previousResourceIds = await _context.Set<Resource>().AsNoTracking()
                .Where(resource => resource.TenantId == project.TenantId && resource.UserId == previousSponsorUserId.Value && !resource.IsDeleted)
                .Select(resource => resource.Id)
                .ToListAsync(cancellationToken);
            var managedRows = await _context.Set<ProjectMember>().Where(member =>
                    member.TenantId == project.TenantId && member.ProjectId == project.Id
                    && previousResourceIds.Contains(member.ResourceId) && !member.IsDeleted
                    && member.Notes == ManagedMembershipNote)
                .ToListAsync(cancellationToken);
            foreach (var member in managedRows)
            {
                member.Status = "Inactive";
                member.EndDate = today;
                member.UpdatedBy = actorId;
            }
        }

        if (!newSponsorUserId.HasValue) return;

        var resourceId = await _context.Set<Resource>().AsNoTracking()
            .Where(resource => resource.TenantId == project.TenantId && resource.UserId == newSponsorUserId.Value
                && resource.IsActive && !resource.IsDeleted)
            .OrderBy(resource => resource.CreatedAt)
            .Select(resource => (Guid?)resource.Id)
            .FirstOrDefaultAsync(cancellationToken);
        // Existing legacy assignments without a Resource remain governed by SponsorId
        // and are rendered as a governance relationship; never synthesize a Resource.
        if (!resourceId.HasValue) return;

        var activeMembershipExists = await _context.Set<ProjectMember>().AnyAsync(member =>
            member.TenantId == project.TenantId && member.ProjectId == project.Id
            && member.ResourceId == resourceId.Value && member.Status == "Active"
            && !member.IsDeleted, cancellationToken);
        if (activeMembershipExists) return;

        var reusable = await _context.Set<ProjectMember>().IgnoreQueryFilters()
            .Where(member => member.TenantId == project.TenantId && member.ProjectId == project.Id
                && member.ResourceId == resourceId.Value && member.Notes == ManagedMembershipNote)
            .OrderByDescending(member => member.UpdatedAt ?? member.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var startDate = project.StartDate?.Date ?? today;
        var endDate = project.EndDate?.Date ?? startDate.AddYears(1);
        if (endDate <= startDate) endDate = startDate.AddDays(1);

        if (reusable is not null)
        {
            reusable.IsDeleted = false;
            reusable.Role = ProjectRelationshipType.Sponsor;
            reusable.AllocationPercentage = 0m;
            reusable.HoursPerWeek = null;
            reusable.StartDate = startDate;
            reusable.EndDate = endDate;
            reusable.Status = "Active";
            reusable.IsBillable = false;
            reusable.UpdatedBy = actorId;
            return;
        }

        _context.Set<ProjectMember>().Add(new ProjectMember
        {
            TenantId = project.TenantId,
            ProjectId = project.Id,
            ResourceId = resourceId.Value,
            Role = ProjectRelationshipType.Sponsor,
            AllocationPercentage = 0m,
            StartDate = startDate,
            EndDate = endDate,
            Status = "Active",
            IsBillable = false,
            Notes = ManagedMembershipNote,
            CreatedBy = actorId
        });
    }

    public static bool IsManagedMembership(ProjectMember member) => member.Notes == ManagedMembershipNote;

    public static bool IsReservedManagedMembershipNote(string? notes) =>
        string.Equals(notes, ManagedMembershipNote, StringComparison.Ordinal);
}
