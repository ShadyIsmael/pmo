using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Persistence.Management;

public sealed class ManagementScopeResolver : IManagementScopeResolver
{
    private readonly AppDbContext _db;
    private readonly IUserEffectivePermissionService _effectivePermissions;

    public ManagementScopeResolver(AppDbContext db, IUserEffectivePermissionService effectivePermissions, IProjectOrganizationLookup organizationLookup)
    {
        _db = db;
        _effectivePermissions = effectivePermissions;
        _organizationLookup = organizationLookup;
    }

    private readonly IProjectOrganizationLookup _organizationLookup;

    public async Task<ManagementPerspectiveSnapshot> GetPerspectiveSnapshotAsync(
        Guid userId,
        Guid tenantId,
        ManagementContext? selectedContext = null,
        CancellationToken cancellationToken = default)
    {
        var assignments = await LoadAssignmentsAsync(userId, tenantId, cancellationToken);
        var native = assignments.NativePerspectives;
        var defaultPerspective = native.OrderByDescending(ManagementPerspectiveRules.Level).FirstOrDefault();
        var scope = await BuildNativeScopeAsync(assignments, cancellationToken);

        var available = new List<ManagementPerspectiveAvailability>();
        foreach (var perspective in new[]
        {
            ManagementPerspective.PortfolioManager,
            ManagementPerspective.ProgramManager,
            ManagementPerspective.ProjectManager
        })
        {
            var isNative = native.Contains(perspective);
            var canViewAs = native.Any(nativePerspective => ManagementPerspectiveRules.IsLower(nativePerspective, perspective));
            if (!isNative && !canViewAs) continue;

            var contexts = perspective switch
            {
                ManagementPerspective.PortfolioManager => assignments.Portfolios,
                ManagementPerspective.ProgramManager => await GetProgramContextsAsync(assignments, cancellationToken),
                ManagementPerspective.ProjectManager => await GetProjectContextsAsync(assignments, cancellationToken),
                _ => Array.Empty<ManagementEntityReference>()
            };
            available.Add(new ManagementPerspectiveAvailability(
                perspective,
                isNative,
                perspective != ManagementPerspective.PortfolioManager,
                contexts));
        }

        return new ManagementPerspectiveSnapshot(native.OrderByDescending(ManagementPerspectiveRules.Level).ToList(), available, native.Count == 0 ? null : defaultPerspective, native.Any(p => ManagementPerspectiveRules.Level(p) > 1), scope, selectedContext);
    }

    public async Task<ManagementContext?> ResolveAsync(
        Guid userId,
        Guid tenantId,
        ManagementPerspective? perspective,
        ManagementViewMode mode,
        Guid? portfolioId,
        Guid? programId,
        Guid? projectId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await LoadAssignmentsAsync(userId, tenantId, cancellationToken);
        var selectedPerspective = perspective ?? assignments.NativePerspectives.OrderByDescending(ManagementPerspectiveRules.Level).FirstOrDefault();
        var nativeSelected = assignments.NativePerspectives.Contains(selectedPerspective);
        var canViewAs = assignments.NativePerspectives.Any(nativePerspective => ManagementPerspectiveRules.IsLower(nativePerspective, selectedPerspective));
        if ((mode == ManagementViewMode.Native && !nativeSelected) || (mode == ManagementViewMode.ReadOnlyViewAs && !canViewAs))
        {
            return null;
        }

        var nativeScope = await BuildNativeScopeAsync(assignments, cancellationToken);

        if (selectedPerspective == ManagementPerspective.PortfolioManager && portfolioId.HasValue && !nativeScope.PortfolioIds.Contains(portfolioId.Value)) return null;
        if (selectedPerspective == ManagementPerspective.ProgramManager && programId.HasValue && !nativeScope.ProgramIds.Contains(programId.Value)) return null;
        if (selectedPerspective == ManagementPerspective.ProjectManager && projectId.HasValue && !nativeScope.ProjectIds.Contains(projectId.Value)) return null;

        if (mode == ManagementViewMode.ReadOnlyViewAs)
        {
            if (selectedPerspective == ManagementPerspective.ProgramManager && !programId.HasValue) return null;
            if (selectedPerspective == ManagementPerspective.ProjectManager && !projectId.HasValue) return null;
        }

        var scope = selectedPerspective switch
        {
            ManagementPerspective.PortfolioManager => nativeScope,
            ManagementPerspective.ProgramManager when programId.HasValue => await BuildProgramContextScopeAsync(programId.Value, nativeScope, cancellationToken),
            ManagementPerspective.ProjectManager when projectId.HasValue => await BuildProjectContextScopeAsync(projectId.Value, nativeScope, cancellationToken),
            _ => nativeScope
        };

        return new ManagementContext(userId, tenantId, selectedPerspective, mode, portfolioId, programId, projectId, scope);
    }

