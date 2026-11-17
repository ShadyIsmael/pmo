using System.Security.Claims;
using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Modules.ProjectManagement.Application;

/// <summary>
/// Tenant/Organization scoped lifecycle reference data. Labels are presentation
/// data; IDs and stable codes are the persisted identities used by projects and
/// the existing Phase Gate configuration.
/// </summary>
public sealed class ProjectLifecycleCatalogService : IProjectLifecycleCatalogService, IOrganizationLifecycleHandler
{
    private const string PreInitiationNormalizedCode = "PRE-INITIATION";
    private const string PreInitiationCode = "pre-initiation";
    private const string PreInitiationNameEn = "Pre-Initiation";
    private const string PreInitiationNameAr = "مرحلة ما قبل البدء";
    private const int PreInitiationDisplayOrder = 0;
    
    private readonly DbContext _context;
    private readonly IHttpContextAccessor _http;
    private readonly IManagementContextAccessor? _management;
    private readonly IProjectOrganizationLookup? _projectOrganizationLookup;
    private readonly IOrganizationSettingsAccessService? _organizationSettingsAccess;
    private readonly IAuditService _audit;

    public ProjectLifecycleCatalogService(
        DbContext context,
        IHttpContextAccessor http,
        IAuditService audit,
        IManagementContextAccessor? management = null,
        IProjectOrganizationLookup? projectOrganizationLookup = null,
        IOrganizationSettingsAccessService? organizationSettingsAccess = null)
    {
        _context = context;
        _http = http;
        _audit = audit;
        _management = management;
        _projectOrganizationLookup = projectOrganizationLookup;
        _organizationSettingsAccess = organizationSettingsAccess;
    }

    public async Task HandleOrganizationCreatedAsync(
        Guid tenantId,
        Guid organizationId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePreInitiationPhaseAsync(tenantId, organizationId, actorId, cancellationToken);
    }

    private static bool IsPreInitiationPhase(ProjectPhaseCatalogEntry phase) =>
        string.Equals(phase.NormalizedCode, PreInitiationNormalizedCode, StringComparison.OrdinalIgnoreCase);

    private async Task<Result> ValidatePreInitiationProtectionAsync(
        ProjectPhaseCatalogEntry phase, bool allowDisplayOrderChange, CancellationToken cancellationToken)
    {
        if (IsPreInitiationPhase(phase))
        {
            return Result.Failure(Error.Conflict("The Pre-Initiation phase is protected and cannot be modified."));
        }
        return Result.Success();
    }

    public Task<Result<IReadOnlyList<ProjectLifecycleCatalogDto>>> GetPhasesAsync(
        Guid? organizationId, bool includeInactive, string? search, CancellationToken cancellationToken = default) =>
        GetCatalogAsync(true, organizationId, includeInactive, search, cancellationToken);

    public Task<Result<IReadOnlyList<ProjectLifecycleCatalogDto>>> GetStatusesAsync(
        Guid? organizationId, bool includeInactive, string? search, CancellationToken cancellationToken = default) =>
        GetCatalogAsync(false, organizationId, includeInactive, search, cancellationToken);

    public Task<Result<ProjectLifecycleCatalogDto>> CreatePhaseAsync(
        ProjectLifecycleCatalogRequest request, Guid actorId, CancellationToken cancellationToken = default) =>
        CreateAsync(true, request, actorId, cancellationToken);

    public Task<Result<ProjectLifecycleCatalogDto>> CreateStatusAsync(
        ProjectLifecycleCatalogRequest request, Guid actorId, CancellationToken cancellationToken = default) =>
        CreateAsync(false, request, actorId, cancellationToken);

    public Task<Result<ProjectLifecycleCatalogDto>> UpdatePhaseAsync(
        Guid id, ProjectLifecycleCatalogRequest request, Guid actorId, CancellationToken cancellationToken = default) =>
        UpdateAsync(true, id, request, actorId, cancellationToken);

    public Task<Result<ProjectLifecycleCatalogDto>> UpdateStatusAsync(
        Guid id, ProjectLifecycleCatalogRequest request, Guid actorId, CancellationToken cancellationToken = default) =>
        UpdateAsync(false, id, request, actorId, cancellationToken);

    public Task<Result<ProjectLifecycleCatalogDto>> SetPhaseActiveAsync(
        Guid id, bool isActive, byte[] rowVersion, Guid actorId, CancellationToken cancellationToken = default) =>
        SetActiveAsync(true, id, isActive, rowVersion, actorId, cancellationToken);

    public Task<Result<ProjectLifecycleCatalogDto>> SetStatusActiveAsync(
        Guid id, bool isActive, byte[] rowVersion, Guid actorId, CancellationToken cancellationToken = default) =>
        SetActiveAsync(false, id, isActive, rowVersion, actorId, cancellationToken);

    public Task<Result<ProjectLifecycleCatalogDto>> SetPhaseDefaultAsync(
        Guid id, byte[] rowVersion, Guid actorId, CancellationToken cancellationToken = default) =>
        SetDefaultAsync(true, id, rowVersion, actorId, cancellationToken);

    public Task<Result<ProjectLifecycleCatalogDto>> SetStatusDefaultAsync(
        Guid id, byte[] rowVersion, Guid actorId, CancellationToken cancellationToken = default) =>
        SetDefaultAsync(false, id, rowVersion, actorId, cancellationToken);

    public Task<Result<ProjectLifecycleOptionsDto>> GetOptionsAsync(
        Guid? organizationId, Guid? projectId, CancellationToken cancellationToken = default) =>
        GetOptionsAsync(organizationId, projectId, null, null, false, cancellationToken);

