using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Persistence.Management;

public sealed class ManagementScopeResolver : IManagementScopeResolver
{
    private const string TenantAdministratorRoleName = "admin";
    private readonly AppDbContext _db;

    public ManagementScopeResolver(AppDbContext db) => _db = db;

    public async Task<ManagementPerspectiveSnapshot> GetPerspectiveSnapshotAsync(
        Guid userId,
        Guid tenantId,
        ManagementContext? selectedContext = null,
        CancellationToken cancellationToken = default)
    {
        var assignments = await LoadAssignmentsAsync(userId, tenantId, cancellationToken);
        var native = assignments.NativePerspectives;
        var defaultPerspective = native.OrderByDescending(ManagementPerspectiveRules.Level).FirstOrDefault();
        var scope = await BuildScopeAsync(assignments, defaultPerspective, cancellationToken);

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
                ManagementPerspective.ProgramManager => await GetProgramContextsAsync(assignments, native, cancellationToken),
                ManagementPerspective.ProjectManager => await GetProjectContextsAsync(assignments, native, cancellationToken),
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

        var scopePerspective = mode == ManagementViewMode.ReadOnlyViewAs
            ? assignments.NativePerspectives.OrderByDescending(ManagementPerspectiveRules.Level).First()
            : selectedPerspective;
        var nativeScope = await BuildScopeAsync(assignments, scopePerspective, cancellationToken);

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

    public Task<bool> IsTenantAdministratorAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
        => _db.Set<UserRole>().IgnoreQueryFilters().AsNoTracking().AnyAsync(assignment =>
            assignment.UserId == userId && assignment.TenantId == tenantId && !assignment.IsDeleted &&
            assignment.User.TenantId == tenantId && assignment.User.IsActive && !assignment.User.IsDeleted &&
            assignment.Role.TenantId == tenantId && assignment.Role.IsSystem &&
            assignment.Role.Name == TenantAdministratorRoleName && !assignment.Role.IsDeleted,
            cancellationToken);

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
            ManagementPerspective.PortfolioManager => await _db.Set<Project>().Where(x => x.TenantId == assignments.TenantId && ((x.ProgramId.HasValue && programIds.Contains(x.ProgramId.Value)) || (!x.ProgramId.HasValue && x.PortfolioId.HasValue && portfolioIds.Contains(x.PortfolioId.Value)))).Select(x => x.Id).ToHashSetAsync(cancellationToken),
            ManagementPerspective.ProgramManager => await _db.Set<Project>().Where(x => x.TenantId == assignments.TenantId && x.ProgramId.HasValue && programIds.Contains(x.ProgramId.Value)).Select(x => x.Id).ToHashSetAsync(cancellationToken),
            ManagementPerspective.ProjectManager => assignments.Projects.Select(x => x.Id).ToHashSet(),
            _ => new HashSet<Guid>()
        };
        return new ManagementScope(portfolioIds, programIds, projectIds);
    }

    private async Task<ManagementScope> BuildProgramContextScopeAsync(Guid programId, ManagementScope inheritedScope, CancellationToken cancellationToken)
    {
        var program = await _db.Set<Program>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == programId && inheritedScope.ProgramIds.Contains(x.Id), cancellationToken);
        if (program is null) return ManagementScope.Empty;
        var projects = await _db.Set<Project>().Where(x => x.ProgramId == programId).Select(x => x.Id).ToHashSetAsync(cancellationToken);
        return new ManagementScope(new HashSet<Guid> { program.PortfolioId }, new HashSet<Guid> { programId }, projects);
    }

    private async Task<ManagementScope> BuildProjectContextScopeAsync(Guid projectId, ManagementScope inheritedScope, CancellationToken cancellationToken)
    {
        var project = await _db.Set<Project>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId && inheritedScope.ProjectIds.Contains(x.Id), cancellationToken);
        if (project is null) return ManagementScope.Empty;
        var programs = project.ProgramId.HasValue ? new HashSet<Guid> { project.ProgramId.Value } : new HashSet<Guid>();
        var portfolios = project.PortfolioId.HasValue ? new HashSet<Guid> { project.PortfolioId.Value } : new HashSet<Guid>();
        return new ManagementScope(portfolios, programs, new HashSet<Guid> { projectId });
    }

    private async Task<IReadOnlyList<ManagementEntityReference>> GetProgramContextsAsync(AssignmentSet assignments, IReadOnlySet<ManagementPerspective> native, CancellationToken cancellationToken)
    {
        var scope = await BuildScopeAsync(assignments, native.Contains(ManagementPerspective.PortfolioManager) ? ManagementPerspective.PortfolioManager : ManagementPerspective.ProgramManager, cancellationToken);
        var programIds = scope.ProgramIds.ToHashSet();
        if (native.Contains(ManagementPerspective.ProgramManager))
        {
            foreach (var program in assignments.Programs) programIds.Add(program.Id);
        }
        return await _db.Set<Program>().AsNoTracking().Where(x => x.TenantId == assignments.TenantId && programIds.Contains(x.Id)).OrderBy(x => x.Name).ThenBy(x => x.Code).ThenBy(x => x.Id).Select(x => new ManagementEntityReference(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ManagementEntityReference>> GetProjectContextsAsync(AssignmentSet assignments, IReadOnlySet<ManagementPerspective> native, CancellationToken cancellationToken)
    {
        var scope = native.Contains(ManagementPerspective.PortfolioManager)
            ? await BuildScopeAsync(assignments, ManagementPerspective.PortfolioManager, cancellationToken)
            : native.Contains(ManagementPerspective.ProgramManager)
                ? await BuildScopeAsync(assignments, ManagementPerspective.ProgramManager, cancellationToken)
                : await BuildScopeAsync(assignments, ManagementPerspective.ProjectManager, cancellationToken);
        var projectIds = scope.ProjectIds.ToHashSet();
        if (native.Contains(ManagementPerspective.ProjectManager))
        {
            foreach (var project in assignments.Projects) projectIds.Add(project.Id);
        }
        return await _db.Set<Project>().AsNoTracking().Where(x => x.TenantId == assignments.TenantId && projectIds.Contains(x.Id)).OrderBy(x => x.Name).ThenBy(x => x.Code).ThenBy(x => x.Id).Select(x => new ManagementEntityReference(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken);
    }

    private async Task<AssignmentSet> LoadAssignmentsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var portfolios = await _db.Set<Portfolio>().AsNoTracking().Where(x => x.TenantId == tenantId && (x.OwnerId == userId || _db.Set<PortfolioManagerAssignment>().Any(a => a.TenantId == tenantId && a.PortfolioId == x.Id && a.PortfolioManagerId == userId && a.IsCurrent && !a.IsDeleted))).OrderBy(x => x.Name).ThenBy(x => x.Code).ThenBy(x => x.Id).Select(x => new ManagementEntityReference(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken);
        var programs = await _db.Set<Program>().AsNoTracking().Where(x => x.TenantId == tenantId && (x.ProgramManagerId == userId || _db.Set<ProgramManagerAssignment>().Any(a => a.TenantId == tenantId && a.ProgramId == x.Id && a.ProgramManagerId == userId && a.IsCurrent && !a.IsDeleted))).OrderBy(x => x.Name).ThenBy(x => x.Code).ThenBy(x => x.Id).Select(x => new ManagementEntityReference(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken);
        var projects = await _db.Set<Project>().AsNoTracking().Where(x => x.TenantId == tenantId && (x.ProjectManagerId == userId || _db.Set<ProjectManagerAssignment>().Any(a => a.TenantId == tenantId && a.ProjectId == x.Id && a.ProjectManagerId == userId && a.IsCurrent && !a.IsDeleted) || _db.Set<ProjectMember>().Any(m => m.ProjectId == x.Id && m.Status == "Active" && !m.IsDeleted && _db.Set<Resource>().Any(r => r.Id == m.ResourceId && r.UserId == userId && !r.IsDeleted)))).OrderBy(x => x.Name).ThenBy(x => x.Code).ThenBy(x => x.Id).Select(x => new ManagementEntityReference(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken);
        var native = new HashSet<ManagementPerspective>();
        if (portfolios.Count > 0) native.Add(ManagementPerspective.PortfolioManager);
        if (programs.Count > 0) native.Add(ManagementPerspective.ProgramManager);
        if (projects.Count > 0) native.Add(ManagementPerspective.ProjectManager);
        return new AssignmentSet(tenantId, portfolios, programs, projects, native);
    }

    private sealed record AssignmentSet(Guid TenantId, IReadOnlyList<ManagementEntityReference> Portfolios, IReadOnlyList<ManagementEntityReference> Programs, IReadOnlyList<ManagementEntityReference> Projects, IReadOnlySet<ManagementPerspective> NativePerspectives);
}