    public async Task<bool> CanAccessPortfolioAsync(Guid userId, Guid tenantId, Guid portfolioId, CancellationToken cancellationToken = default)
        => (await BuildScopeAsync(await LoadAssignmentsAsync(userId, tenantId, cancellationToken), ManagementPerspective.PortfolioManager, cancellationToken)).PortfolioIds.Contains(portfolioId);

    public async Task<bool> CanAccessProgramAsync(Guid userId, Guid tenantId, Guid programId, CancellationToken cancellationToken = default)
    {
        var assignments = await LoadAssignmentsAsync(userId, tenantId, cancellationToken);
        var scope = await BuildScopeAsync(assignments, ManagementPerspective.PortfolioManager, cancellationToken);
        return scope.ProgramIds.Contains(programId) || (await BuildScopeAsync(assignments, ManagementPerspective.ProgramManager, cancellationToken)).ProgramIds.Contains(programId);
    }

    public async Task<bool> CanAccessProjectAsync(Guid userId, Guid tenantId, Guid projectId, CancellationToken cancellationToken = default)
    {
        var assignments = await LoadAssignmentsAsync(userId, tenantId, cancellationToken);
        var portfolioScope = await BuildScopeAsync(assignments, ManagementPerspective.PortfolioManager, cancellationToken);
        var programScope = await BuildScopeAsync(assignments, ManagementPerspective.ProgramManager, cancellationToken);
        var projectScope = await BuildScopeAsync(assignments, ManagementPerspective.ProjectManager, cancellationToken);
        return portfolioScope.ProjectIds.Contains(projectId) || programScope.ProjectIds.Contains(projectId) || projectScope.ProjectIds.Contains(projectId);
    }