    public async Task<Result<ProjectLifecycleOptionsDto>> GetOptionsAsync(
        Guid? organizationId,
        Guid? projectId,
        Guid? portfolioId,
        Guid? programId,
        bool createPurpose,
        CancellationToken cancellationToken = default)
    {
        var canCreateProject = HasEffectivePermission("Project.Create");
        var canReadProjectOptions = HasAnyPermission("Project.Create", "Project.Edit");
        var canViewProjectOptions = HasEffectivePermission("Project.View");
        var canReadStatusReportOptions = projectId.HasValue && HasAnyPermission(
            "StatusReport.View", "StatusReport.Create", "StatusReport.Edit", "StatusReport.Publish");
        var canChangeProjectStatus = projectId.HasValue && HasEffectivePermission(ProjectLifecyclePermissions.StatusChange);
        var canChangeProjectPhase = projectId.HasValue && HasEffectivePermission(ProjectLifecyclePermissions.PhaseChange);
        if (createPurpose && !canCreateProject)
            return Result<ProjectLifecycleOptionsDto>.Failure(Error.Forbidden("Project.Create permission is required for Create Project lifecycle options."));
        if (!createPurpose && (portfolioId.HasValue || programId.HasValue))
            return Result<ProjectLifecycleOptionsDto>.Failure(Error.Validation("Portfolio and Program scope require the Create Project lifecycle purpose."));
        if (!createPurpose && !canReadProjectOptions && !canViewProjectOptions && !canReadStatusReportOptions
            && !canChangeProjectStatus && !canChangeProjectPhase)
            return Result<ProjectLifecycleOptionsDto>.Failure(Error.Forbidden("Project or Status Report lifecycle option permission is required."));
        if (!TryGetTrustedManagementContext(out var management))
            return Result<ProjectLifecycleOptionsDto>.Failure(Error.Forbidden("A valid tenant and management context are required."));

        if (!createPurpose && !organizationId.HasValue && !projectId.HasValue)
            return Result<ProjectLifecycleOptionsDto>.Failure(Error.Validation(
                "Project lifecycle options require an Organization or in-scope Project context."));

        var hasCreatePortfolioScope = false;
        var createTenantStatusOnly = false;
        if (createPurpose)
        {
            if (organizationId.HasValue || projectId.HasValue)
                return Result<ProjectLifecycleOptionsDto>.Failure(Error.Validation(
                    "Create Project lifecycle options must derive Organization from the selected Portfolio."));
            if (programId.HasValue && !portfolioId.HasValue)
                return Result<ProjectLifecycleOptionsDto>.Failure(Error.Validation(
                    "A Portfolio must be selected before a Program.", "portfolioId"));
            if (!portfolioId.HasValue)
            {
                // Project Status is Tenant-scoped reference data shared by every
                // Organization, so Create Project lists it before a Portfolio (and
                // therefore an Organization) is selected. Project Phase stays
                // Organization-scoped and remains unavailable until then.
                createTenantStatusOnly = true;
            }
            else
            {
                var createOrganization = await ResolveCreatePortfolioOrganizationAsync(
                    management!, portfolioId.Value, programId, cancellationToken);
                if (createOrganization.Error is not null)
                    return Result<ProjectLifecycleOptionsDto>.Failure(createOrganization.Error);

                organizationId = createOrganization.OrganizationId;
                hasCreatePortfolioScope = true;
            }
        }
        else if (portfolioId.HasValue || programId.HasValue)
        {
            return Result<ProjectLifecycleOptionsDto>.Failure(Error.Validation(
                "Portfolio and Program scope require the Create Project lifecycle purpose."));
        }

        Guid? currentPhaseId = null;
        Guid? currentStatusId = null;
        var hasProjectScope = projectId.HasValue;
        if (projectId.HasValue)
        {
            // API callers receive the internal Project ID after PublicId routing middleware
            // rewrites the route/query parameter. Resolve it within the trusted internal
            // project scope.
            var project = await _context.Set<Project>().AsNoTracking().FirstOrDefaultAsync(item =>
                item.TenantId == management!.TenantId && item.Id == projectId.Value && !item.IsDeleted, cancellationToken);
            if (project is null || !management!.Scope.ProjectIds.Contains(project.Id))
                return Result<ProjectLifecycleOptionsDto>.Failure(Error.NotFound("The requested project was not found."));

            // The ManagementScope snapshot can be stale or intentionally omit broad
            // Organization listing data. Resolve the owning Organization from the
            // persisted Project -> Portfolio/Program/Department relationships.
            // Project scope above remains the authorization boundary for this lookup.
            var resolvedOrganizationId = _projectOrganizationLookup is null
                ? null
                : await _projectOrganizationLookup.ResolveOrganizationIdAsync(
                    management.TenantId, project.Id, cancellationToken);
            if (!resolvedOrganizationId.HasValue)
                return Result<ProjectLifecycleOptionsDto>.Failure(Error.NotFound("The requested project was not found."));
            if (organizationId.HasValue && resolvedOrganizationId.Value != organizationId.Value)
                return Result<ProjectLifecycleOptionsDto>.Failure(Error.NotFound("The requested project was not found."));
            organizationId = resolvedOrganizationId.Value;
            currentPhaseId = project.ProjectPhaseId;
            currentStatusId = project.ProjectStatusId;
        }

        // A project-specific request is authorized by its in-scope Project and
        // the Organization derived from that same persisted ownership graph. A
        // separate Organization-list scope would incorrectly require access to
        // unrelated broad lookup data. Explicit Organization-only lookups still
        // require Organization scope membership.
        if (organizationId.HasValue && !hasProjectScope && !hasCreatePortfolioScope
            && !CanAccessOrganization(management!, organizationId.Value))
            return Result<ProjectLifecycleOptionsDto>.Failure(Error.NotFound("The selected organization was not found."));

        // Before a Portfolio is selected there is no Organization and therefore no
        // Organization-scoped phase catalog to read. Create Project only needs the
        // Tenant-scoped Status catalog at that point, and it must not fail on the
        // Organization-scoped Pre-Initiation phase, which is provisioned later.
        var phaseOptionsAllowed = !createTenantStatusOnly
            && (canReadProjectOptions || canViewProjectOptions || canChangeProjectPhase);
        var statusOptionsAllowed = canReadProjectOptions || canViewProjectOptions || canReadStatusReportOptions || canChangeProjectStatus;
        if (phaseOptionsAllowed
            && !await HasActiveDefaultPreInitiationPhaseAsync(management!.TenantId, organizationId, cancellationToken))
            return Result<ProjectLifecycleOptionsDto>.Failure(PreInitiationUnavailableError());
        var phaseQuery = phaseOptionsAllowed
            ? PhaseQuery(management!.TenantId).Where(item => item.IsActive)
            : PhaseQuery(management!.TenantId).Where(_ => false);
        var statusQuery = statusOptionsAllowed
            ? StatusQuery(management.TenantId).Where(item => item.IsActive || (currentStatusId.HasValue && item.Id == currentStatusId.Value))
            : StatusQuery(management.TenantId).Where(_ => false);
        if (hasCreatePortfolioScope)
        {
            // New Projects use only their selected Portfolio's Organization catalog.
            // Tenant-level defaults and another Organization's entries are not valid fallbacks.
            phaseQuery = phaseQuery.Where(item => item.OrganizationId == organizationId);
            // Project Status is Tenant-scoped reference data shared by every
            // Organization in the Tenant. Create Project must load the Tenant
            // catalog without requiring the selected Portfolio's Organization.
            statusQuery = statusQuery.Where(item => item.OrganizationId == null);
        }
        else if (hasProjectScope)
        {
            // A Project target list contains only phases explicitly configured for
            // its owning Organization. Legacy tenant-seeded rows are not targets.
            phaseQuery = phaseQuery.Where(item => item.OrganizationId == organizationId);
            // Project Status is Tenant-scoped reference data and is shared by
            // every Organization in the Tenant.
            statusQuery = statusQuery.Where(item => item.OrganizationId == null);
        }
        else if (organizationId.HasValue)
        {
            phaseQuery = phaseQuery.Where(item => item.OrganizationId == null || item.OrganizationId == organizationId.Value);
            // Project Status is Tenant-scoped reference data and is shared by
            // every Organization in the Tenant.
            statusQuery = statusQuery.Where(item => item.OrganizationId == null);
        }
        else
        {
            // Create mode without a selected Portfolio exposes no phase catalog;
            // it may return the Tenant-scoped Project Status values only.
            phaseQuery = phaseQuery.Where(item => item.OrganizationId == null || (currentPhaseId.HasValue && item.Id == currentPhaseId.Value));
            statusQuery = statusQuery.Where(item => item.OrganizationId == null || (currentStatusId.HasValue && item.Id == currentStatusId.Value));
        }

        var phases = await phaseQuery.OrderBy(item => item.OrganizationId == organizationId ? 1 : 0)
            .ThenBy(item => item.DisplayOrder).ThenBy(item => item.NameEn).ToListAsync(cancellationToken);
        var statuses = await statusQuery.OrderBy(item => item.OrganizationId == organizationId ? 1 : 0)
            .ThenBy(item => item.DisplayOrder).ThenBy(item => item.NameEn).ToListAsync(cancellationToken);

        var phaseOptions = new List<ProjectLifecycleOptionDto>();
        var orderedPhases = ApplyOrganizationOverrides(phases, organizationId, item => item.Code)
            .OrderBy(item => IsPreInitiationPhase(item) ? 0 : 1)
            .ThenBy(item => item.DisplayOrder)
            .ThenBy(item => item.NameEn, StringComparer.OrdinalIgnoreCase);
        foreach (var phase in orderedPhases)
        {
            phaseOptions.Add(new ProjectLifecycleOptionDto(
                phase.Id, phase.Code, phase.NameEn, phase.NameAr, phase.DescriptionEn, phase.DescriptionAr,
                phase.DisplayOrder, phase.IsActive,
                phase.IsDefault && !(organizationId.HasValue && phases.Any(item => item.OrganizationId == organizationId && item.IsDefault)),
                phase.IsContinuousActivity, phase.DocumentCategoryId));
        }

        var statusOptions = new List<ProjectLifecycleOptionDto>();
        foreach (var status in ApplyOrganizationOverrides(statuses, organizationId, item => item.Code))
        {
            statusOptions.Add(new ProjectLifecycleOptionDto(
                status.Id, status.Code, status.NameEn, status.NameAr, status.DescriptionEn, status.DescriptionAr,
                status.DisplayOrder, status.IsActive,
                status.IsDefault && !(organizationId.HasValue && statuses.Any(item => item.OrganizationId == organizationId && item.IsDefault)),
                ReportingCategory: status.ReportingCategory,
                AttentionLevel: status.AttentionLevel));
        }

        // An Organization-specific default supersedes the tenant default in the
        // effective lookup while the tenant-wide value remains independently saved.
        if (organizationId.HasValue)
        {
            var phaseDefault = phaseOptions.FindIndex(option => phases.Any(item => item.OrganizationId == organizationId && item.Id == option.Id && item.IsDefault));
            if (phaseDefault >= 0) phaseOptions = phaseOptions.Select((option, index) => option with { IsDefault = index == phaseDefault }).ToList();
            var statusDefault = statusOptions.FindIndex(option => statuses.Any(item => item.OrganizationId == organizationId && item.Id == option.Id && item.IsDefault));
            if (statusDefault >= 0) statusOptions = statusOptions.Select((option, index) => option with { IsDefault = index == statusDefault }).ToList();
        }

        return Result<ProjectLifecycleOptionsDto>.Success(new ProjectLifecycleOptionsDto(phaseOptions, statusOptions));
    }

