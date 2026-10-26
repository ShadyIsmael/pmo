using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.ProjectManagement.Application;

/// <summary>
/// Composes the legacy allocation and stakeholder stores into one project-people contract.
/// Resource Management remains the only source of person identity.
/// </summary>
public sealed class ProjectPeopleService : IProjectPeopleService
{
    private readonly DbContext _context;
    private readonly IResourceReferenceLookup _resources;
    private readonly IAuditService _audit;
    private readonly ProjectSponsorMembershipSynchronizer _sponsorMembershipSynchronizer;
    private readonly IProjectService? _projectService;

    public ProjectPeopleService(DbContext context, IResourceReferenceLookup resources, IAuditService audit, IProjectService? projectService = null)
    {
        _context = context;
        _resources = resources;
        _audit = audit;
        _projectService = projectService;
        _sponsorMembershipSynchronizer = new ProjectSponsorMembershipSynchronizer(context);
    }

    public async Task<Result<IReadOnlyList<ProjectPersonDto>>> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _context.Set<Project>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);
        if (project is null) return Result<IReadOnlyList<ProjectPersonDto>>.Failure(Error.NotFound("Project not found"));

        var members = await _context.Set<ProjectMember>().AsNoTracking()
            .Where(m => m.ProjectId == projectId && !m.IsDeleted)
            .OrderBy(m => m.Role).ThenBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var memberIds = members.Select(m => m.Id).ToList();
        var memberRoles = await _context.Set<ProjectMemberRole>().AsNoTracking()
            .Where(pmr => memberIds.Contains(pmr.ProjectMemberId) && !pmr.IsDeleted)
            .ToListAsync(cancellationToken);

        var roleIds = memberRoles.Select(pmr => pmr.ProjectRoleId).Distinct().ToList();
        var roleLookup = await _context.Set<Lookup>().AsNoTracking()
            .Where(l => roleIds.Contains(l.Id) && !l.IsDeleted)
            .ToDictionaryAsync(l => l.Id, cancellationToken);
        var stakeholders = await _context.Set<ProjectStakeholder>().AsNoTracking()
            .Where(s => s.ProjectId == projectId && !s.IsDeleted)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var resourceIds = members.Select(m => m.ResourceId)
            .Concat(stakeholders.Where(s => s.ResourceId.HasValue).Select(s => s.ResourceId!.Value))
            .Distinct().ToList();
        var userIds = stakeholders.Where(s => !s.ResourceId.HasValue && s.UserId.HasValue).Select(s => s.UserId!.Value)
            .Concat(new[] { project.ProjectManagerId, project.SponsorId }.Where(id => id.HasValue).Select(id => id!.Value))
            .Distinct().ToList();
        var resourceById = await _resources.GetDetailsAsync(resourceIds, cancellationToken);
        var resourceByUser = await _resources.GetDetailsByUserIdsAsync(userIds, cancellationToken);
        var usersById = await _context.Set<User>()
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id) && !user.IsDeleted)
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        var result = new List<ProjectPersonDto>(members.Count + stakeholders.Count + 2);
        var managerResourceId = project.ProjectManagerId.HasValue && resourceByUser.TryGetValue(project.ProjectManagerId.Value, out var managerResource)
            ? managerResource.Id
            : (Guid?)null;
        var sponsorResourceId = project.SponsorId.HasValue && resourceByUser.TryGetValue(project.SponsorId.Value, out var sponsorResource)
            ? sponsorResource.Id
            : (Guid?)null;
        result.AddRange(members.Select(member =>
        {
            var active = member.Status == "Active";
            var isProjectManager = active && managerResourceId == member.ResourceId && IsProjectManagerRole(member.Role);
            var isSponsor = active && sponsorResourceId == member.ResourceId;
            var memberFunctionalRoles = memberRoles
                .Where(pmr => pmr.ProjectMemberId == member.Id)
                .Select(pmr => roleLookup.TryGetValue(pmr.ProjectRoleId, out var role)
                    ? new ProjectRoleRefDto { Id = role.Id, Code = role.Code, NameEn = role.NameEn, NameAr = role.NameAr, IsPrimary = pmr.IsPrimary }
                    : null)
                .Where(dto => dto is not null)
                .Cast<ProjectRoleRefDto>()
                .ToList();
            return ToMemberDto(
                member,
                resourceById.GetValueOrDefault(member.ResourceId),
                isProjectManager ? ProjectRelationshipType.ProjectManager : isSponsor ? ProjectRelationshipType.Sponsor : ProjectRelationshipType.TeamMember,
                memberFunctionalRoles,
                isProjectManager && isSponsor ? new[] { ProjectRelationshipType.Sponsor } : Array.Empty<string>());
        }));
        result.AddRange(stakeholders.Select(stakeholder => ToStakeholderDto(stakeholder,
            stakeholder.ResourceId.HasValue ? resourceById.GetValueOrDefault(stakeholder.ResourceId.Value) :
            stakeholder.UserId.HasValue ? resourceByUser.GetValueOrDefault(stakeholder.UserId.Value) : null)));

        if (project.ProjectManagerId.HasValue)
        {
            var managerBackedByMembership = false;
            if (resourceByUser.TryGetValue(project.ProjectManagerId.Value, out var manager))
            {
                managerBackedByMembership = members.Any(member => member.Status == "Active" && member.ResourceId == manager.Id && IsProjectManagerRole(member.Role));
                if (!managerBackedByMembership)
                    result.Add(ToGovernanceDto(project, manager, ProjectRelationshipType.ProjectManager));
            }
            if (!managerBackedByMembership && !resourceByUser.ContainsKey(project.ProjectManagerId.Value) && usersById.TryGetValue(project.ProjectManagerId.Value, out var managerUser))
                result.Add(ToGovernanceDto(project, managerUser, ProjectRelationshipType.ProjectManager));
        }
        if (project.SponsorId.HasValue)
        {
            var sponsorBackedByMembership = sponsorResourceId.HasValue && members.Any(member =>
                member.Status == "Active" && member.ResourceId == sponsorResourceId.Value);
            if (!sponsorBackedByMembership && resourceByUser.TryGetValue(project.SponsorId.Value, out var sponsor))
                result.Add(ToGovernanceDto(project, sponsor, ProjectRelationshipType.Sponsor));
            else if (!sponsorBackedByMembership && !resourceByUser.ContainsKey(project.SponsorId.Value) && usersById.TryGetValue(project.SponsorId.Value, out var sponsorUser))
                result.Add(ToGovernanceDto(project, sponsorUser, ProjectRelationshipType.Sponsor));
        }

        return Result<IReadOnlyList<ProjectPersonDto>>.Success(result
            .OrderBy(person => person.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(person => person.RelationshipType, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    public async Task<Result<ProjectPersonDto>> AddAsync(Guid projectId, CreateProjectPersonDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);
        if (project is null) return Result<ProjectPersonDto>.Failure(Error.NotFound("Project not found"));
        if (dto.ResourceId == Guid.Empty) return Result<ProjectPersonDto>.Failure(Error.Validation("An existing Resource is required"));
        if (!ProjectRelationshipType.IsSupported(dto.RelationshipType)) return Result<ProjectPersonDto>.Failure(Error.Validation("Unsupported project relationship"));

        var resource = await _resources.FindAsync(dto.ResourceId, cancellationToken);
        if (resource is null) return Result<ProjectPersonDto>.Failure(Error.NotFound("Resource not found. Create this person in Resource Management first."));
        if (!resource.IsActive || string.Equals(resource.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
            return Result<ProjectPersonDto>.Failure(Error.Validation("The selected Resource is inactive"));

        var relationship = dto.RelationshipType.Trim();
        if (relationship == ProjectRelationshipType.ProjectManager)
            return Result<ProjectPersonDto>.Failure(Error.Validation("Project Manager changes must use the governed handover flow"));

        if (relationship == ProjectRelationshipType.TeamMember)
        {
            if (!dto.AllocationPercentage.HasValue)
                return Result<ProjectPersonDto>.Failure(Error.Validation("Project allocation percentage is required for team members."));
            if (await _context.Set<ProjectMember>().AnyAsync(m => m.ProjectId == projectId && m.ResourceId == dto.ResourceId && !m.IsDeleted && m.Status == "Active", cancellationToken))
                return Result<ProjectPersonDto>.Failure(Error.Conflict("This Resource is already an active Team Member on the project"));
            var startDate = (dto.StartDate ?? project.StartDate ?? DateTime.UtcNow.Date).Date;
            var endDate = (dto.EndDate ?? project.EndDate ?? startDate.AddYears(1)).Date;
            if (endDate <= startDate) return Result<ProjectPersonDto>.Failure(Error.Validation("End date must be after start date"));
            var allocation = dto.AllocationPercentage ?? 0m;
            if (allocation < 0 || allocation > 100) return Result<ProjectPersonDto>.Failure(Error.Validation("Allocation must be between 0 and 100"));
            var allocationConflict = await ValidateOverallAllocationAsync(dto.ResourceId, startDate, endDate, allocation, null, cancellationToken);
            if (allocationConflict is not null) return Result<ProjectPersonDto>.Failure(allocationConflict);
            var reusableMember = await _context.Set<ProjectMember>()
                .IgnoreQueryFilters()
                .Where(member => member.TenantId == project.TenantId && member.ProjectId == projectId && member.ResourceId == dto.ResourceId)
                .OrderByDescending(member => member.UpdatedAt ?? member.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (reusableMember is not null)
            {
                reusableMember.IsDeleted = false;
                reusableMember.Role = Normalize(dto.Role) ?? "Team Member";
                reusableMember.AllocationPercentage = allocation;
                reusableMember.HoursPerWeek = null;
                reusableMember.StartDate = startDate;
                reusableMember.EndDate = endDate;
                reusableMember.Status = "Active";
                reusableMember.IsBillable = dto.IsBillable ?? true;
                if (dto.CalendarPolicy != null && CalendarPolicyConstants.IsValid(dto.CalendarPolicy))
                    reusableMember.CalendarPolicy = dto.CalendarPolicy;
                reusableMember.UpdatedBy = userId;
                await _context.SaveChangesAsync(cancellationToken);
                await SyncFunctionalRolesAsync(reusableMember.Id, dto.ProjectRoleIds, project.TenantId, userId, cancellationToken);
                await LogAsync("ProjectMember", reusableMember.Id, "ReAdd", project.TenantId, userId, cancellationToken);
                return Result<ProjectPersonDto>.Success(ToMemberDto(reusableMember, resource));
            }
            var calendarPolicy = dto.CalendarPolicy != null && CalendarPolicyConstants.IsValid(dto.CalendarPolicy)
                ? dto.CalendarPolicy : CalendarPolicyConstants.UseResourceCalendar;
            var member = new ProjectMember
            {
                TenantId = project.TenantId, ProjectId = projectId, ResourceId = dto.ResourceId,
                Role = Normalize(dto.Role) ?? "Team Member", AllocationPercentage = allocation,
                HoursPerWeek = null, StartDate = startDate, EndDate = endDate,
                Status = "Active", IsBillable = dto.IsBillable ?? true, Notes = null,
                CalendarPolicy = calendarPolicy, CreatedBy = userId
            };
            _context.Set<ProjectMember>().Add(member);
            await _context.SaveChangesAsync(cancellationToken);
            await SyncFunctionalRolesAsync(member.Id, dto.ProjectRoleIds, project.TenantId, userId, cancellationToken);
            await LogAsync("ProjectMember", member.Id, "Create", project.TenantId, userId, cancellationToken);
            return Result<ProjectPersonDto>.Success(ToMemberDto(member, resource));
        }

        if (relationship == ProjectRelationshipType.Sponsor)
        {
            if (!resource.UserId.HasValue) return Result<ProjectPersonDto>.Failure(Error.Validation("A Sponsor must be linked to a system user. Assign the Resource a User in Resource Management first."));
            if (project.SponsorId.HasValue) return Result<ProjectPersonDto>.Failure(Error.Conflict("This project already has a Sponsor"));
            var sponsorError = await _sponsorMembershipSynchronizer.ValidateAssignmentAsync(resource.UserId, project.TenantId, cancellationToken);
            if (sponsorError is not null) return Result<ProjectPersonDto>.Failure(sponsorError);
            project.SponsorId = resource.UserId.Value;
            project.UpdatedBy = userId;
            await _sponsorMembershipSynchronizer.SynchronizeAsync(project, null, project.SponsorId, userId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await LogAsync("Project", project.Id, "AssignSponsor", project.TenantId, userId, cancellationToken);
            return Result<ProjectPersonDto>.Success(ToGovernanceDto(project, resource, ProjectRelationshipType.Sponsor));
        }

        var stakeholderRelationship = relationship == ProjectRelationshipType.Other ? ProjectRelationshipType.Other : ProjectRelationshipType.Stakeholder;
        var duplicate = await _context.Set<ProjectStakeholder>().AnyAsync(s => s.ProjectId == projectId && s.ResourceId == dto.ResourceId && !s.IsDeleted && s.Status == ProjectStakeholderStatus.Active && s.RelationshipType == stakeholderRelationship, cancellationToken);
        if (duplicate) return Result<ProjectPersonDto>.Failure(Error.Conflict("This Resource already has the selected active relationship on the project"));
        var reusableStakeholder = await _context.Set<ProjectStakeholder>()
            .IgnoreQueryFilters()
            .Where(stakeholder => stakeholder.TenantId == project.TenantId && stakeholder.ProjectId == projectId && stakeholder.ResourceId == dto.ResourceId && stakeholder.RelationshipType == stakeholderRelationship)
            .OrderByDescending(stakeholder => stakeholder.UpdatedAt ?? stakeholder.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (reusableStakeholder is not null)
        {
            reusableStakeholder.IsDeleted = false;
            reusableStakeholder.UserId = resource.UserId;
            reusableStakeholder.Name = resource.Name;
            reusableStakeholder.Email = resource.Email;
            reusableStakeholder.NormalizedEmail = NormalizeEmail(resource.Email);
            reusableStakeholder.Role = Normalize(dto.Role) ?? stakeholderRelationship;
            reusableStakeholder.Interest = Normalize(dto.Interest);
            reusableStakeholder.Influence = Normalize(dto.Influence);
            reusableStakeholder.Status = ProjectStakeholderStatus.Active;
            reusableStakeholder.RowVersion = RandomNumberGenerator.GetBytes(8);
            reusableStakeholder.UpdatedBy = userId;
            await _context.SaveChangesAsync(cancellationToken);
            await LogAsync("ProjectStakeholder", reusableStakeholder.Id, "ReAdd", project.TenantId, userId, cancellationToken);
            return Result<ProjectPersonDto>.Success(ToStakeholderDto(reusableStakeholder, resource));
        }
        var entity = new ProjectStakeholder
        {
            TenantId = project.TenantId, ProjectId = projectId, ResourceId = resource.Id, UserId = resource.UserId,
            Name = resource.Name, Email = resource.Email, NormalizedEmail = NormalizeEmail(resource.Email),
            Role = Normalize(dto.Role) ?? stakeholderRelationship, RelationshipType = stakeholderRelationship,
            Interest = Normalize(dto.Interest), Influence = Normalize(dto.Influence),
            Status = ProjectStakeholderStatus.Active, RowVersion = RandomNumberGenerator.GetBytes(8), CreatedBy = userId
        };
        _context.Set<ProjectStakeholder>().Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await LogAsync("ProjectStakeholder", entity.Id, "Create", project.TenantId, userId, cancellationToken);
        return Result<ProjectPersonDto>.Success(ToStakeholderDto(entity, resource));
    }

    public async Task<Result<ProjectPersonDto>> UpdateAsync(Guid projectId, Guid personId, UpdateProjectPersonDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (dto.Source == "Governance") return Result<ProjectPersonDto>.Failure(Error.Validation("Project Manager and Sponsor are governed by Project settings"));
        if (dto.Source == "TeamMember")
        {
            var member = await _context.Set<ProjectMember>().FirstOrDefaultAsync(m => m.Id == personId && m.ProjectId == projectId && !m.IsDeleted, cancellationToken);
            if (member is null) return Result<ProjectPersonDto>.Failure(Error.NotFound("Project person not found"));
            if (dto.RelationshipType is not null && dto.RelationshipType != ProjectRelationshipType.TeamMember) return Result<ProjectPersonDto>.Failure(Error.Validation("Change the relationship by removing this assignment and selecting the Resource again"));
            var start = dto.StartDate ?? member.StartDate; var end = dto.EndDate ?? member.EndDate; var allocation = dto.AllocationPercentage ?? member.AllocationPercentage;
            if (end <= start || allocation < 0 || allocation > 100) return Result<ProjectPersonDto>.Failure(Error.Validation("Invalid team member allocation dates or percentage"));
            var allocationConflict = await ValidateOverallAllocationAsync(member.ResourceId, start, end, allocation, member.Id, cancellationToken);
            if (allocationConflict is not null) return Result<ProjectPersonDto>.Failure(allocationConflict);
            if (dto.Role is not null) member.Role = dto.Role.Trim();
            if (dto.AllocationPercentage.HasValue) member.AllocationPercentage = allocation;
            member.HoursPerWeek = null;
            if (dto.StartDate.HasValue) member.StartDate = start;
            if (dto.EndDate.HasValue) member.EndDate = end;
            if (dto.IsBillable.HasValue) member.IsBillable = dto.IsBillable.Value;
            if (dto.CalendarPolicy != null && CalendarPolicyConstants.IsValid(dto.CalendarPolicy))
                member.CalendarPolicy = dto.CalendarPolicy;
            member.UpdatedBy = userId;
            await _context.SaveChangesAsync(cancellationToken);
            if (dto.ProjectRoleIds != null)
            {
                var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
                if (project != null)
                    await SyncFunctionalRolesAsync(member.Id, dto.ProjectRoleIds, project.TenantId, userId, cancellationToken);
            }
            var resource = await _resources.FindAsync(member.ResourceId, cancellationToken);
            return Result<ProjectPersonDto>.Success(ToMemberDto(member, resource));
        }

        var stakeholder = await _context.Set<ProjectStakeholder>().FirstOrDefaultAsync(s => s.Id == personId && s.ProjectId == projectId && !s.IsDeleted, cancellationToken);
        if (stakeholder is null) return Result<ProjectPersonDto>.Failure(Error.NotFound("Project person not found"));
        if (dto.RelationshipType is not null && dto.RelationshipType != stakeholder.RelationshipType) return Result<ProjectPersonDto>.Failure(Error.Validation("Change the relationship by removing this assignment and selecting the Resource again"));
        if (dto.RowVersion is null || dto.RowVersion.Length == 0) return Result<ProjectPersonDto>.Failure(Error.Validation("RowVersion is required"));
        _context.Entry(stakeholder).Property(x => x.RowVersion).OriginalValue = dto.RowVersion;
        if (dto.Role is not null) stakeholder.Role = dto.Role.Trim();
        if (dto.Interest is not null) stakeholder.Interest = Normalize(dto.Interest);
        if (dto.Influence is not null) stakeholder.Influence = Normalize(dto.Influence);
        stakeholder.UpdatedBy = userId; stakeholder.RowVersion = RandomNumberGenerator.GetBytes(8);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<ProjectPersonDto>.Failure(Error.Conflict("This project relationship was changed by another user")); }
        var updatedResource = stakeholder.ResourceId.HasValue ? await _resources.FindAsync(stakeholder.ResourceId.Value, cancellationToken) : null;
        return Result<ProjectPersonDto>.Success(ToStakeholderDto(stakeholder, updatedResource));
    }

    public async Task<Result<ProjectPersonDto>> RevokeOrRemoveAsync(Guid projectId, Guid personId, string source, byte[]? rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var project = await _context.Set<Project>()
            .FirstOrDefaultAsync(item => item.Id == projectId && !item.IsDeleted, cancellationToken);
        if (project is null) return Result<ProjectPersonDto>.Failure(Error.NotFound("Project not found"));

        if (string.Equals(source, "TeamMember", StringComparison.OrdinalIgnoreCase))
        {
            var member = await _context.Set<ProjectMember>().FirstOrDefaultAsync(m => m.Id == personId && m.ProjectId == projectId && !m.IsDeleted, cancellationToken);
            if (member is null) return Result<ProjectPersonDto>.Failure(Error.NotFound("Project team member not found"));

            var memberResource = await _resources.FindAsync(member.ResourceId, cancellationToken);
            if (project.ProjectManagerId.HasValue && memberResource?.UserId == project.ProjectManagerId.Value)
                return Result<ProjectPersonDto>.Failure(Error.Conflict("This person is the current Project Manager. Complete a Project Handover first."));
            if (ProjectSponsorMembershipSynchronizer.IsManagedMembership(member)
                || (project.SponsorId.HasValue && memberResource?.UserId == project.SponsorId.Value))
                return Result<ProjectPersonDto>.Failure(Error.Conflict("This Project Team relationship is governed by Project Sponsor settings. Change the Sponsor through Project Governance."));

            var blockers = await GetActiveBlockersAsync(projectId, member.ResourceId, memberResource?.UserId, cancellationToken);
            if (blockers.HasAny)
                return Result<ProjectPersonDto>.Failure(Error.Conflict(blockers.ToMessage()));

            member.IsDeleted = true;
            member.Status = "Inactive";
            member.EndDate = DateTime.UtcNow.Date;
            member.UpdatedBy = userId;

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await LogRemovalAsync(project, member.Id, member.ResourceId, memberResource?.UserId, memberResource?.Name ?? "Unknown", ProjectRelationshipType.TeamMember, userId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<ProjectPersonDto>.Success(ToMemberDto(member, memberResource));
        }

        if (string.Equals(source, "Governance", StringComparison.OrdinalIgnoreCase))
        {
            if (personId == StableId(project.Id, ProjectRelationshipType.ProjectManager))
                return Result<ProjectPersonDto>.Failure(Error.Conflict("This person is the current Project Manager. Complete a Project Handover first."));

            if (personId == StableId(project.Id, ProjectRelationshipType.Sponsor) && project.SponsorId.HasValue)
            {
                var previousSponsorId = project.SponsorId.Value;
                var oldResource = await _resources.GetDetailsByUserIdsAsync(new[] { previousSponsorId }, cancellationToken);
                var oldSponsorResource = oldResource.GetValueOrDefault(previousSponsorId);
                var oldSponsorUser = oldSponsorResource is null
                    ? await _context.Set<User>().AsNoTracking().FirstOrDefaultAsync(user => user.Id == previousSponsorId && user.TenantId == project.TenantId && !user.IsDeleted, cancellationToken)
                    : null;
                project.SponsorId = null;
                project.UpdatedBy = userId;
                await _sponsorMembershipSynchronizer.SynchronizeAsync(project, previousSponsorId, null, userId, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await LogAsync("Project", project.Id, "RemoveSponsor", project.TenantId, userId, cancellationToken);
                var removed = oldSponsorResource is not null
                    ? ToGovernanceDto(project, oldSponsorResource, ProjectRelationshipType.Sponsor)
                    : oldSponsorUser is not null
                        ? ToGovernanceDto(project, oldSponsorUser, ProjectRelationshipType.Sponsor)
                        : new ProjectPersonDto { Id = personId, ProjectId = project.Id, Source = "Governance", RelationshipType = ProjectRelationshipType.Sponsor, Role = ProjectRelationshipType.Sponsor };
                removed.Status = "Inactive";
                return Result<ProjectPersonDto>.Success(removed);
            }

            return Result<ProjectPersonDto>.Failure(Error.Validation("Project Manager and Sponsor relationships are governed by Project settings."));
        }
        if (!string.Equals(source, "Stakeholder", StringComparison.OrdinalIgnoreCase))
            return Result<ProjectPersonDto>.Failure(Error.Validation("The project relationship source is invalid."));

        var stakeholder = await _context.Set<ProjectStakeholder>().FirstOrDefaultAsync(s => s.Id == personId && s.ProjectId == projectId && !s.IsDeleted, cancellationToken);
        if (stakeholder is null) return Result<ProjectPersonDto>.Failure(Error.NotFound("Project stakeholder not found"));
        if (rowVersion is null || rowVersion.Length == 0) return Result<ProjectPersonDto>.Failure(Error.Validation("RowVersion is required"));
        _context.Entry(stakeholder).Property(x => x.RowVersion).OriginalValue = rowVersion;
        stakeholder.IsDeleted = true;
        stakeholder.Status = ProjectStakeholderStatus.Revoked;
        stakeholder.UpdatedBy = userId;
        stakeholder.RowVersion = RandomNumberGenerator.GetBytes(8);
        var stakeholderResource = stakeholder.ResourceId.HasValue
            ? await _resources.FindAsync(stakeholder.ResourceId.Value, cancellationToken)
            : null;
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await LogRemovalAsync(project, stakeholder.Id, stakeholder.ResourceId, stakeholder.UserId, stakeholder.Name, stakeholder.RelationshipType, userId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return Result<ProjectPersonDto>.Failure(Error.Conflict("This project relationship was changed by another user")); }
        var resource = stakeholder.ResourceId.HasValue ? await _resources.FindAsync(stakeholder.ResourceId.Value, cancellationToken) : null;
        return Result<ProjectPersonDto>.Success(ToStakeholderDto(stakeholder, resource));
    }

    public async Task<Result> ClearSponsorAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
    {
        var project = await _context.Set<Project>()
            .FirstOrDefaultAsync(item => item.Id == projectId && !item.IsDeleted, cancellationToken);
        if (project is null) return Result.Failure(Error.NotFound("Project not found"));
        if (!project.SponsorId.HasValue) return Result.Success();

        var previousSponsorId = project.SponsorId.Value;
        project.SponsorId = null;
        project.UpdatedBy = userId;
        await _sponsorMembershipSynchronizer.SynchronizeAsync(project, previousSponsorId, null, userId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await LogAsync("Project", project.Id, "RemoveSponsor", project.TenantId, userId, cancellationToken);
        return Result.Success();
    }

    private async Task<RemovalBlockers> GetActiveBlockersAsync(Guid projectId, Guid resourceId, Guid? userId, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var activeAllocations = await _context.Set<ResourceAllocation>().CountAsync(allocation =>
            allocation.ResourceId == resourceId && allocation.ProjectId == projectId && !allocation.IsDeleted &&
            allocation.Status != ResourceAllocation.StatusCancelled && allocation.EndDate.Date >= today, cancellationToken);

        var activePlanAssignments = await _context.Set<PlanAssignment>().CountAsync(assignment =>
            assignment.ResourceId == resourceId && !assignment.IsDeleted &&
            assignment.PlanVersion != null && !assignment.PlanVersion.IsDeleted &&
            assignment.PlanVersion.Type == PlanVersionType.Working && assignment.PlanVersion.Status == PlanVersionStatus.Active &&
            assignment.PlanVersion.Plan != null && !assignment.PlanVersion.Plan.IsDeleted && assignment.PlanVersion.Plan.ProjectId == projectId, cancellationToken);

        var workItemIds = new List<Guid>();
        if (userId.HasValue)
        {
            workItemIds.AddRange(await _context.Set<WorkItem>()
                .Where(item => item.ProjectId == projectId && item.AssignedResourceId == resourceId && !item.IsDeleted && item.CompletedAt == null && item.Status != "Done")
                .Select(item => item.Id)
                .ToListAsync(cancellationToken));
            workItemIds.AddRange(await _context.Set<WorkItemAssignment>()
                .Where(assignment => assignment.UserId == userId.Value && assignment.UnassignedAt == null && !assignment.IsDeleted &&
                                     !assignment.WorkItem.IsDeleted && assignment.WorkItem.ProjectId == projectId && assignment.WorkItem.CompletedAt == null && assignment.WorkItem.Status != "Done")
                .Select(assignment => assignment.WorkItemId)
                .ToListAsync(cancellationToken));
        }

        var activeMilestones = userId.HasValue
            ? await _context.Set<Milestone>().CountAsync(milestone => milestone.ProjectId == projectId && milestone.AssigneeId == userId.Value && !milestone.IsDeleted && milestone.CompletedDate == null, cancellationToken)
            : 0;
        var openRisks = await _context.Set<Risk>().CountAsync(risk =>
            risk.ProjectId == projectId && risk.OwnerResourceId == resourceId && !risk.IsDeleted &&
            risk.Status != RiskStatus.Mitigated && risk.Status != RiskStatus.Closed, cancellationToken);
        var activeRiskActions = await _context.Set<RiskAction>().CountAsync(action =>
            action.OwnerResourceId == resourceId && !action.IsDeleted && action.Status != RiskActionStatus.Completed && action.Status != RiskActionStatus.Cancelled &&
            action.Risk != null && !action.Risk.IsDeleted && action.Risk.ProjectId == projectId && action.Risk.Status != RiskStatus.Mitigated && action.Risk.Status != RiskStatus.Closed, cancellationToken);

        return new RemovalBlockers(
            workItemIds.Distinct().Count() + activePlanAssignments,
            activeAllocations,
            openRisks,
            activeRiskActions,
            activeMilestones);
    }

    private async Task LogRemovalAsync(Project project, Guid relationshipId, Guid? resourceId, Guid? userId, string personName, string relationship, string actorId, CancellationToken cancellationToken)
    {
        await _audit.LogAsync(new AuditEntry
        {
            TenantId = project.TenantId,
            EntityType = "ProjectPerson",
            EntityId = relationshipId,
            Action = "RemoveFromProject",
            UserId = GuidHelper.TryParse(actorId),
            Changes = $"Project Person Removed: {personName}",
            OriginalValues = JsonSerializer.Serialize(new { ProjectId = project.Id, ResourceId = resourceId, UserId = userId, Person = personName, Relationship = relationship, Project = project.Name }),
            Timestamp = DateTime.UtcNow,
            CreatedBy = actorId
        }, cancellationToken);
    }

    private sealed record RemovalBlockers(int ActiveTaskAssignments, int ActiveAllocations, int OpenRisks, int ActiveRiskActions, int ActiveMilestones)
    {
        public bool HasAny => ActiveTaskAssignments > 0 || ActiveAllocations > 0 || OpenRisks > 0 || ActiveRiskActions > 0 || ActiveMilestones > 0;

        public string ToMessage()
        {
            var lines = new List<string> { "Cannot remove this project person because they still have active project responsibilities:" };
            if (ActiveTaskAssignments > 0) lines.Add($"- Active Task Assignments: {ActiveTaskAssignments}");
            if (ActiveAllocations > 0) lines.Add($"- Active Resource Allocations: {ActiveAllocations}");
            if (OpenRisks > 0) lines.Add($"- Open Risks: {OpenRisks}");
            if (ActiveRiskActions > 0) lines.Add($"- Active Risk Actions: {ActiveRiskActions}");
            if (ActiveMilestones > 0) lines.Add($"- Active Milestones: {ActiveMilestones}");
            lines.Add("Reassign or complete these responsibilities before removing the person.");
            return string.Join(Environment.NewLine, lines);
        }
    }

    private async Task LogAsync(string entityType, Guid entityId, string action, Guid tenantId, string userId, CancellationToken cancellationToken) =>
        await _audit.LogAsync(new AuditEntry { TenantId = tenantId, EntityType = entityType, EntityId = entityId, Action = action, UserId = GuidHelper.TryParse(userId), Timestamp = DateTime.UtcNow, CreatedBy = userId }, cancellationToken);

    private async Task<Error?> ValidateOverallAllocationAsync(
        Guid resourceId,
        DateTime startDate,
        DateTime endDate,
        decimal allocationPercentage,
        Guid? excludeMemberId,
        CancellationToken cancellationToken)
    {
        if (_projectService is null) return null;
        var conflicts = await _projectService.CheckResourceConflictsAsync(
            resourceId, startDate, endDate, allocationPercentage, excludeMemberId, cancellationToken);
        if (conflicts.IsFailure) return conflicts.Error;
        if (conflicts.Value is { Count: > 0 })
            return Error.Validation("Resource overallocation detected: combined project allocations would exceed 100%.");
        return null;
    }

    private static ProjectPersonDto ToMemberDto(ProjectMember member, ResourceReference? resource, string relationship = ProjectRelationshipType.TeamMember, List<ProjectRoleRefDto>? functionalRoles = null, IReadOnlyList<string>? additionalRelationships = null) => new()
    {
        Id = member.Id, Source = "TeamMember", ProjectId = member.ProjectId, ResourceId = member.ResourceId,
        Name = resource?.Name ?? "Unknown", Email = resource?.Email, JobTitle = resource?.JobTitle, ResourceType = resource?.Type ?? "",
        UserId = resource?.UserId, RelationshipType = relationship, Status = member.Status,
        AllocationPercentage = member.AllocationPercentage, HoursPerWeek = null, StartDate = member.StartDate, EndDate = member.EndDate,
        IsBillable = member.IsBillable, Role = member.Role, CalendarPolicy = member.CalendarPolicy,
        FunctionalRoles = functionalRoles ?? new(), AdditionalRelationshipTypes = additionalRelationships ?? Array.Empty<string>(), RowVersion = member.RowVersion, CreatedAt = member.CreatedAt, UpdatedAt = member.UpdatedAt
    };

    private static ProjectPersonDto ToStakeholderDto(ProjectStakeholder stakeholder, ResourceReference? resource) => new()
    {
        Id = stakeholder.Id, Source = "Stakeholder", ProjectId = stakeholder.ProjectId, ResourceId = stakeholder.ResourceId,
        Name = resource?.Name ?? stakeholder.Name, Email = resource?.Email ?? stakeholder.Email, JobTitle = resource?.JobTitle, ResourceType = resource?.Type ?? "",
        UserId = resource?.UserId ?? stakeholder.UserId, RelationshipType = stakeholder.RelationshipType, Status = stakeholder.Status.ToString(),
        Role = stakeholder.Role, Interest = stakeholder.Interest, Influence = stakeholder.Influence, IsLegacy = !stakeholder.ResourceId.HasValue,
        RowVersion = stakeholder.RowVersion, CreatedAt = stakeholder.CreatedAt, UpdatedAt = stakeholder.UpdatedAt
    };

    private static ProjectPersonDto ToGovernanceDto(Project project, ResourceReference resource, string relationship) => new()
    {
        Id = StableId(project.Id, relationship), Source = "Governance", ProjectId = project.Id, ResourceId = resource.Id,
        Name = resource.Name, Email = resource.Email, JobTitle = resource.JobTitle, ResourceType = resource.Type, UserId = resource.UserId,
        RelationshipType = relationship, Status = "Active", Role = relationship, CreatedAt = project.CreatedAt, UpdatedAt = project.UpdatedAt
    };

    private static ProjectPersonDto ToGovernanceDto(Project project, User user, string relationship) => new()
    {
        Id = StableId(project.Id, relationship), Source = "Governance", ProjectId = project.Id,
        Name = user.FullName, Email = user.Email, JobTitle = user.JobTitle, ResourceType = "System User",
        UserId = user.Id, RelationshipType = relationship, Status = "Active", Role = relationship,
        CreatedAt = project.CreatedAt, UpdatedAt = project.UpdatedAt
    };

    private static Guid StableId(Guid projectId, string relationship)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{projectId:N}:{relationship}"));
        return new Guid(bytes[..16]);
    }

    private static bool IsProjectManagerRole(string? role) =>
        string.Equals(role?.Trim(), ProjectRelationshipType.ProjectManager, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role?.Trim(), "Project Manager", StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeEmail(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private async Task SyncFunctionalRolesAsync(Guid projectMemberId, List<Guid>? projectRoleIds, Guid tenantId, string userId, CancellationToken cancellationToken)
    {
        var existingRoles = await _context.Set<ProjectMemberRole>()
            .Where(pmr => pmr.ProjectMemberId == projectMemberId && !pmr.IsDeleted)
            .ToListAsync(cancellationToken);

        if (projectRoleIds is null || projectRoleIds.Count == 0)
        {
            foreach (var role in existingRoles)
            {
                role.IsDeleted = true;
                role.UpdatedBy = userId;
            }
            return;
        }

        var existingRoleIds = existingRoles.Select(r => r.ProjectRoleId).ToHashSet();
        var requestedRoleIds = projectRoleIds.ToHashSet();

        foreach (var role in existingRoles.Where(r => !requestedRoleIds.Contains(r.ProjectRoleId)))
        {
            role.IsDeleted = true;
            role.UpdatedBy = userId;
        }

        var isFirst = existingRoles.Count(r => !r.IsDeleted) == 0;
        foreach (var roleId in projectRoleIds.Where(id => !existingRoleIds.Contains(id)))
        {
            _context.Set<ProjectMemberRole>().Add(new ProjectMemberRole
            {
                TenantId = tenantId,
                ProjectMemberId = projectMemberId,
                ProjectRoleId = roleId,
                IsPrimary = isFirst,
                CreatedBy = userId
            });
            isFirst = false;
        }
    }
}