    public async Task<bool> IsTenantAdministratorAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty)
            return false;

        var userIsActive = await _db.Set<User>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.TenantId == tenantId && user.IsActive && !user.IsDeleted, cancellationToken);
        if (!userIsActive)
            return false;

        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken);
        return permissions.Contains("TenantAdministration.ManageScope");
    }

    private async Task<ManagementScope> BuildScopeAsync(AssignmentSet assignments, ManagementPerspective perspective, CancellationToken cancellationToken)
    {
        var portfolioIds = perspective == ManagementPerspective.PortfolioManager ? assignments.Portfolios.Select(x => x.Id).ToHashSet() : new HashSet<Guid>();
        var programIds = perspective switch
        {
            ManagementPerspective.PortfolioManager => await _db.Set<Program>().Where(x => x.TenantId == assignments.TenantId && portfolioIds.Contains(x.PortfolioId)).Select(x => x.Id).ToHashSetAsync(cancellationToken),
            ManagementPerspective.ProgramManager => assignments.Programs.Select(x => x.Id).ToHashSet(),
            _ => new HashSet<Guid>()
        };
        var projectIds = perspective switch
        {
            ManagementPerspective.PortfolioManager => (await _db.Set<Project>().Where(x => x.TenantId == assignments.TenantId && ((x.ProgramId.HasValue && programIds.Contains(x.ProgramId.Value)) || (!x.ProgramId.HasValue && x.PortfolioId.HasValue && portfolioIds.Contains(x.PortfolioId.Value)))).Select(x => x.Id).ToHashSetAsync(cancellationToken)).Union(assignments.Projects.Select(x => x.Id)).ToHashSet(),
            ManagementPerspective.ProgramManager => (await _db.Set<Project>().Where(x => x.TenantId == assignments.TenantId && x.ProgramId.HasValue && programIds.Contains(x.ProgramId.Value)).Select(x => x.Id).ToHashSetAsync(cancellationToken)).Union(assignments.Projects.Select(x => x.Id)).ToHashSet(),
            ManagementPerspective.ProjectManager => assignments.Projects.Select(x => x.Id).ToHashSet(),
            _ => new HashSet<Guid>()
        };
        // Organization capability scope is the union of the user's native assignments.
        // It is independent from the currently selected Portfolio/Program/Project context
        // and does not widen the corresponding record-id sets above.
        var organizationPortfolioIds = assignments.Portfolios.Select(item => item.Id).ToHashSet();
        var assignedProgramIds = assignments.Programs.Select(item => item.Id).ToArray();
        if (assignedProgramIds.Length > 0)
        {
            var assignedProgramPortfolios = await _db.Set<Program>().AsNoTracking()
                .Where(program => program.TenantId == assignments.TenantId && assignedProgramIds.Contains(program.Id))
                .Select(program => program.PortfolioId)
                .ToListAsync(cancellationToken);
            organizationPortfolioIds.UnionWith(assignedProgramPortfolios);
        }
        var assignedProjectIds = assignments.Projects.Select(item => item.Id).ToArray();
        if (assignedProjectIds.Length > 0)
        {
            var projectRows = await _db.Set<Project>().AsNoTracking()
                .Where(project => project.TenantId == assignments.TenantId && assignedProjectIds.Contains(project.Id))
                .Select(project => new { project.PortfolioId, project.ProgramId })
                .ToListAsync(cancellationToken);
            organizationPortfolioIds.UnionWith(projectRows.Where(row => row.PortfolioId.HasValue)
                .Select(row => row.PortfolioId!.Value));
            var projectProgramIds = projectRows.Where(row => row.ProgramId.HasValue)
                .Select(row => row.ProgramId!.Value).Distinct().ToArray();
            if (projectProgramIds.Length > 0)
            {
                var programPortfolios = await _db.Set<Program>().AsNoTracking()
                    .Where(program => program.TenantId == assignments.TenantId && projectProgramIds.Contains(program.Id))
                    .Select(program => program.PortfolioId)
                    .ToListAsync(cancellationToken);
                organizationPortfolioIds.UnionWith(programPortfolios);
            }
        }

        var projectOrganizationIds = await GetProjectOrganizationIdsAsync(assignments.TenantId, projectIds, cancellationToken);
        var isTenantAdministrator = await IsTenantAdministratorAsync(assignments.UserId, assignments.TenantId, cancellationToken);
        var organizationIds = isTenantAdministrator
            ? await _db.Set<Organization>().AsNoTracking()
                .Where(organization => organization.TenantId == assignments.TenantId && !organization.IsDeleted)
                .Select(organization => organization.Id).ToHashSetAsync(cancellationToken)
            : organizationPortfolioIds.Count == 0 ? new HashSet<Guid>()
                : await _db.Set<Portfolio>().AsNoTracking()
                    .Where(portfolio => portfolio.TenantId == assignments.TenantId
                        && organizationPortfolioIds.Contains(portfolio.Id) && portfolio.OrganizationId.HasValue)
                    .Select(portfolio => portfolio.OrganizationId!.Value).ToHashSetAsync(cancellationToken);
        if (!isTenantAdministrator)
            organizationIds.UnionWith(projectOrganizationIds.Values);

        var organizationReferences = await GetOrganizationReferencesAsync(assignments.TenantId, organizationIds, cancellationToken);
        return new ManagementScope(portfolioIds, programIds, projectIds)
        {
            OrganizationIds = organizationIds,
            OrganizationReferences = organizationReferences,
            ProjectOrganizationIds = projectOrganizationIds
        };
    }

    private async Task<ManagementScope> BuildNativeScopeAsync(AssignmentSet assignments, CancellationToken cancellationToken)
    {
        var defaultPerspective = assignments.NativePerspectives
            .OrderByDescending(ManagementPerspectiveRules.Level)
            .FirstOrDefault();
        var scope = await BuildScopeAsync(assignments, defaultPerspective, cancellationToken);
        var programIds = scope.ProgramIds.Concat(assignments.Programs.Select(item => item.Id)).ToHashSet();
        var projectIds = scope.ProjectIds.Concat(assignments.Projects.Select(item => item.Id)).ToHashSet();
        var assignedProgramIds = assignments.Programs.Select(item => item.Id).ToArray();
        if (assignedProgramIds.Length > 0)
        {
            var programProjectIds = await _db.Set<Project>().AsNoTracking()
                .Where(project => project.TenantId == assignments.TenantId
                    && project.ProgramId.HasValue && assignedProgramIds.Contains(project.ProgramId.Value))
                .Select(project => project.Id)
                .ToListAsync(cancellationToken);
            projectIds.UnionWith(programProjectIds);
        }

        return new ManagementScope(
            scope.PortfolioIds,
            programIds,
            projectIds)
        {
            OrganizationIds = scope.OrganizationIds,
            OrganizationReferences = scope.OrganizationReferences,
            ProjectOrganizationIds = scope.ProjectOrganizationIds
        };
    }

    private async Task<ManagementScope> BuildProgramContextScopeAsync(Guid programId, ManagementScope inheritedScope, CancellationToken cancellationToken)
    {
        var program = await _db.Set<Program>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == programId && inheritedScope.ProgramIds.Contains(x.Id), cancellationToken);
        if (program is null) return ManagementScope.Empty;
        var projects = await _db.Set<Project>().Where(x => x.ProgramId == programId).Select(x => x.Id).ToHashSetAsync(cancellationToken);
        var organizationIds = await _db.Set<Portfolio>().AsNoTracking()
            .Where(portfolio => portfolio.TenantId == program.TenantId && portfolio.Id == program.PortfolioId && portfolio.OrganizationId.HasValue)
            .Select(portfolio => portfolio.OrganizationId!.Value).ToHashSetAsync(cancellationToken);
        var projectOrganizationIds = await GetProjectOrganizationIdsAsync(program.TenantId, projects, cancellationToken);
        organizationIds.UnionWith(projectOrganizationIds.Values);
        var organizationReferences = await GetOrganizationReferencesAsync(program.TenantId, organizationIds, cancellationToken);
        return new ManagementScope(new HashSet<Guid> { program.PortfolioId }, new HashSet<Guid> { programId }, projects)
        {
            OrganizationIds = organizationIds,
            OrganizationReferences = organizationReferences,
            ProjectOrganizationIds = projectOrganizationIds
        };
    }

    private async Task<ManagementScope> BuildProjectContextScopeAsync(Guid projectId, ManagementScope inheritedScope, CancellationToken cancellationToken)
    {
        var project = await _db.Set<Project>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId && inheritedScope.ProjectIds.Contains(x.Id), cancellationToken);
        if (project is null) return ManagementScope.Empty;
        var programs = project.ProgramId.HasValue ? new HashSet<Guid> { project.ProgramId.Value } : new HashSet<Guid>();
        var portfolios = project.PortfolioId.HasValue
            ? new HashSet<Guid> { project.PortfolioId.Value }
            : project.ProgramId.HasValue
                ? await _db.Set<Program>().AsNoTracking()
                    .Where(program => program.TenantId == project.TenantId && program.Id == project.ProgramId.Value)
                    .Select(program => program.PortfolioId)
                    .ToHashSetAsync(cancellationToken)
                : new HashSet<Guid>();
        var organizationIds = portfolios.Count == 0 ? new HashSet<Guid>() : await _db.Set<Portfolio>().AsNoTracking()
            .Where(portfolio => portfolio.TenantId == project.TenantId && portfolios.Contains(portfolio.Id) && portfolio.OrganizationId.HasValue)
            .Select(portfolio => portfolio.OrganizationId!.Value).ToHashSetAsync(cancellationToken);
        var projectOrganizationIds = await GetProjectOrganizationIdsAsync(project.TenantId, new HashSet<Guid> { projectId }, cancellationToken);
        organizationIds.UnionWith(projectOrganizationIds.Values);
        var organizationReferences = await GetOrganizationReferencesAsync(project.TenantId, organizationIds, cancellationToken);
        return new ManagementScope(portfolios, programs, new HashSet<Guid> { projectId })
        {
            OrganizationIds = organizationIds,
            OrganizationReferences = organizationReferences,
            ProjectOrganizationIds = projectOrganizationIds
        };
    }

    private async Task<IReadOnlyList<ManagementEntityReference>> GetOrganizationReferencesAsync(Guid tenantId, IReadOnlySet<Guid> organizationIds, CancellationToken cancellationToken)
    {
        if (organizationIds.Count == 0) return Array.Empty<ManagementEntityReference>();
        var allowedIds = organizationIds.ToArray();
        return await _db.Set<Organization>().AsNoTracking()
            .Where(organization => organization.TenantId == tenantId && allowedIds.Contains(organization.Id)
                && organization.IsActive && !organization.IsDeleted)
            .OrderBy(organization => organization.Name).ThenBy(organization => organization.Id)
            .Select(organization => new ManagementEntityReference(organization.Id, organization.Name))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, Guid>> GetProjectOrganizationIdsAsync(Guid tenantId, IReadOnlySet<Guid> projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0) return new Dictionary<Guid, Guid>();
        var result = new Dictionary<Guid, Guid>();
        foreach (var projectId in projectIds)
        {
            var organizationId = await _organizationLookup.ResolveOrganizationIdAsync(tenantId, projectId, cancellationToken);
            if (organizationId.HasValue)
                result[projectId] = organizationId.Value;
        }
        return result;
    }

    private async Task<IReadOnlyList<ManagementEntityReference>> GetProgramContextsAsync(AssignmentSet assignments, CancellationToken cancellationToken)
    {
        var scope = await BuildNativeScopeAsync(assignments, cancellationToken);
        var programIds = scope.ProgramIds;
        return await _db.Set<Program>().AsNoTracking().Where(x => x.TenantId == assignments.TenantId && programIds.Contains(x.Id)).OrderBy(x => x.Name).ThenBy(x => x.Code).ThenBy(x => x.Id).Select(x => new ManagementEntityReference(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ManagementEntityReference>> GetProjectContextsAsync(AssignmentSet assignments, CancellationToken cancellationToken)
    {
        var scope = await BuildNativeScopeAsync(assignments, cancellationToken);
        var projectIds = scope.ProjectIds;
        return await _db.Set<Project>().AsNoTracking().Where(x => x.TenantId == assignments.TenantId && projectIds.Contains(x.Id)).OrderBy(x => x.Name).ThenBy(x => x.Code).ThenBy(x => x.Id).Select(x => new ManagementEntityReference(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken);
    }

    private async Task<AssignmentSet> LoadAssignmentsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var isActiveTenantUser = await _db.Set<User>().AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.TenantId == tenantId && user.IsActive && !user.IsDeleted, cancellationToken);
        var portfolios = await _db.Set<Portfolio>().AsNoTracking().Where(x => x.TenantId == tenantId && (x.OwnerId == userId || _db.Set<PortfolioManagerAssignment>().Any(a => a.TenantId == tenantId && a.PortfolioId == x.Id && a.PortfolioManagerId == userId && a.IsCurrent && !a.IsDeleted))).OrderBy(x => x.Name).ThenBy(x => x.Code).ThenBy(x => x.Id).Select(x => new ManagementEntityReference(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken);
        var programs = await _db.Set<Program>().AsNoTracking().Where(x => x.TenantId == tenantId && (x.ProgramManagerId == userId || _db.Set<ProgramManagerAssignment>().Any(a => a.TenantId == tenantId && a.ProgramId == x.Id && a.ProgramManagerId == userId && a.IsCurrent && !a.IsDeleted))).OrderBy(x => x.Name).ThenBy(x => x.Code).ThenBy(x => x.Id).Select(x => new ManagementEntityReference(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken);
        var today = DateTime.UtcNow.Date;
        var projects = await _db.Set<Project>().AsNoTracking().Where(x => x.TenantId == tenantId && (x.ProjectManagerId == userId
            || (isActiveTenantUser && x.SponsorId == userId)
            || _db.Set<ProjectManagerAssignment>().Any(a => a.TenantId == tenantId && a.ProjectId == x.Id && a.ProjectManagerId == userId && a.IsCurrent && !a.IsDeleted)
            || _db.Set<ProjectMember>().Any(m => m.TenantId == tenantId && m.ProjectId == x.Id && m.Status == "Active" && !m.IsDeleted
                && m.StartDate.Date <= today && m.EndDate.Date >= today
                && ((m.ResourceId.HasValue && _db.Set<Resource>().Any(r => r.TenantId == tenantId && r.Id == m.ResourceId.Value
                    && r.UserId == userId && r.IsActive && !r.IsDeleted))
                    || (m.TeamId.HasValue && _db.Set<Team>().Any(team => team.TenantId == tenantId && team.Id == m.TeamId.Value && team.IsActive && !team.IsDeleted
                        && _db.Set<TeamMember>().Any(tm => tm.TenantId == tenantId && tm.TeamId == team.Id && tm.IsActive && !tm.IsDeleted
                            && tm.EffectiveFrom.Date <= today && (!tm.EffectiveTo.HasValue || tm.EffectiveTo.Value.Date >= today)
                            && _db.Set<Resource>().Any(r => r.TenantId == tenantId && r.Id == tm.ResourceId && r.UserId == userId
                                && r.IsActive && !r.IsDeleted)))))))).OrderBy(x => x.Name).ThenBy(x => x.Code).ThenBy(x => x.Id)
            .Select(x => new ManagementEntityReference(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken);

        // Organization Role membership contributes resource visibility only when
        // the effective permission is evaluated for that Role's exact owner.
        // This is deliberately materialized into the same row-id scope consumed
        // by portfolio/program/project queries; permission claims alone never
        // turn a tenant-wide collection into an unrestricted query.
        var organizationRoleOwners = await (
                from assignment in _db.Set<UserRole>().IgnoreQueryFilters().AsNoTracking()
                join role in _db.Set<Role>().IgnoreQueryFilters().AsNoTracking()
                    on new { assignment.TenantId, assignment.RoleId }
                    equals new { role.TenantId, RoleId = role.Id }
                join organization in _db.Set<Organization>().IgnoreQueryFilters().AsNoTracking()
                    on new { role.TenantId, OrganizationId = role.OrganizationId!.Value }
                    equals new { organization.TenantId, OrganizationId = organization.Id }
                where assignment.UserId == userId && assignment.TenantId == tenantId && !assignment.IsDeleted
                    && assignment.User.TenantId == tenantId && assignment.User.IsActive && !assignment.User.IsDeleted
                    && role.TenantId == tenantId && !role.IsDeleted && !role.IsLegacyOrganizationOwnershipUnresolved
                    && role.OrganizationId.HasValue
                    && organization.IsActive && !organization.IsDeleted
                select new { OrganizationId = organization.Id, organization.Name })
            .Distinct()
            .ToListAsync(cancellationToken);

        var organizationPermissions = new Dictionary<Guid, IReadOnlySet<string>>();
        foreach (var owner in organizationRoleOwners)
        {
            organizationPermissions[owner.OrganizationId] = await _effectivePermissions.GetEffectivePermissionsForOrganizationAsync(
                userId, tenantId, owner.OrganizationId, cancellationToken);
        }

        var portfolioViewOrganizations = organizationPermissions
            .Where(item => item.Value.Contains("Portfolio.View"))
            .Select(item => item.Key).ToArray();
        if (portfolioViewOrganizations.Length > 0)
        {
            var organizationPortfolios = await _db.Set<Portfolio>().IgnoreQueryFilters().AsNoTracking()
                .Where(portfolio => portfolio.TenantId == tenantId && !portfolio.IsDeleted
                    && portfolio.OrganizationId.HasValue && portfolioViewOrganizations.Contains(portfolio.OrganizationId.Value))
                .OrderBy(portfolio => portfolio.Name).ThenBy(portfolio => portfolio.Code).ThenBy(portfolio => portfolio.Id)
                .Select(portfolio => new ManagementEntityReference(portfolio.Id, portfolio.Name, portfolio.Code))
                .ToListAsync(cancellationToken);
            portfolios.AddRange(organizationPortfolios);
        }

        var programViewOrganizations = organizationPermissions
            .Where(item => item.Value.Contains("Program.View"))
            .Select(item => item.Key).ToArray();
        if (programViewOrganizations.Length > 0)
        {
            var organizationPrograms = await (
                    from program in _db.Set<Program>().IgnoreQueryFilters().AsNoTracking()
                    join portfolio in _db.Set<Portfolio>().IgnoreQueryFilters().AsNoTracking()
                        on new { program.TenantId, PortfolioId = program.PortfolioId }
                        equals new { portfolio.TenantId, PortfolioId = portfolio.Id }
                    where program.TenantId == tenantId && !program.IsDeleted && !portfolio.IsDeleted
                        && portfolio.OrganizationId.HasValue
                        && programViewOrganizations.Contains(portfolio.OrganizationId.Value)
                    orderby program.Name, program.Code, program.Id
                    select new ManagementEntityReference(program.Id, program.Name, program.Code))
                .ToListAsync(cancellationToken);
            programs.AddRange(organizationPrograms);
        }

        var projectViewOrganizations = organizationPermissions
            .Where(item => item.Value.Contains("Project.View"))
            .Select(item => item.Key).ToHashSet();
        if (projectViewOrganizations.Count > 0)
        {
            var tenantProjects = await _db.Set<Project>().IgnoreQueryFilters().AsNoTracking()
                .Where(project => project.TenantId == tenantId && !project.IsDeleted)
                .OrderBy(project => project.Name).ThenBy(project => project.Code).ThenBy(project => project.Id)
                .Select(project => new ManagementEntityReference(project.Id, project.Name, project.Code))
                .ToListAsync(cancellationToken);
            foreach (var project in tenantProjects)
            {
                var ownerOrganizationId = await _organizationLookup.ResolveOrganizationIdAsync(
                    tenantId, project.Id, cancellationToken);
                if (ownerOrganizationId.HasValue && projectViewOrganizations.Contains(ownerOrganizationId.Value))
                    projects.Add(project);
            }
        }

        portfolios = portfolios.GroupBy(item => item.Id).Select(group => group.First()).ToList();
        programs = programs.GroupBy(item => item.Id).Select(group => group.First()).ToList();
        projects = projects.GroupBy(item => item.Id).Select(group => group.First()).ToList();

        var native = new HashSet<ManagementPerspective>();
        if (portfolios.Count > 0) native.Add(ManagementPerspective.PortfolioManager);
        if (programs.Count > 0) native.Add(ManagementPerspective.ProgramManager);
        if (projects.Count > 0) native.Add(ManagementPerspective.ProjectManager);
        return new AssignmentSet(userId, tenantId, portfolios, programs, projects, native);
    }

    private sealed record AssignmentSet(Guid UserId, Guid TenantId, IReadOnlyList<ManagementEntityReference> Portfolios, IReadOnlyList<ManagementEntityReference> Programs, IReadOnlyList<ManagementEntityReference> Projects, IReadOnlySet<ManagementPerspective> NativePerspectives);
}