    private async Task<(Guid? OrganizationId, Error? Error)> ResolveCreatePortfolioOrganizationAsync(
        ManagementContext management,
        Guid portfolioId,
        Guid? programId,
        CancellationToken cancellationToken)
    {
        var portfolio = await _context.Set<PortfolioEntity>().AsNoTracking()
            .Where(item => item.Id == portfolioId && item.TenantId == management.TenantId
                && !item.IsDeleted && item.Status == "Active")
            .Select(item => new { item.Id, item.OrganizationId })
            .FirstOrDefaultAsync(cancellationToken);
        if (portfolio is null || !portfolio.OrganizationId.HasValue
            || !management.Scope.PortfolioIds.Contains(portfolio.Id))
            return (null, Error.NotFound("The selected Portfolio was not found."));

        if (programId.HasValue)
        {
            var programIsInPortfolio = await _context.Set<ProgramEntity>().AsNoTracking()
                .AnyAsync(item => item.Id == programId.Value && item.TenantId == management.TenantId
                    && !item.IsDeleted && item.Status == "Active" && item.PortfolioId == portfolio.Id, cancellationToken);
            if (!programIsInPortfolio || !management.Scope.ProgramIds.Contains(programId.Value))
                return (null, Error.NotFound("The selected Program was not found."));
        }

        var organizationIsActive = await _context.Set<NEXUS.Modules.Organization.Domain.Organization>()
            .AsNoTracking()
            .AnyAsync(item => item.Id == portfolio.OrganizationId.Value
                && item.TenantId == management.TenantId && item.IsActive && !item.IsDeleted, cancellationToken);
        return organizationIsActive
            ? (portfolio.OrganizationId.Value, null)
            : (null, Error.NotFound("The selected Portfolio was not found."));
    }

