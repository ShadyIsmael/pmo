using System.Data.Common;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Organization.Application.Settings;

namespace NEXUS.Modules.Organization.Application;

public sealed class PhaseGateConfigurationService : IPhaseGateConfigurationService
{
    private const int MaximumRequirementsPerPhasePair = 100;
    private const string DocumentViewPolicy = "Document.View";
    private const string SettingsViewPolicy = "Settings.View";
    private const string SettingsManagePolicy = "Settings.ManageTenantConfiguration";

    private readonly DbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IOrganizationSettingsAccessService _organizationAccess;
    private readonly IProjectPhaseGatePhasePairCatalog _phasePairs;
    private readonly IApprovedTemplateCatalog _templateCatalog;
    private readonly IAuditService _auditService;

    public PhaseGateConfigurationService(
        DbContext context,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IOrganizationSettingsAccessService organizationAccess,
        IProjectPhaseGatePhasePairCatalog phasePairs,
        IApprovedTemplateCatalog templateCatalog,
        IAuditService auditService)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _organizationAccess = organizationAccess;
        _phasePairs = phasePairs;
        _templateCatalog = templateCatalog;
        _auditService = auditService;
    }

    public async Task<(PhaseGateConfigurationOperationCode Code, IReadOnlyList<ProjectPhaseGatePhasePairDto> PhasePairs)> GetPhasePairsAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantAndActor(out var tenantId, out _))
            return (PhaseGateConfigurationOperationCode.TenantContextMissing, Array.Empty<ProjectPhaseGatePhasePairDto>());
        if (!await IsAuthorizedAsync(SettingsViewPolicy) || !await IsAuthorizedAsync(DocumentViewPolicy))
            return (PhaseGateConfigurationOperationCode.AccessDenied, Array.Empty<ProjectPhaseGatePhasePairDto>());
        try
        {
            if (!await _organizationAccess.HasAccessAsync(organizationId, manage: false, cancellationToken))
                return (PhaseGateConfigurationOperationCode.AccessDenied, Array.Empty<ProjectPhaseGatePhasePairDto>());
            return (PhaseGateConfigurationOperationCode.Success,
                await _phasePairs.GetPhasePairsAsync(tenantId, organizationId, cancellationToken));
        }
        catch (SettingsPersistenceUnavailableException)
        {
            return (PhaseGateConfigurationOperationCode.PersistenceUnavailable, Array.Empty<ProjectPhaseGatePhasePairDto>());
        }
    }

    public async Task<PhaseGateConfigurationOperationResult> GetConfigurationAsync(
        Guid organizationId, Guid sourcePhaseId, Guid targetPhaseId, CancellationToken cancellationToken = default)
    {
        var access = await CheckOrganizationAccessAsync(organizationId, manage: false, cancellationToken);
        if (access != PhaseGateConfigurationOperationCode.Success) return new(access);
        if (!TryGetTenantAndActor(out var tenantId, out _))
            return new(PhaseGateConfigurationOperationCode.TenantContextMissing);
        ProjectPhaseGatePhasePairDto? phasePair;
        try
        {
            phasePair = await _phasePairs.GetPhasePairAsync(
                tenantId, organizationId, sourcePhaseId, targetPhaseId, cancellationToken);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            return new(PhaseGateConfigurationOperationCode.PersistenceUnavailable,
                Error: "Phase-gate phase-pair storage is unavailable or its schema has not been deployed.");
        }
        if (phasePair is null) return InvalidPhasePair();

        try
        {
            var configuration = await FindConfigurationAsync(tenantId, organizationId, sourcePhaseId, targetPhaseId, tracking: false, cancellationToken);
            if (configuration is null)
                return new(PhaseGateConfigurationOperationCode.Success,
                    Configuration: EmptyConfiguration(organizationId, phasePair));

            var version = await _context.Set<OrganizationPhaseGateConfigurationVersion>()
                .IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted
                    && item.ConfigurationId == configuration.Id
                    && item.VersionNumber == configuration.CurrentVersionNumber)
                .Include(item => item.Requirements)
                .SingleOrDefaultAsync(cancellationToken);
            if (version is null)
                return new(PhaseGateConfigurationOperationCode.PersistenceUnavailable,
                    Error: "The current phase-gate configuration version is unavailable.");
            return new(PhaseGateConfigurationOperationCode.Success,
                Configuration: MapConfiguration(configuration, version));
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            return new(PhaseGateConfigurationOperationCode.PersistenceUnavailable,
                Error: "Phase-gate configuration storage is unavailable or its schema has not been deployed.");
        }
    }

    public async Task<PhaseGateConfigurationOperationResult> GetHistoryAsync(
        Guid organizationId, Guid sourcePhaseId, Guid targetPhaseId, CancellationToken cancellationToken = default)
    {
        var access = await CheckOrganizationAccessAsync(organizationId, manage: false, cancellationToken);
        if (access != PhaseGateConfigurationOperationCode.Success) return new(access);
        if (!TryGetTenantAndActor(out var tenantId, out _))
            return new(PhaseGateConfigurationOperationCode.TenantContextMissing);
        ProjectPhaseGatePhasePairDto? phasePair;
        try
        {
            phasePair = await _phasePairs.GetPhasePairAsync(
                tenantId, organizationId, sourcePhaseId, targetPhaseId, cancellationToken);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            return new(PhaseGateConfigurationOperationCode.PersistenceUnavailable,
                Error: "Phase-gate phase-pair storage is unavailable or its schema has not been deployed.");
        }
        if (phasePair is null) return InvalidPhasePair();

        try
        {
            var configuration = await FindConfigurationAsync(tenantId, organizationId, sourcePhaseId, targetPhaseId, tracking: false, cancellationToken);
            if (configuration is null)
                return new(PhaseGateConfigurationOperationCode.Success,
                    History: new PhaseGateConfigurationHistoryDto(Guid.Empty, organizationId,
                        sourcePhaseId, phasePair.SourcePhase, targetPhaseId, phasePair.TargetPhase,
                        Array.Empty<PhaseGateConfigurationVersionDto>()));

            var versions = await _context.Set<OrganizationPhaseGateConfigurationVersion>()
                .IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ConfigurationId == configuration.Id)
                .Include(item => item.Requirements)
                .OrderByDescending(item => item.VersionNumber)
                .ToListAsync(cancellationToken);
            return new(PhaseGateConfigurationOperationCode.Success,
                History: new PhaseGateConfigurationHistoryDto(configuration.Id, organizationId,
                    configuration.SourcePhaseId, configuration.SourcePhase,
                    configuration.TargetPhaseId, configuration.TargetPhase,
                    versions.Select(MapVersion).ToArray()));
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            return new(PhaseGateConfigurationOperationCode.PersistenceUnavailable,
                Error: "Phase-gate configuration history is unavailable or its schema has not been deployed.");
        }
    }

    public async Task<PhaseGateConfigurationOperationResult> SaveConfigurationAsync(
        Guid organizationId,
        Guid sourcePhaseId,
        Guid targetPhaseId,
        SavePhaseGateConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await CheckOrganizationAccessAsync(organizationId, manage: true, cancellationToken);
        if (access != PhaseGateConfigurationOperationCode.Success) return new(access);
        if (!TryGetTenantAndActor(out var tenantId, out var actorId))
            return new(PhaseGateConfigurationOperationCode.TenantContextMissing);
        ProjectPhaseGatePhasePairDto? phasePair;
        try
        {
            phasePair = await _phasePairs.GetPhasePairAsync(
                tenantId, organizationId, sourcePhaseId, targetPhaseId, cancellationToken);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            return new(PhaseGateConfigurationOperationCode.PersistenceUnavailable,
                Error: "Phase-gate phase-pair storage is unavailable or its schema has not been deployed.");
        }
        if (phasePair is null) return InvalidPhasePair();
        if (request is null || request.Requirements is null)
            return ValidationFailure("requirements", "A requirement list is required.");
        if (request.Requirements.Count > MaximumRequirementsPerPhasePair)
            return ValidationFailure("requirements", $"No more than {MaximumRequirementsPerPhasePair} requirements may be configured for one phase pair.");

        if (!TryParseExpectedVersion(request.ExpectedVersionToken, out var expectedVersion, out var versionError))
            return ValidationFailure("expectedVersionToken", versionError!);
        for (var index = 0; index < request.Requirements.Count; index++)
        {
            if (request.Requirements[index] is null)
                return ValidationFailure($"requirements[{index}]", "A requirement item cannot be null.");
        }
        var duplicateTemplate = request.Requirements
            .GroupBy(item => item.TemplateId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateTemplate is not null)
            return ValidationFailure("requirements", "A template may appear only once in one phase-pair configuration.");
        for (var index = 0; index < request.Requirements.Count; index++)
        {
            var requirement = request.Requirements[index];
            if (requirement.TemplateId == Guid.Empty || requirement.TemplateVersionId == Guid.Empty)
                return ValidationFailure($"requirements[{index}]", "A valid template and template version are required.");
            if (!Enum.IsDefined(requirement.RequirementType))
                return ValidationFailure($"requirements[{index}].requirementType", "Requirement type must be Mandatory or Optional.");
        }

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var configuration = await FindConfigurationAsync(tenantId, organizationId, sourcePhaseId, targetPhaseId,
                tracking: true, cancellationToken);

            if ((configuration is null && expectedVersion is not null)
                || (configuration is not null && (expectedVersion is null
                    || !expectedVersion.AsSpan().SequenceEqual(configuration.RowVersion))))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(PhaseGateConfigurationOperationCode.ConcurrencyConflict,
                    Error: "The phase-gate configuration changed. Reload it before saving.");
            }

            OrganizationPhaseGateConfigurationVersion? currentVersion = null;
            if (configuration is not null)
            {
                currentVersion = await _context.Set<OrganizationPhaseGateConfigurationVersion>()
                    .IgnoreQueryFilters()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.ConfigurationId == configuration.Id
                        && item.VersionNumber == configuration.CurrentVersionNumber)
                    .Include(item => item.Requirements)
                    .SingleOrDefaultAsync(cancellationToken);
                if (currentVersion is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new(PhaseGateConfigurationOperationCode.PersistenceUnavailable,
                        Error: "The current phase-gate configuration version is unavailable.");
                }
            }
            else
            {
                configuration = new OrganizationPhaseGateConfiguration
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    OrganizationId = organizationId,
                    SourcePhaseId = sourcePhaseId,
                    TargetPhaseId = targetPhaseId,
                    SourcePhase = NormalizePhase(phasePair.SourcePhase),
                    TargetPhase = NormalizePhase(phasePair.TargetPhase),
                    CurrentVersionNumber = 0,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = actorId.ToString()
                };
                _context.Set<OrganizationPhaseGateConfiguration>().Add(configuration);
            }

            var currentRequirementSnapshots = currentVersion?.Requirements
                .Where(item => !item.IsDeleted)
                .ToDictionary(
                    item => (item.TemplateId, item.TemplateVersionId),
                    item => new ApprovedTemplateVersionSnapshot(
                        item.TemplateId,
                        item.TemplateVersionId,
                        item.TemplateName,
                        item.Category,
                        item.TemplateVersionLabel))
                ?? new Dictionary<(Guid TemplateId, Guid TemplateVersionId), ApprovedTemplateVersionSnapshot>();
            var snapshots = new List<(SavePhaseGateRequirementRequest Request, ApprovedTemplateVersionSnapshot Snapshot)>();
            for (var index = 0; index < request.Requirements.Count; index++)
            {
                var requirement = request.Requirements[index];
                var reference = (requirement.TemplateId, requirement.TemplateVersionId);
                var remainsUnchanged = currentRequirementSnapshots.TryGetValue(reference, out var existingSnapshot);
                var catalogSnapshot = await _templateCatalog.GetVersionAsync(
                    tenantId, organizationId, requirement.TemplateId, requirement.TemplateVersionId,
                    requireActive: !remainsUnchanged, cancellationToken);
                if (catalogSnapshot is null
                    || catalogSnapshot.TemplateId != requirement.TemplateId
                    || catalogSnapshot.TemplateVersionId != requirement.TemplateVersionId)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ValidationFailure($"requirements[{index}].templateVersionId",
                        "The selected approved template version is unavailable for this organization.");
                }
                // Keep historical labels stable when the exact same template version remains
                // selected, even if its current Document Center metadata has since changed.
                var snapshot = remainsUnchanged ? existingSnapshot! : catalogSnapshot;
                snapshots.Add((requirement, snapshot));
            }

            var oldSummary = currentVersion is null ? null : currentVersion.Requirements
                .Where(item => !item.IsDeleted)
                .Select(item => new { item.TemplateId, item.TemplateVersionId, item.RequirementType })
                .OrderBy(item => item.TemplateId).ToArray();
            var nextVersionNumber = configuration.CurrentVersionNumber + 1;
            var now = DateTime.UtcNow;
            var version = new OrganizationPhaseGateConfigurationVersion
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ConfigurationId = configuration.Id,
                VersionNumber = nextVersionNumber,
                CreatedAt = now,
                CreatedBy = actorId.ToString()
            };
            foreach (var item in snapshots)
            {
                version.Requirements.Add(new OrganizationPhaseGateRequirement
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ConfigurationVersionId = version.Id,
                    TemplateId = item.Snapshot.TemplateId,
                    TemplateVersionId = item.Snapshot.TemplateVersionId,
                    TemplateName = item.Snapshot.Name,
                    Category = item.Snapshot.Category,
                    TemplateVersionLabel = item.Snapshot.VersionLabel,
                    RequirementType = item.Request.RequirementType.ToString(),
                    CreatedAt = now,
                    CreatedBy = actorId.ToString()
                });
            }
            configuration.CurrentVersionNumber = nextVersionNumber;
            configuration.UpdatedAt = now;
            configuration.UpdatedBy = actorId.ToString();
            configuration.Versions.Add(version);
            _context.Set<OrganizationPhaseGateConfigurationVersion>().Add(version);

            await _context.SaveChangesAsync(cancellationToken);
            var auditResult = await _auditService.AppendAsync(CreateAudit(
                tenantId, actorId, configuration, nextVersionNumber, oldSummary,
                snapshots.Select(item => new
                {
                    item.Snapshot.TemplateId,
                    item.Snapshot.TemplateVersionId,
                    RequirementType = item.Request.RequirementType.ToString()
                }).OrderBy(item => item.TemplateId).ToArray()), cancellationToken);
            if (auditResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(PhaseGateConfigurationOperationCode.AuditFailed,
                    Error: auditResult.Error?.Message ?? "The phase-gate audit event could not be recorded.");
            }
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new(PhaseGateConfigurationOperationCode.Success,
                Configuration: MapConfiguration(configuration, version));
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(PhaseGateConfigurationOperationCode.ConcurrencyConflict,
                Error: "The phase-gate configuration changed. Reload it before saving.");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return new(PhaseGateConfigurationOperationCode.ConcurrencyConflict,
                Error: "Another update created or changed this phase-gate configuration. Reload it before saving.");
        }
        catch (SettingsPersistenceUnavailableException exception)
        {
            return new(PhaseGateConfigurationOperationCode.PersistenceUnavailable, Error: exception.Message);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            return new(PhaseGateConfigurationOperationCode.PersistenceUnavailable,
                Error: "Phase-gate configuration storage is unavailable or its schema has not been deployed.");
        }
    }

    public async Task<PhaseGateConfigurationOperationResult> GetTemplatesByCategoryAsync(
        Guid organizationId, Guid sourcePhaseId, Guid? categoryId, CancellationToken cancellationToken = default)
    {
        var access = await CheckOrganizationAccessAsync(organizationId, manage: false, cancellationToken);
        if (access != PhaseGateConfigurationOperationCode.Success) return new(access);
        if (!TryGetTenantAndActor(out var tenantId, out _))
            return new(PhaseGateConfigurationOperationCode.TenantContextMissing);

        // Verify the source phase exists in the lifecycle catalog for this tenant/org
        // Use the existing phase pair catalog which already has access to phase pairs
        var phasePairs = await _phasePairs.GetPhasePairsAsync(tenantId, organizationId, cancellationToken);
        var phaseExists = phasePairs.Any(pair => pair.SourcePhaseId == sourcePhaseId);
        if (!phaseExists)
            return new(PhaseGateConfigurationOperationCode.NotFound, Error: "Source phase not found.");

        try
        {
            var templateGroups = await _templateCatalog.GetTemplatesByCategoryAsync(
                tenantId, organizationId, categoryId, cancellationToken);
            
            // Convert to DTO format expected by frontend
            var dtos = templateGroups.Select(group => new
            {
                categoryId = group.CategoryId,
                categoryName = group.CategoryName,
                categoryNameAr = group.CategoryNameAr,
                templates = group.Templates.Select(t => new
                {
                    templateId = t.TemplateId,
                    templateVersionId = t.TemplateVersionId,
                    templateName = t.Name,
                    category = t.Category,
                    version = t.VersionLabel
                }).ToArray()
            }).ToArray();

            return new(PhaseGateConfigurationOperationCode.Success,
                Configuration: new PhaseGateConfigurationDto(
                    null, organizationId, sourcePhaseId, "", Guid.Empty, "", null, 0, null, null, null, null, null, 
                    dtos.SelectMany(g => g.templates.Select(t => new PhaseGateRequirementDto(
                        Guid.Empty, t.templateId, t.templateVersionId, t.templateName, t.category, t.version, PhaseGateRequirementType.Mandatory))).ToArray()));
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            return new(PhaseGateConfigurationOperationCode.PersistenceUnavailable,
                Error: "Template catalog is unavailable or its schema has not been deployed.");
        }
    }

    private async Task<PhaseGateConfigurationOperationCode> CheckOrganizationAccessAsync(
        Guid organizationId, bool manage, CancellationToken cancellationToken)
    {
        if (!TryGetTenantAndActor(out _, out _)) return PhaseGateConfigurationOperationCode.TenantContextMissing;
        var settingsPolicy = manage ? SettingsManagePolicy : SettingsViewPolicy;
        if (!await IsAuthorizedAsync(settingsPolicy) || !await IsAuthorizedAsync(DocumentViewPolicy))
            return PhaseGateConfigurationOperationCode.AccessDenied;
        try
        {
            return await _organizationAccess.HasAccessAsync(organizationId, manage, cancellationToken)
                ? PhaseGateConfigurationOperationCode.Success
                : PhaseGateConfigurationOperationCode.AccessDenied;
        }
        catch (SettingsPersistenceUnavailableException)
        {
            return PhaseGateConfigurationOperationCode.PersistenceUnavailable;
        }
    }

    private async Task<bool> IsAuthorizedAsync(string policy)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return false;
        var result = await _authorizationService.AuthorizeAsync(user, null, policy);
        return result.Succeeded;
    }

    private async Task<OrganizationPhaseGateConfiguration?> FindConfigurationAsync(
        Guid tenantId, Guid organizationId, Guid sourcePhaseId, Guid targetPhaseId,
        bool tracking, CancellationToken cancellationToken)
    {
        var query = _context.Set<OrganizationPhaseGateConfiguration>().IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.OrganizationId == organizationId
                && item.SourcePhaseId == sourcePhaseId
                && item.TargetPhaseId == targetPhaseId && !item.IsDeleted);
        return tracking
            ? await query.SingleOrDefaultAsync(cancellationToken)
            : await query.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
    }

    private AuditEntry CreateAudit(
        Guid tenantId,
        Guid actorId,
        OrganizationPhaseGateConfiguration configuration,
        int versionNumber,
        object? oldValue,
        object newValue) => new()
    {
        TenantId = tenantId,
        EntityType = "OrganizationPhaseGateConfiguration",
        EntityId = configuration.Id,
        Action = "VersionCreated",
        ChangedFields = "Requirements,CurrentVersionNumber",
        Description = $"Phase-gate requirements for phase transition {configuration.SourcePhaseId} to {configuration.TargetPhaseId} were saved as version {versionNumber}.",
        OriginalValues = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
        NewValues = JsonSerializer.Serialize(new
        {
            configuration.SourcePhaseId,
            configuration.TargetPhaseId,
            configuration.SourcePhase,
            configuration.TargetPhase,
            versionNumber,
            requirements = newValue
        }),
        Changes = JsonSerializer.Serialize(new { organizationId = configuration.OrganizationId, from = oldValue, to = newValue }),
        Source = AuditSources.User,
        CorrelationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? _httpContextAccessor.HttpContext?.TraceIdentifier,
        UserId = actorId,
        CreatedBy = actorId.ToString()
    };

    private static PhaseGateConfigurationDto EmptyConfiguration(Guid organizationId, ProjectPhaseGatePhasePairDto phasePair) =>
        new(null, organizationId, phasePair.SourcePhaseId, NormalizePhase(phasePair.SourcePhase),
            phasePair.TargetPhaseId, NormalizePhase(phasePair.TargetPhase), null, 0,
            null, null, null, null, null, Array.Empty<PhaseGateRequirementDto>());

    private static PhaseGateConfigurationDto MapConfiguration(
        OrganizationPhaseGateConfiguration configuration,
        OrganizationPhaseGateConfigurationVersion version) =>
        new(configuration.Id, configuration.OrganizationId,
            configuration.SourcePhaseId, configuration.SourcePhase,
            configuration.TargetPhaseId, configuration.TargetPhase,
            version.Id, version.VersionNumber, Convert.ToBase64String(configuration.RowVersion),
            version.CreatedAt, version.CreatedBy, configuration.UpdatedAt, configuration.UpdatedBy,
            MapRequirements(version.Requirements));

    private static PhaseGateConfigurationVersionDto MapVersion(OrganizationPhaseGateConfigurationVersion version) =>
        new(version.Id, version.VersionNumber, version.CreatedAt, version.CreatedBy, MapRequirements(version.Requirements));

    private static IReadOnlyList<PhaseGateRequirementDto> MapRequirements(IEnumerable<OrganizationPhaseGateRequirement> requirements) =>
        requirements.Where(item => !item.IsDeleted)
            .OrderBy(item => item.TemplateName).ThenBy(item => item.TemplateId)
            .Select(item => new PhaseGateRequirementDto(
                item.Id,
                item.TemplateId,
                item.TemplateVersionId,
                item.TemplateName,
                item.Category,
                item.TemplateVersionLabel,
                Enum.TryParse<PhaseGateRequirementType>(item.RequirementType, ignoreCase: false, out var type)
                    ? type
                    : PhaseGateRequirementType.Optional))
            .ToArray();

    private static string NormalizePhase(string phase) => phase.Trim().ToLowerInvariant();

    private static PhaseGateConfigurationOperationResult InvalidPhasePair() =>
        ValidationFailure("phasePair", "The selected active phase pair is not available in this Organization's lifecycle catalog.");

    private static PhaseGateConfigurationOperationResult ValidationFailure(string field, string message) =>
        new(PhaseGateConfigurationOperationCode.ValidationFailed, Error: message,
            ValidationErrors: new Dictionary<string, string[]> { [field] = [message] });

    private bool TryGetTenantAndActor(out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true
            || !Guid.TryParse(user.FindFirstValue("tenantId"), out tenantId)
            || tenantId == Guid.Empty) return false;
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out actorId)
            && !Guid.TryParse(user.FindFirstValue("sub"), out actorId)) return false;
        return actorId != Guid.Empty;
    }

    private static bool TryParseExpectedVersion(string? value, out byte[]? version, out string? error)
    {
        version = null;
        error = null;
        if (value is null) return true;
        try
        {
            version = Convert.FromBase64String(value);
            if (version.Length == 8) return true;
        }
        catch (FormatException)
        {
            // Report the common validation message below.
        }
        error = "Expected version token must be an 8-byte base64 rowversion value, or null when creating a configuration.";
        return false;
    }

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is DbException or DbUpdateException
        || exception.InnerException is not null && IsDatabaseFailure(exception.InnerException);

    private static bool IsUniqueConstraintViolation(Exception exception) =>
        exception is DbException databaseException
        && databaseException.GetType().GetProperty("Number", BindingFlags.Public | BindingFlags.Instance)?.GetValue(databaseException) is int number
        && number is 2601 or 2627
        || exception.InnerException is not null && IsUniqueConstraintViolation(exception.InnerException);
}