    private async Task<Result<IReadOnlyList<ProjectLifecycleCatalogDto>>> GetCatalogAsync(
        bool phases, Guid? organizationId, bool includeInactive, string? search, CancellationToken cancellationToken)
    {
        var permission = phases ? ProjectLifecyclePermissions.PhaseView : ProjectLifecyclePermissions.StatusView;
        if (!TryGetTrustedManagementContext(out var management))
            return Result<IReadOnlyList<ProjectLifecycleCatalogDto>>.Failure(Error.Forbidden("A valid tenant and management context are required."));
        var hasTenantLifecycleView = HasEffectivePermission(permission)
            && (!organizationId.HasValue || CanAccessOrganization(management!, organizationId.Value));
        var hasScopedOrganizationAdminView = await HasScopedOrganizationAdminConfigurationAsync(
            organizationId, manage: false, cancellationToken);
        if (organizationId.HasValue && !hasTenantLifecycleView && !hasScopedOrganizationAdminView)
            return Result<IReadOnlyList<ProjectLifecycleCatalogDto>>.Failure(Error.NotFound("The selected organization was not found."));
        if (!hasTenantLifecycleView && !hasScopedOrganizationAdminView)
            return Result<IReadOnlyList<ProjectLifecycleCatalogDto>>.Failure(Error.Forbidden("Lifecycle catalog view permission is required within the selected Organization scope."));

        var isOrganizationAdminOnlyView = hasScopedOrganizationAdminView && !hasTenantLifecycleView;
        var accessibleOrganizationIds = isOrganizationAdminOnlyView
            ? new[] { organizationId!.Value }
            : management!.Scope.OrganizationIds.ToArray();
        var query = phases
            ? PhaseQuery(management.TenantId).Where(item => item.OrganizationId == null || accessibleOrganizationIds.Contains(item.OrganizationId.Value))
            : null;
        if (!includeInactive)
        {
            if (phases) query = query!.Where(item => item.IsActive);
        }
        if (organizationId.HasValue && phases) query = query!.Where(item => item.OrganizationId == null || item.OrganizationId == organizationId.Value);
        var searchTerm = search?.Trim();
        if (phases)
        {
            if (!await HasActiveDefaultPreInitiationPhaseAsync(management.TenantId, organizationId, cancellationToken))
                return Result<IReadOnlyList<ProjectLifecycleCatalogDto>>.Failure(PreInitiationUnavailableError());

            if (!includeInactive) query = query!.Where(item => item.IsActive);
            if (!string.IsNullOrWhiteSpace(searchTerm))
                query = query!.Where(item => item.NameEn.Contains(searchTerm) || item.NameAr.Contains(searchTerm) || item.Code.Contains(searchTerm));
            var items = await query!.ToListAsync(cancellationToken);
            items = items.OrderBy(item => IsPreInitiationPhase(item) ? 0 : 1)
                .ThenBy(item => item.OrganizationId == organizationId ? 1 : 0)
                .ThenBy(item => item.DisplayOrder)
                .ThenBy(item => item.NameEn, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var ids = items.Select(item => item.Id).ToArray();
            var referenceCounts = isOrganizationAdminOnlyView || ids.Length == 0 ? new Dictionary<Guid, int>()
                : await _context.Set<Project>().AsNoTracking().Where(project => project.TenantId == management.TenantId
                        && !project.IsDeleted && project.ProjectPhaseId.HasValue && ids.Contains(project.ProjectPhaseId.Value))
                    .GroupBy(project => project.ProjectPhaseId!.Value)
                    .Select(group => new { Id = group.Key, Count = group.Count() })
                    .ToDictionaryAsync(group => group.Id, group => group.Count, cancellationToken);
            return Result<IReadOnlyList<ProjectLifecycleCatalogDto>>.Success(items.Select(item => ToDto(item) with
                { ReferencedProjectCount = isOrganizationAdminOnlyView
                    ? null
                    : referenceCounts.GetValueOrDefault(item.Id) }).ToList());
        }

        var statusQuery = StatusQuery(management.TenantId)
            .Where(item => item.OrganizationId == null || accessibleOrganizationIds.Contains(item.OrganizationId.Value));
        if (!includeInactive) statusQuery = statusQuery.Where(item => item.IsActive);
        if (organizationId.HasValue) statusQuery = statusQuery.Where(item => item.OrganizationId == null || item.OrganizationId == organizationId.Value);
        if (!string.IsNullOrWhiteSpace(searchTerm))
            statusQuery = statusQuery.Where(item => item.NameEn.Contains(searchTerm) || item.NameAr.Contains(searchTerm) || item.Code.Contains(searchTerm));
        var statusItems = await statusQuery.OrderBy(item => item.OrganizationId == organizationId ? 1 : 0)
            .ThenBy(item => item.DisplayOrder).ThenBy(item => item.NameEn).ToListAsync(cancellationToken);
        var statusIds = statusItems.Select(item => item.Id).ToArray();
        var statusReferenceCounts = isOrganizationAdminOnlyView || statusIds.Length == 0 ? new Dictionary<Guid, int>()
            : await _context.Set<Project>().AsNoTracking().Where(project => project.TenantId == management.TenantId
                    && !project.IsDeleted && project.ProjectStatusId.HasValue && statusIds.Contains(project.ProjectStatusId.Value))
                .GroupBy(project => project.ProjectStatusId!.Value)
                .Select(group => new { Id = group.Key, Count = group.Count() })
                .ToDictionaryAsync(group => group.Id, group => group.Count, cancellationToken);
        return Result<IReadOnlyList<ProjectLifecycleCatalogDto>>.Success(statusItems.Select(item => ToDto(item) with
            { ReferencedProjectCount = isOrganizationAdminOnlyView
                ? null
                : statusReferenceCounts.GetValueOrDefault(item.Id) }).ToList());
    }

    private async Task<Result<ProjectLifecycleCatalogDto>> CreateAsync(
        bool phases, ProjectLifecycleCatalogRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        var permission = phases ? ProjectLifecyclePermissions.PhaseCreate : ProjectLifecyclePermissions.StatusCreate;
        if (!TryGetTrustedManagementContext(out var management) || management!.UserId != actorId)
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Forbidden("A valid tenant and management context are required."));
        var hasTenantLifecyclePermission = HasEffectivePermission(permission);
        var hasScopedOrganizationAdminManage = await HasScopedOrganizationAdminConfigurationAsync(
            request.OrganizationId, manage: true, cancellationToken);
        var hasTenantLifecycleScope = hasTenantLifecyclePermission
            && (!request.OrganizationId.HasValue || CanAccessOrganization(management!, request.OrganizationId.Value));
        if (!hasTenantLifecycleScope && !hasScopedOrganizationAdminManage)
            return Result<ProjectLifecycleCatalogDto>.Failure(request.OrganizationId.HasValue
                ? Error.NotFound("The selected Organization was not found.")
                : Error.Forbidden("Lifecycle catalog create permission is required."));
        if (management.EffectiveReadOnly)
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Forbidden("Lifecycle catalog changes are unavailable in read-only management mode."));
        if (!await CanManageScopeAsync(request.OrganizationId, management, cancellationToken,
                allowScopedOrganizationAdmin: hasScopedOrganizationAdminManage && !hasTenantLifecycleScope))
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.NotFound("The selected Organization was not found."));
        var normalized = Normalize(request, creating: true);
        if (normalized.Error is not null) return Result<ProjectLifecycleCatalogDto>.Failure(normalized.Error);

        if (phases)
        {
            var categoryError = await ValidateDocumentCategorySelectionAsync(
                management.TenantId, request.OrganizationId, request.DocumentCategoryId, null, cancellationToken);
            if (categoryError is not null)
                return Result<ProjectLifecycleCatalogDto>.Failure(categoryError);

            var recovered = await EnsurePreInitiationPhaseAsync(
                management.TenantId, request.OrganizationId, actorId, cancellationToken);
            if (recovered.IsFailure)
                return Result<ProjectLifecycleCatalogDto>.Failure(recovered.Error!);

            // Prevent creating another Pre-Initiation phase in the same scope
            if (string.Equals(normalized.Code!.ToUpperInvariant(), PreInitiationNormalizedCode, StringComparison.Ordinal))
            {
                var existingPreInitiation = await PhaseQuery(management.TenantId)
                    .FirstOrDefaultAsync(item => item.OrganizationId == request.OrganizationId
                        && string.Equals(item.NormalizedCode, PreInitiationNormalizedCode, StringComparison.OrdinalIgnoreCase)
                        && !item.IsDeleted, cancellationToken);
                if (existingPreInitiation != null)
                    return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("A Pre-Initiation phase already exists in this scope."));
            }

            if (request.IsDefault)
                return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("The protected Pre-Initiation phase must remain the default phase."));
            
            if (await PhaseDuplicateExistsAsync(management.TenantId, request.OrganizationId, normalized.Code!, normalized.NameEn!, normalized.NameAr!, null, cancellationToken))
                return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("A phase with this code or bilingual name already exists in the selected scope."));
            var hasDefault = await PhaseQuery(management.TenantId).AnyAsync(item => item.OrganizationId == request.OrganizationId && item.IsActive && item.IsDefault, cancellationToken);
            var makeDefault = request.IsDefault || !hasDefault;
            var previousDefaults = makeDefault
                ? await PhaseQuery(management.TenantId).Where(item => item.OrganizationId == request.OrganizationId && item.IsDefault).ToListAsync(cancellationToken)
                : new List<ProjectPhaseCatalogEntry>();
            foreach (var previous in previousDefaults) { previous.IsDefault = false; previous.UpdatedBy = actorId.ToString(); }
            var entity = new ProjectPhaseCatalogEntry
            {
                TenantId = management.TenantId, OrganizationId = request.OrganizationId,
                Code = normalized.Code!, NormalizedCode = normalized.Code!.ToUpperInvariant(),
                NameEn = normalized.NameEn!, NameAr = normalized.NameAr!,
                DescriptionEn = normalized.DescriptionEn, DescriptionAr = normalized.DescriptionAr,
                DisplayOrder = request.DisplayOrder, IsDefault = makeDefault,
                IsActive = true, IsContinuousActivity = request.IsContinuousActivity,
                DocumentCategoryId = request.DocumentCategoryId,
                CreatedBy = actorId.ToString()
            };
            _context.Set<ProjectPhaseCatalogEntry>().Add(entity);
            var saved = await SaveAndAuditAsync(entity, "ProjectLifecyclePhase", "Create", actorId,
                previousDefaults.Select(item => new { item.Id, item.Code, item.IsDefault }).ToArray(),
                new { entity.Code, entity.NameEn, entity.NameAr, entity.OrganizationId, entity.IsDefault, entity.IsContinuousActivity }, cancellationToken);
            return saved.IsFailure ? Result<ProjectLifecycleCatalogDto>.Failure(saved.Error!) : Result<ProjectLifecycleCatalogDto>.Success(ToDto(entity));
        }

        if (await StatusDuplicateExistsAsync(management.TenantId, request.OrganizationId, normalized.Code!, normalized.NameEn!, normalized.NameAr!, null, cancellationToken))
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("A status with this code or bilingual name already exists in the selected scope."));
        var reportingCategory = NormalizeReportingCategory(request.ReportingCategory);
        if (request.ReportingCategory is not null && reportingCategory is null)
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Validation("Reporting category must be InProgress, Completed, or Excluded.", "reportingCategory"));
        var attentionLevel = ProjectStatusAttentionLevels.Normalize(request.AttentionLevel);
        if (request.AttentionLevel is not null && attentionLevel is null)
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Validation("Attention level must be None, NeedsAttention, or Critical.", "attentionLevel"));
        var statusHasDefault = await StatusQuery(management.TenantId).AnyAsync(item => item.OrganizationId == request.OrganizationId && item.IsActive && item.IsDefault, cancellationToken);
        var makeStatusDefault = request.IsDefault || !statusHasDefault;
        var previousStatusDefaults = makeStatusDefault
            ? await StatusQuery(management.TenantId).Where(item => item.OrganizationId == request.OrganizationId && item.IsDefault).ToListAsync(cancellationToken)
            : new List<ProjectStatusCatalogEntry>();
        foreach (var previous in previousStatusDefaults) { previous.IsDefault = false; previous.UpdatedBy = actorId.ToString(); }
        var statusEntity = new ProjectStatusCatalogEntry
        {
            TenantId = management.TenantId, OrganizationId = request.OrganizationId,
            Code = normalized.Code!, NormalizedCode = normalized.Code!.ToUpperInvariant(),
            NameEn = normalized.NameEn!, NameAr = normalized.NameAr!,
            DescriptionEn = normalized.DescriptionEn, DescriptionAr = normalized.DescriptionAr,
            DisplayOrder = request.DisplayOrder, IsDefault = makeStatusDefault,
            ReportingCategory = reportingCategory ?? ProjectStatusReportingCategories.InProgress,
            AttentionLevel = attentionLevel ?? ProjectStatusAttentionLevels.None,
            IsActive = true, CreatedBy = actorId.ToString()
        };
        _context.Set<ProjectStatusCatalogEntry>().Add(statusEntity);
        var statusSaved = await SaveAndAuditAsync(statusEntity, "ProjectLifecycleStatus", "Create", actorId,
            previousStatusDefaults.Select(item => new { item.Id, item.Code, item.IsDefault }).ToArray(),
            new { statusEntity.Code, statusEntity.NameEn, statusEntity.NameAr, statusEntity.OrganizationId, statusEntity.IsDefault, statusEntity.ReportingCategory, statusEntity.AttentionLevel }, cancellationToken);
        return statusSaved.IsFailure ? Result<ProjectLifecycleCatalogDto>.Failure(statusSaved.Error!) : Result<ProjectLifecycleCatalogDto>.Success(ToDto(statusEntity));
    }

    private async Task<Result<ProjectLifecycleCatalogDto>> UpdateAsync(
        bool phases, Guid id, ProjectLifecycleCatalogRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        var permission = phases ? ProjectLifecyclePermissions.PhaseEdit : ProjectLifecyclePermissions.StatusEdit;
        if (!TryGetTrustedManagementContext(out var management) || management!.UserId != actorId)
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Forbidden("A valid tenant and management context are required."));
        if (management.EffectiveReadOnly)
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Forbidden("Lifecycle catalog changes are unavailable in read-only management mode."));
        var normalized = Normalize(request, creating: false);
        if (normalized.Error is not null) return Result<ProjectLifecycleCatalogDto>.Failure(normalized.Error);

        if (phases)
        {
            var entity = await PhaseQuery(management.TenantId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity is null) return Result<ProjectLifecycleCatalogDto>.Failure(Error.NotFound("Project phase not found."));
            var hasTenantLifecyclePermission = HasEffectivePermission(permission);
            var hasScopedOrganizationAdminManage = await HasScopedOrganizationAdminConfigurationAsync(
                entity.OrganizationId, manage: true, cancellationToken);
            var hasTenantLifecycleScope = hasTenantLifecyclePermission
                && (!entity.OrganizationId.HasValue || CanAccessOrganization(management, entity.OrganizationId.Value));
            if (!hasTenantLifecycleScope && !hasScopedOrganizationAdminManage)
                return Result<ProjectLifecycleCatalogDto>.Failure(entity.OrganizationId.HasValue
                    ? Error.NotFound("Project phase not found.")
                    : Error.Forbidden("Lifecycle catalog edit permission is required."));
            if (!await CanManageScopeAsync(entity.OrganizationId, management, cancellationToken,
                    allowScopedOrganizationAdmin: hasScopedOrganizationAdminManage && !hasTenantLifecycleScope))
                return Result<ProjectLifecycleCatalogDto>.Failure(Error.NotFound("Project phase not found."));
            
            // Protect Pre-Initiation phase from modification
            if (IsPreInitiationPhase(entity))
                return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("The Pre-Initiation phase is protected and cannot be modified."));
            
            if (request.OrganizationId != entity.OrganizationId) return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("The Organization scope of a lifecycle entry is stable."));
            if (await PhaseDuplicateExistsAsync(management.TenantId, entity.OrganizationId, normalized.Code!, normalized.NameEn!, normalized.NameAr!, id, cancellationToken))
                return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("A phase with this code or bilingual name already exists in the selected scope."));
            if (!MatchesRowVersion(entity.RowVersion, request.RowVersion)) return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("The phase changed; reload it before saving."));
            var categoryError = await ValidateDocumentCategorySelectionAsync(
                management.TenantId, entity.OrganizationId, request.DocumentCategoryId, entity.DocumentCategoryId, cancellationToken);
            if (categoryError is not null)
                return Result<ProjectLifecycleCatalogDto>.Failure(categoryError);
            var original = new { entity.NameEn, entity.NameAr, entity.DescriptionEn, entity.DescriptionAr, entity.DisplayOrder, entity.IsContinuousActivity, entity.DocumentCategoryId };
            var code = normalized.Code!;
            if (!string.Equals(code, entity.Code, StringComparison.Ordinal))
                return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("Lifecycle codes are stable and cannot be changed after creation."));
            entity.NameEn = normalized.NameEn!; entity.NameAr = normalized.NameAr!;
            entity.DescriptionEn = normalized.DescriptionEn; entity.DescriptionAr = normalized.DescriptionAr;
            entity.DisplayOrder = request.DisplayOrder; entity.IsContinuousActivity = request.IsContinuousActivity;
            entity.DocumentCategoryId = request.DocumentCategoryId;
            entity.UpdatedBy = actorId.ToString();
            var saved = await SaveAndAuditAsync(entity, "ProjectLifecyclePhase", "Update", actorId, original,
                new { entity.NameEn, entity.NameAr, entity.DescriptionEn, entity.DescriptionAr, entity.DisplayOrder, entity.IsContinuousActivity }, cancellationToken);
            return saved.IsFailure ? Result<ProjectLifecycleCatalogDto>.Failure(saved.Error!) : Result<ProjectLifecycleCatalogDto>.Success(ToDto(entity));
        }

        var statusEntity = await StatusQuery(management.TenantId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (statusEntity is null) return Result<ProjectLifecycleCatalogDto>.Failure(Error.NotFound("Project status not found."));
        var hasTenantStatusPermission = HasEffectivePermission(permission);
        var hasScopedOrganizationAdminStatusManage = await HasScopedOrganizationAdminConfigurationAsync(
            statusEntity.OrganizationId, manage: true, cancellationToken);
        var hasTenantStatusScope = hasTenantStatusPermission
            && (!statusEntity.OrganizationId.HasValue || CanAccessOrganization(management, statusEntity.OrganizationId.Value));
        if (!hasTenantStatusScope && !hasScopedOrganizationAdminStatusManage)
            return Result<ProjectLifecycleCatalogDto>.Failure(statusEntity.OrganizationId.HasValue
                ? Error.NotFound("Project status not found.")
                : Error.Forbidden("Lifecycle catalog edit permission is required."));
        if (!await CanManageScopeAsync(statusEntity.OrganizationId, management, cancellationToken,
                allowScopedOrganizationAdmin: hasScopedOrganizationAdminStatusManage && !hasTenantStatusScope))
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.NotFound("Project status not found."));
        if (request.OrganizationId != statusEntity.OrganizationId) return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("The Organization scope of a lifecycle entry is stable."));
        if (await StatusDuplicateExistsAsync(management.TenantId, statusEntity.OrganizationId, normalized.Code!, normalized.NameEn!, normalized.NameAr!, id, cancellationToken))
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("A status with this code or bilingual name already exists in the selected scope."));
        if (!MatchesRowVersion(statusEntity.RowVersion, request.RowVersion)) return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("The status changed; reload it before saving."));
        if (!string.Equals(normalized.Code, statusEntity.Code, StringComparison.Ordinal))
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("Lifecycle codes are stable and cannot be changed after creation."));
        var reportingCategory = NormalizeReportingCategory(request.ReportingCategory);
        if (request.ReportingCategory is not null && reportingCategory is null)
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Validation("Reporting category must be InProgress, Completed, or Excluded.", "reportingCategory"));
        var attentionLevel = ProjectStatusAttentionLevels.Normalize(request.AttentionLevel);
        if (request.AttentionLevel is not null && attentionLevel is null)
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Validation("Attention level must be None, NeedsAttention, or Critical.", "attentionLevel"));
        var statusOriginal = new { statusEntity.NameEn, statusEntity.NameAr, statusEntity.DescriptionEn, statusEntity.DescriptionAr, statusEntity.DisplayOrder, statusEntity.ReportingCategory, statusEntity.AttentionLevel };
        statusEntity.NameEn = normalized.NameEn!; statusEntity.NameAr = normalized.NameAr!;
        statusEntity.DescriptionEn = normalized.DescriptionEn; statusEntity.DescriptionAr = normalized.DescriptionAr;
        statusEntity.DisplayOrder = request.DisplayOrder;
        statusEntity.ReportingCategory = reportingCategory ?? statusEntity.ReportingCategory;
        statusEntity.AttentionLevel = attentionLevel ?? statusEntity.AttentionLevel;
        statusEntity.UpdatedBy = actorId.ToString();
        var statusSaved = await SaveAndAuditAsync(statusEntity, "ProjectLifecycleStatus", "Update", actorId, statusOriginal,
            new { statusEntity.NameEn, statusEntity.NameAr, statusEntity.DescriptionEn, statusEntity.DescriptionAr, statusEntity.DisplayOrder, statusEntity.ReportingCategory, statusEntity.AttentionLevel }, cancellationToken);
        return statusSaved.IsFailure ? Result<ProjectLifecycleCatalogDto>.Failure(statusSaved.Error!) : Result<ProjectLifecycleCatalogDto>.Success(ToDto(statusEntity));
    }

    private async Task<Result<ProjectLifecycleCatalogDto>> SetActiveAsync(
        bool phases, Guid id, bool isActive, byte[] rowVersion, Guid actorId, CancellationToken cancellationToken)
    {
        var permission = phases ? ProjectLifecyclePermissions.PhaseActivateDeactivate : ProjectLifecyclePermissions.StatusActivateDeactivate;
        if (!TryGetTrustedManagementContext(out var management) || management!.UserId != actorId)
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Forbidden("A valid tenant and management context are required."));
        if (management.EffectiveReadOnly)
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Forbidden("Lifecycle catalog changes are unavailable in read-only management mode."));
        if (phases)
        {
            var entity = await PhaseQuery(management.TenantId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity is null) return Result<ProjectLifecycleCatalogDto>.Failure(Error.NotFound("Project phase not found."));
            var hasTenantLifecyclePermission = HasEffectivePermission(permission);
            var hasScopedOrganizationAdminManage = await HasScopedOrganizationAdminConfigurationAsync(
                entity.OrganizationId, manage: true, cancellationToken);
            var hasTenantLifecycleScope = hasTenantLifecyclePermission
                && (!entity.OrganizationId.HasValue || CanAccessOrganization(management, entity.OrganizationId.Value));
            if (!hasTenantLifecycleScope && !hasScopedOrganizationAdminManage)
                return Result<ProjectLifecycleCatalogDto>.Failure(entity.OrganizationId.HasValue
                    ? Error.NotFound("Project phase not found.")
                    : Error.Forbidden("Lifecycle catalog activation permission is required."));
            if (!await CanManageScopeAsync(entity.OrganizationId, management, cancellationToken,
                    allowScopedOrganizationAdmin: hasScopedOrganizationAdminManage && !hasTenantLifecycleScope))
                return Result<ProjectLifecycleCatalogDto>.Failure(Error.NotFound("Project phase not found."));
            
            // Protect Pre-Initiation phase from deactivation
            if (IsPreInitiationPhase(entity) && !isActive)
                return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("The Pre-Initiation phase cannot be deactivated."));
            
            if (!MatchesRowVersion(entity.RowVersion, rowVersion)) return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("The phase changed; reload it before updating status."));
            if (!isActive && entity.IsDefault) return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("Choose another default phase before deactivating this phase."));
            if (entity.IsActive != isActive)
            {
                var original = new { entity.IsActive }; entity.IsActive = isActive; entity.UpdatedBy = actorId.ToString();
                var saved = await SaveAndAuditAsync(entity, "ProjectLifecyclePhase", isActive ? "Activate" : "Deactivate", actorId, original, new { entity.IsActive }, cancellationToken);
                if (saved.IsFailure) return Result<ProjectLifecycleCatalogDto>.Failure(saved.Error!);
            }
            return Result<ProjectLifecycleCatalogDto>.Success(ToDto(entity));
        }
        var status = await StatusQuery(management.TenantId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (status is null) return Result<ProjectLifecycleCatalogDto>.Failure(Error.NotFound("Project status not found."));
        var hasTenantStatusPermission = HasEffectivePermission(permission);
        var hasScopedOrganizationAdminStatusManage = await HasScopedOrganizationAdminConfigurationAsync(
            status.OrganizationId, manage: true, cancellationToken);
        var hasTenantStatusScope = hasTenantStatusPermission
            && (!status.OrganizationId.HasValue || CanAccessOrganization(management, status.OrganizationId.Value));
        if (!hasTenantStatusScope && !hasScopedOrganizationAdminStatusManage)
            return Result<ProjectLifecycleCatalogDto>.Failure(status.OrganizationId.HasValue
                ? Error.NotFound("Project status not found.")
                : Error.Forbidden("Lifecycle catalog activation permission is required."));
        if (!await CanManageScopeAsync(status.OrganizationId, management, cancellationToken,
                allowScopedOrganizationAdmin: hasScopedOrganizationAdminStatusManage && !hasTenantStatusScope))
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.NotFound("Project status not found."));
        if (!MatchesRowVersion(status.RowVersion, rowVersion)) return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("The status changed; reload it before updating status."));
        if (!isActive && status.IsDefault) return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("Choose another default status before deactivating this status."));
        if (status.IsActive != isActive)
        {
            var original = new { status.IsActive }; status.IsActive = isActive; status.UpdatedBy = actorId.ToString();
            var saved = await SaveAndAuditAsync(status, "ProjectLifecycleStatus", isActive ? "Activate" : "Deactivate", actorId, original, new { status.IsActive }, cancellationToken);
            if (saved.IsFailure) return Result<ProjectLifecycleCatalogDto>.Failure(saved.Error!);
        }
        return Result<ProjectLifecycleCatalogDto>.Success(ToDto(status));
    }

    private async Task<Error?> ValidateDocumentCategorySelectionAsync(
        Guid tenantId,
        Guid? organizationId,
        Guid? documentCategoryId,
        Guid? existingDocumentCategoryId,
        CancellationToken cancellationToken)
    {
        if (!documentCategoryId.HasValue)
            return null;

        // Include soft-deleted rows so an existing mapping can be preserved during
        // an unrelated edit, while new selections stay limited to active categories
        // in the exact Tenant/Organization scope.
        var category = await _context.Set<DocumentCategory>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.Id == documentCategoryId.Value
                && item.TenantId == tenantId
                && item.OrganizationId == organizationId)
            .Select(item => new { item.IsActive, item.IsDeleted })
            .SingleOrDefaultAsync(cancellationToken);

        if (category is null)
            return Error.Validation(
                "The selected document category is not available in this Tenant and Organization scope.",
                "documentCategoryId");

        if ((category.IsActive && !category.IsDeleted)
            || existingDocumentCategoryId == documentCategoryId)
            return null;

        return Error.Validation(
            "The selected document category is inactive or deleted and cannot be assigned to a phase.",
            "documentCategoryId");
    }

    private async Task<Result<ProjectLifecycleCatalogDto>> SetDefaultAsync(bool phases, Guid id, byte[] rowVersion, Guid actorId, CancellationToken cancellationToken)
    {
        var permission = phases ? ProjectLifecyclePermissions.PhaseEdit : ProjectLifecyclePermissions.StatusEdit;
        if (!TryGetTrustedManagementContext(out var management) || management!.UserId != actorId)
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Forbidden("A valid tenant and management context are required."));
        if (management.EffectiveReadOnly)
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Forbidden("Lifecycle catalog changes are unavailable in read-only management mode."));

        if (phases)
        {
            var selected = await PhaseQuery(management.TenantId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (selected is null) return Result<ProjectLifecycleCatalogDto>.Failure(Error.NotFound("Project phase not found."));
            var hasTenantLifecyclePermission = HasEffectivePermission(permission);
            var hasScopedOrganizationAdminManage = await HasScopedOrganizationAdminConfigurationAsync(
                selected.OrganizationId, manage: true, cancellationToken);
            var hasTenantLifecycleScope = hasTenantLifecyclePermission
                && (!selected.OrganizationId.HasValue || CanAccessOrganization(management, selected.OrganizationId.Value));
            if (!hasTenantLifecycleScope && !hasScopedOrganizationAdminManage)
                return Result<ProjectLifecycleCatalogDto>.Failure(selected.OrganizationId.HasValue
                    ? Error.NotFound("Project phase not found.")
                    : Error.Forbidden("Lifecycle catalog edit permission is required."));
            if (!await CanManageScopeAsync(selected.OrganizationId, management, cancellationToken,
                    allowScopedOrganizationAdmin: hasScopedOrganizationAdminManage && !hasTenantLifecycleScope))
                return Result<ProjectLifecycleCatalogDto>.Failure(Error.NotFound("Project phase not found."));
            if (!MatchesRowVersion(selected.RowVersion, rowVersion)) return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("The phase changed; reload it before setting the default."));
            if (!selected.IsActive) return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("An inactive phase cannot be the default."));

            var recovered = await EnsurePreInitiationPhaseAsync(
                management.TenantId, selected.OrganizationId, actorId, cancellationToken);
            if (recovered.IsFailure)
                return Result<ProjectLifecycleCatalogDto>.Failure(recovered.Error!);
            if (recovered.Value!.Id != selected.Id)
                return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("The protected Pre-Initiation phase must remain the default phase."));
            return Result<ProjectLifecycleCatalogDto>.Success(ToDto(recovered.Value!));
        }

        var status = await StatusQuery(management.TenantId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (status is null) return Result<ProjectLifecycleCatalogDto>.Failure(Error.NotFound("Project status not found."));
        var hasTenantStatusPermission = HasEffectivePermission(permission);
        var hasScopedOrganizationAdminStatusManage = await HasScopedOrganizationAdminConfigurationAsync(
            status.OrganizationId, manage: true, cancellationToken);
        var hasTenantStatusScope = hasTenantStatusPermission
            && (!status.OrganizationId.HasValue || CanAccessOrganization(management, status.OrganizationId.Value));
        if (!hasTenantStatusScope && !hasScopedOrganizationAdminStatusManage)
            return Result<ProjectLifecycleCatalogDto>.Failure(status.OrganizationId.HasValue
                ? Error.NotFound("Project status not found.")
                : Error.Forbidden("Lifecycle catalog edit permission is required."));
        if (!await CanManageScopeAsync(status.OrganizationId, management, cancellationToken,
                allowScopedOrganizationAdmin: hasScopedOrganizationAdminStatusManage && !hasTenantStatusScope))
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.NotFound("Project status not found."));
        if (!MatchesRowVersion(status.RowVersion, rowVersion)) return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("The status changed; reload it before setting the default."));
        if (!status.IsActive) return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("An inactive status cannot be the default."));

        if (status.IsDefault)
            return Result<ProjectLifecycleCatalogDto>.Success(ToDto(status));

        var statusSiblings = await StatusQuery(management.TenantId)
            .Where(item => item.OrganizationId == status.OrganizationId && item.IsDefault && item.Id != id)
            .ToListAsync(cancellationToken);
        var previousStatusDefaults = statusSiblings.Select(item => new { item.Id, item.Code }).ToArray();

        await using var statusTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var sibling in statusSiblings) { sibling.IsDefault = false; sibling.UpdatedBy = actorId.ToString(); }
            if (statusSiblings.Count > 0)
                await _context.SaveChangesAsync(cancellationToken);

            status.IsDefault = true; status.UpdatedBy = actorId.ToString();
            await _context.SaveChangesAsync(cancellationToken);

            var statusAudit = await _audit.LogAsync(new AuditEntry
            {
                TenantId = status.TenantId, EntityType = "ProjectLifecycleStatus", EntityId = status.Id,
                Action = "SetDefault", UserId = actorId,
                OriginalValues = previousStatusDefaults.Length == 0 ? null : JsonSerializer.Serialize(previousStatusDefaults),
                NewValues = JsonSerializer.Serialize(new { status.Id, status.Code }),
                Timestamp = DateTime.UtcNow, CreatedBy = actorId.ToString()
            }, cancellationToken);
            if (statusAudit.IsFailure) { await statusTransaction.RollbackAsync(cancellationToken); return Result<ProjectLifecycleCatalogDto>.Failure(statusAudit.Error!); }

            await statusTransaction.CommitAsync(cancellationToken);
            return Result<ProjectLifecycleCatalogDto>.Success(ToDto(status));
        }
        catch (DbUpdateConcurrencyException)
        {
            await statusTransaction.RollbackAsync(CancellationToken.None);
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("Lifecycle catalog data changed during this request. Reload and try again."));
        }
        catch (DbUpdateException)
        {
            await statusTransaction.RollbackAsync(CancellationToken.None);
            return Result<ProjectLifecycleCatalogDto>.Failure(Error.Conflict("The default lifecycle entry changed during this request. Reload and try again."));
        }
        catch
        {
            await statusTransaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private IQueryable<ProjectPhaseCatalogEntry> PhaseQuery(Guid tenantId) => _context.Set<ProjectPhaseCatalogEntry>().Where(item => item.TenantId == tenantId && !item.IsDeleted);
    private IQueryable<ProjectStatusCatalogEntry> StatusQuery(Guid tenantId) => _context.Set<ProjectStatusCatalogEntry>().Where(item => item.TenantId == tenantId && !item.IsDeleted);

    private Task<bool> HasActiveDefaultPreInitiationPhaseAsync(
        Guid tenantId, Guid? organizationId, CancellationToken cancellationToken) =>
        PhaseQuery(tenantId).AnyAsync(item => item.OrganizationId == organizationId
            && item.NormalizedCode == PreInitiationNormalizedCode
            && item.IsActive && item.IsDefault, cancellationToken);

    private static Error PreInitiationUnavailableError() => Error.Conflict(
        "The protected Pre-Initiation phase is missing or misconfigured in this scope. Lifecycle setup must be repaired before reading lifecycle options.");

    private async Task<bool> CanManageScopeAsync(
        Guid? organizationId,
        ManagementContext management,
        CancellationToken cancellationToken,
        bool allowScopedOrganizationAdmin = false)
    {
        if (organizationId is null) return !allowScopedOrganizationAdmin;
        var hasManagementScope = CanAccessOrganization(management, organizationId.Value);
        var hasOrganizationAdminScope = allowScopedOrganizationAdmin
            && await HasScopedOrganizationAdminConfigurationAsync(organizationId, manage: true, cancellationToken);
        if (!hasManagementScope && !hasOrganizationAdminScope) return false;
        return await _context.Set<NEXUS.Modules.Organization.Domain.Organization>().AnyAsync(organization =>
            organization.Id == organizationId && organization.TenantId == management.TenantId && organization.IsActive && !organization.IsDeleted, cancellationToken);
    }

    private Task<bool> HasScopedOrganizationAdminConfigurationAsync(
        Guid? organizationId,
        bool manage,
        CancellationToken cancellationToken) =>
        organizationId.HasValue && _organizationSettingsAccess is not null
            ? _organizationSettingsAccess.HasScopedOrganizationAdminAuthorityAsync(
                organizationId.Value, manage, cancellationToken)
            : Task.FromResult(false);

    private static bool CanAccessOrganization(ManagementContext management, Guid organizationId) =>
        management.Scope.OrganizationIds.Contains(organizationId)
        && management.Scope.OrganizationReferences.Any(item => item.Id == organizationId);

    private bool TryGetTrustedManagementContext(out ManagementContext? context)
    {
        context = null;
        var principal = _http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out var userId)
            || userId == Guid.Empty
            || !Guid.TryParse(principal.FindFirstValue("tenantId"), out var tenantId)
            || tenantId == Guid.Empty)
            return false;
        var current = _management?.Current;
        if (current is null || current.UserId != userId || current.TenantId != tenantId) return false;
        context = current;
        return true;
    }

    private bool HasEffectivePermission(string permission)
    {
        var user = _http.HttpContext?.User;
        return user is not null && user.HasClaim("permission", permission) && !user.HasClaim("deny", permission);
    }

    private bool HasAnyPermission(params string[] permissions) => permissions.Any(HasEffectivePermission);

    private async Task<bool> PhaseDuplicateExistsAsync(Guid tenantId, Guid? organizationId, string code, string nameEn, string nameAr, Guid? excludingId, CancellationToken cancellationToken)
    {
        var rows = await PhaseQuery(tenantId).Where(item => item.OrganizationId == organizationId && item.Id != excludingId)
            .Select(item => new { item.Id, item.NormalizedCode, item.NameEn, item.NameAr }).ToListAsync(cancellationToken);
        return rows.Any(item => item.NormalizedCode == code.ToUpperInvariant()
            || string.Equals(NormalizeName(item.NameEn)?.Normalize(), nameEn.Normalize(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(NormalizeName(item.NameAr)?.Normalize(), nameAr.Normalize(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> StatusDuplicateExistsAsync(Guid tenantId, Guid? organizationId, string code, string nameEn, string nameAr, Guid? excludingId, CancellationToken cancellationToken)
    {
        var rows = await StatusQuery(tenantId).Where(item => item.OrganizationId == organizationId && item.Id != excludingId)
            .Select(item => new { item.Id, item.NormalizedCode, item.NameEn, item.NameAr }).ToListAsync(cancellationToken);
        return rows.Any(item => item.NormalizedCode == code.ToUpperInvariant()
            || string.Equals(NormalizeName(item.NameEn)?.Normalize(), nameEn.Normalize(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(NormalizeName(item.NameAr)?.Normalize(), nameAr.Normalize(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<Result> SaveAndAuditAsync(
        BaseEntity entity, string entityType, string action, Guid actorId, object? original, object? updated, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            var audit = await _audit.LogAsync(new AuditEntry
            {
                TenantId = entity.TenantId, EntityType = entityType, EntityId = entity.Id,
                Action = action, UserId = actorId,
                OriginalValues = original is null ? null : JsonSerializer.Serialize(original),
                NewValues = updated is null ? null : JsonSerializer.Serialize(updated),
                Timestamp = DateTime.UtcNow, CreatedBy = actorId.ToString()
            }, cancellationToken);
            if (audit.IsFailure) { await transaction.RollbackAsync(cancellationToken); return audit; }
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result.Failure(Error.Conflict("Lifecycle catalog data changed during this request. Reload and try again."));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static bool MatchesRowVersion(byte[] current, byte[]? requested) =>
        requested is { Length: > 0 } && current.SequenceEqual(requested);

    private static (string? Code, string? NameEn, string? NameAr, string? DescriptionEn, string? DescriptionAr, Error? Error) Normalize(
        ProjectLifecycleCatalogRequest request, bool creating)
    {
        var code = request.Code?.Trim();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 64 || !Regex.IsMatch(code, "^[A-Za-z0-9][A-Za-z0-9._-]*$"))
            return (null, null, null, null, null, Error.Validation("Code must be 1–64 letters, digits, dots, underscores, or hyphens.", "code"));
        var nameEn = NormalizeName(request.NameEn);
        var nameAr = NormalizeName(request.NameAr);
        if (nameEn is null || nameAr is null) return (null, null, null, null, null, Error.Validation("English and Arabic names are required and must be 1–200 characters."));
        if (request.DisplayOrder < 0) return (null, null, null, null, null, Error.Validation("Display order cannot be negative.", "displayOrder"));
        if (request.DescriptionEn?.Length > 4000 || request.DescriptionAr?.Length > 4000)
            return (null, null, null, null, null, Error.Validation("Descriptions cannot exceed 4,000 characters."));
        if (!creating && request.RowVersion is null) return (null, null, null, null, null, Error.Validation("A row version is required.", "rowVersion"));
        return (code, nameEn, nameAr, CleanDescription(request.DescriptionEn), CleanDescription(request.DescriptionAr), null);
    }

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = string.Join(' ', value.Normalize().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length is < 1 or > 200 || normalized.Any(char.IsControl) ? null : normalized;
    }

    private static string? CleanDescription(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeReportingCategory(string? value)
        => ProjectStatusReportingCategories.Normalize(value);

    private static IEnumerable<ProjectPhaseCatalogEntry> ApplyOrganizationOverrides(
        IReadOnlyList<ProjectPhaseCatalogEntry> items, Guid? organizationId, Func<ProjectPhaseCatalogEntry, string> key) =>
        organizationId is null ? items : items.Where(item => item.OrganizationId == organizationId || item.OrganizationId is null)
            .GroupBy(key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.OrganizationId == organizationId && organizationId.HasValue).First());

    private static IEnumerable<ProjectStatusCatalogEntry> ApplyOrganizationOverrides(
        IReadOnlyList<ProjectStatusCatalogEntry> items, Guid? organizationId, Func<ProjectStatusCatalogEntry, string> key) =>
        organizationId is null ? items : items.Where(item => item.OrganizationId == organizationId || item.OrganizationId is null)
            .GroupBy(key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.OrganizationId == organizationId && organizationId.HasValue).First());

    private static ProjectLifecycleCatalogDto ToDto(ProjectPhaseCatalogEntry item) => new(
        item.Id, item.TenantId, item.OrganizationId, item.Code, item.NameEn, item.NameAr,
        item.DescriptionEn, item.DescriptionAr, item.DisplayOrder, item.IsDefault, item.IsActive,
        item.IsContinuousActivity, 0, item.RowVersion, item.DocumentCategoryId);

    private static ProjectLifecycleCatalogDto ToDto(ProjectStatusCatalogEntry item) => new(
        item.Id, item.TenantId, item.OrganizationId, item.Code, item.NameEn, item.NameAr,
        item.DescriptionEn, item.DescriptionAr, item.DisplayOrder, item.IsDefault, item.IsActive,
        false, 0, item.RowVersion, null, item.ReportingCategory, item.AttentionLevel);

    /// <summary>
    /// Ensures a Pre-Initiation phase exists for the specified Organization scope.
    /// Idempotent: safe to call multiple times concurrently.
    /// </summary>
    public async Task<Result<ProjectPhaseCatalogEntry>> EnsurePreInitiationPhaseAsync(
        Guid tenantId,
        Guid? organizationId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || actorId == Guid.Empty)
            return Result<ProjectPhaseCatalogEntry>.Failure(Error.Validation("Tenant and actor IDs are required to provision Pre-Initiation."));

        if (organizationId.HasValue)
        {
            var organizationExists = await _context.Set<NEXUS.Modules.Organization.Domain.Organization>().IgnoreQueryFilters()
                .AnyAsync(organization => organization.Id == organizationId.Value
                    && organization.TenantId == tenantId && !organization.IsDeleted, cancellationToken);
            if (!organizationExists)
                return Result<ProjectPhaseCatalogEntry>.Failure(Error.NotFound("The selected Organization was not found in this Tenant."));
        }

        ProjectPhaseCatalogEntry? candidate = null;
        List<ProjectPhaseCatalogEntry> changedDefaults = [];
        try
        {
            // The existing unique scope/code index plus a serializable transaction makes
            // this safe when multiple provisioning or recovery requests arrive together.
            await using (var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken))
            {
                var phases = _context.Set<ProjectPhaseCatalogEntry>().IgnoreQueryFilters();
                var matching = await phases.Where(item => item.TenantId == tenantId
                        && item.OrganizationId == organizationId
                        && item.NormalizedCode == PreInitiationNormalizedCode)
                    .ToListAsync(cancellationToken);

                var current = matching.Where(item => !item.IsDeleted).ToArray();
                if (current.Length > 1)
                    return Result<ProjectPhaseCatalogEntry>.Failure(Error.Conflict(
                        "Multiple active Pre-Initiation entries already exist in this scope; automatic recovery stopped to preserve existing references."));

                candidate = current.SingleOrDefault();
                if (candidate is null && matching.Count > 0)
                {
                    // Restore one existing identity instead of inserting a new row. Prefer
                    // the deleted record referenced by the most Projects; no Project link
                    // or historical snapshot is rewritten.
                    var deletedIds = matching.Select(item => item.Id).ToArray();
                    var referenceCounts = await _context.Set<Project>().IgnoreQueryFilters()
                        .Where(project => project.TenantId == tenantId && project.ProjectPhaseId.HasValue
                            && deletedIds.Contains(project.ProjectPhaseId.Value))
                        .GroupBy(project => project.ProjectPhaseId!.Value)
                        .Select(group => new { PhaseId = group.Key, Count = group.Count() })
                        .ToDictionaryAsync(group => group.PhaseId, group => group.Count, cancellationToken);
                    candidate = matching.OrderByDescending(item => referenceCounts.GetValueOrDefault(item.Id))
                        .ThenBy(item => item.Id)
                        .First();
                }

                var candidateId = candidate?.Id;
                var scopePhases = await _context.Set<ProjectPhaseCatalogEntry>().IgnoreQueryFilters()
                    .Where(item => item.TenantId == tenantId && item.OrganizationId == organizationId
                        && !item.IsDeleted && item.IsDefault
                        && (!candidateId.HasValue || item.Id != candidateId.Value))
                    .ToListAsync(cancellationToken);
                foreach (var phase in scopePhases)
                {
                    phase.IsDefault = false;
                    phase.UpdatedBy = actorId.ToString();
                }
                changedDefaults = scopePhases;
                if (changedDefaults.Count > 0)
                    await _context.SaveChangesAsync(cancellationToken);

                if (candidate is null)
                {
                    candidate = new ProjectPhaseCatalogEntry
                    {
                        TenantId = tenantId,
                        OrganizationId = organizationId,
                        Code = PreInitiationCode,
                        NormalizedCode = PreInitiationNormalizedCode,
                        NameEn = PreInitiationNameEn,
                        NameAr = PreInitiationNameAr,
                        DisplayOrder = PreInitiationDisplayOrder,
                        IsDefault = true,
                        IsActive = true,
                        IsContinuousActivity = false,
                        CreatedBy = actorId.ToString()
                    };
                    _context.Set<ProjectPhaseCatalogEntry>().Add(candidate);
                }
                else
                {
                    candidate.IsDeleted = false;
                    candidate.IsActive = true;
                    candidate.IsDefault = true;
                    candidate.NameEn = PreInitiationNameEn;
                    candidate.NameAr = PreInitiationNameAr;
                    candidate.DisplayOrder = PreInitiationDisplayOrder;
                    candidate.IsContinuousActivity = false;
                    candidate.UpdatedBy = actorId.ToString();
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            return Result<ProjectPhaseCatalogEntry>.Success(candidate);
        }
        catch (DbUpdateException)
        {
            // If another request won the unique-index race, discard stale tracked state
            // and return the persisted winner. Other database errors remain a conflict.
            if (candidate is not null)
                _context.Entry(candidate).State = EntityState.Detached;
            foreach (var phase in changedDefaults)
                _context.Entry(phase).State = EntityState.Detached;

            var winner = await _context.Set<ProjectPhaseCatalogEntry>().IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId
                    && item.OrganizationId == organizationId
                    && item.NormalizedCode == PreInitiationNormalizedCode
                    && !item.IsDeleted
                    && item.IsActive
                    && item.IsDefault
                    && item.NameEn == PreInitiationNameEn
                    && item.NameAr == PreInitiationNameAr
                    && item.DisplayOrder == PreInitiationDisplayOrder,
                    CancellationToken.None);
            if (winner is not null)
                return Result<ProjectPhaseCatalogEntry>.Success(winner);

            return Result<ProjectPhaseCatalogEntry>.Failure(Error.Conflict(
                "Pre-Initiation changed during provisioning. Retry the request after reloading the lifecycle catalog."));
        }
    }
}
