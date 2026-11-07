using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.WorkflowEngine.Application.Services;

public interface IProjectWorkflowConfigurationService
{
    Task<Result<ProjectWorkflowSettingsDto>> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectWorkflowConfigurationDto>> SaveAsync(Guid projectId, string domain, SaveProjectWorkflowConfigurationRequest request, string actor, CancellationToken cancellationToken = default);
    Task<Result<ProjectWorkflowSelection?>> ResolveForNewRecordAsync(Guid? projectId, string domain, string entityType, WorkflowExecutionContext context, CancellationToken cancellationToken = default);
}

public sealed class ProjectWorkflowConfigurationService : IProjectWorkflowConfigurationService
{
    private const string ProjectSettingsViewPermission = "ProjectSettings.View";
    private const string ProjectSettingsEditPermission = "ProjectSettings.Edit";
    private static readonly string[] Domains = { WorkflowDomains.Task, WorkflowDomains.Risk, WorkflowDomains.Finance, WorkflowDomains.ChangeRequest };
    private readonly DbContext _context;
    private readonly IProjectReferenceLookup _projectLookup;
    private readonly IManagementContextAccessor _managementContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProjectWorkflowConfigurationService(
        DbContext context,
        IProjectReferenceLookup projectLookup,
        IManagementContextAccessor managementContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _projectLookup = projectLookup;
        _managementContext = managementContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<ProjectWorkflowSettingsDto>> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!HasPermission(ProjectSettingsViewPermission))
            return Result<ProjectWorkflowSettingsDto>.Failure(Error.Forbidden("ProjectSettings.View is required to view project workflow settings."));
        var tenantId = CurrentTenantId();
        if (!tenantId.HasValue)
            return Result<ProjectWorkflowSettingsDto>.Failure(Error.Forbidden("A verified tenant context is required to view project workflow settings."));

        var access = await EnsureProjectAccessAsync(projectId, cancellationToken);
        if (access.IsFailure) return Result<ProjectWorkflowSettingsDto>.Failure(access.Error!);

        var rows = await _context.Set<ProjectWorkflowConfiguration>()
            .AsNoTracking()
            .Include(item => item.WorkflowDefinition)
            .Include(item => item.EffectiveWorkflowVersion)
            .Where(item => item.TenantId == tenantId.Value && item.ProjectId == projectId && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        var dto = Domains.Select(domain => ToDto(rows.FirstOrDefault(item => item.Domain == domain), projectId, domain)).ToList();
        return Result<ProjectWorkflowSettingsDto>.Success(new ProjectWorkflowSettingsDto(projectId, dto));
    }

    public async Task<Result<ProjectWorkflowConfigurationDto>> SaveAsync(Guid projectId, string domain, SaveProjectWorkflowConfigurationRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (!HasPermission(ProjectSettingsViewPermission) || !HasPermission(ProjectSettingsEditPermission))
            return Result<ProjectWorkflowConfigurationDto>.Failure(Error.Forbidden("ProjectSettings.View and ProjectSettings.Edit are required to change project workflow settings."));

        var tenantId = CurrentTenantId();
        if (!tenantId.HasValue)
            return Result<ProjectWorkflowConfigurationDto>.Failure(Error.Forbidden("A verified tenant context is required to change project workflow settings."));
        var access = await EnsureProjectAccessAsync(projectId, cancellationToken);
        if (access.IsFailure) return Result<ProjectWorkflowConfigurationDto>.Failure(access.Error!);
        if (_managementContext.Current?.EffectiveReadOnly == true || IsReadOnlyClaim())
            return Result<ProjectWorkflowConfigurationDto>.Failure(Error.Forbidden("Cannot change project workflow configuration in read-only view."));

        var normalizedDomain = WorkflowDomains.Normalize(domain);
        if (!Domains.Contains(normalizedDomain, StringComparer.Ordinal))
            return Result<ProjectWorkflowConfigurationDto>.Failure(Error.Validation("Project workflow settings are not available for this workflow domain."));
        var mode = request.Mode.Trim();
        if (!ProjectWorkflowModes.IsSupported(mode))
            return Result<ProjectWorkflowConfigurationDto>.Failure(Error.Validation("Workflow mode must be Standard or Custom."));
        mode = string.Equals(mode, ProjectWorkflowModes.Custom, StringComparison.OrdinalIgnoreCase) ? ProjectWorkflowModes.Custom : ProjectWorkflowModes.Standard;

        WorkflowDefinition? definition = null;
        WorkflowDefinitionVersion? version = null;
        if (mode == ProjectWorkflowModes.Custom)
        {
            if (!HasPermission("Workflow.View"))
                return Result<ProjectWorkflowConfigurationDto>.Failure(Error.Forbidden("Workflow.View is required to select a published project workflow."));
            if (!request.WorkflowDefinitionId.HasValue)
                return Result<ProjectWorkflowConfigurationDto>.Failure(Error.Validation("Select a published workflow before enabling Custom mode."));

            definition = await _context.Set<WorkflowDefinition>()
                .Include(item => item.Versions)
                .FirstOrDefaultAsync(item => item.Id == request.WorkflowDefinitionId.Value && item.TenantId == tenantId.Value && !item.IsDeleted, cancellationToken);
            if (definition is null)
                return Result<ProjectWorkflowConfigurationDto>.Failure(Error.NotFound("Workflow definition not found."));
            if (!definition.IsActive || !string.Equals(WorkflowDomains.Normalize(definition.Domain), normalizedDomain, StringComparison.OrdinalIgnoreCase))
                return Result<ProjectWorkflowConfigurationDto>.Failure(Error.Validation("The selected workflow is not active or does not match the selected domain."));
            if (!string.Equals(definition.EntityType, WorkflowDomains.EntityTypeFor(normalizedDomain), StringComparison.OrdinalIgnoreCase))
                return Result<ProjectWorkflowConfigurationDto>.Failure(Error.Validation("The selected workflow is not compatible with this domain."));
            if (!definition.ActivePublishedVersionId.HasValue)
                return Result<ProjectWorkflowConfigurationDto>.Failure(Error.Validation("Only a Published workflow can be selected."));
            version = definition.Versions.FirstOrDefault(item => item.Id == definition.ActivePublishedVersionId.Value && item.TenantId == tenantId.Value && !item.IsDeleted);
            if (version is null || version.LifecycleStatus != WorkflowDefinitionVersionLifecycleStatus.Published)
                return Result<ProjectWorkflowConfigurationDto>.Failure(Error.Validation("Only the active Published version can be selected."));
        }

        var row = await _context.Set<ProjectWorkflowConfiguration>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId.Value && item.ProjectId == projectId && item.Domain == normalizedDomain && !item.IsDeleted, cancellationToken);
        var oldValue = row is null ? ProjectWorkflowModes.Standard : row.Mode;
        if (row is null)
        {
            row = new ProjectWorkflowConfiguration
            {
                TenantId = tenantId.Value,
                ProjectId = projectId,
                Domain = normalizedDomain,
                CreatedBy = actor
            };
            _context.Set<ProjectWorkflowConfiguration>().Add(row);
        }
        else
        {
            if (request.RowVersion is not { Length: > 0 })
                return Result<ProjectWorkflowConfigurationDto>.Failure(Error.Conflict("Project workflow configuration is stale. Refresh and retry."));
            _context.Entry(row).Property(item => item.RowVersion).OriginalValue = request.RowVersion;
        }

        row.Mode = mode;
        if (normalizedDomain.Equals(WorkflowDomains.ChangeRequest, StringComparison.OrdinalIgnoreCase))
        {
            row.SponsorApprovalRequired = request.SponsorApprovalRequired ?? row.SponsorApprovalRequired;
            row.AllowAnyOneOptionalApprover = request.AllowAnyOneOptionalApprover ?? row.AllowAnyOneOptionalApprover;
            row.AllowSequentialApprovers = request.AllowSequentialApprovers ?? row.AllowSequentialApprovers;
        }
        row.WorkflowDefinitionId = definition?.Id;
        row.EffectiveWorkflowVersionId = version?.Id;
        row.ConfiguredBy = actor;
        row.ConfiguredAt = DateTime.UtcNow;
        row.UpdatedBy = actor;
        _context.Set<AuditEntry>().Add(new AuditEntry
        {
            TenantId = row.TenantId,
            EntityType = "ProjectWorkflowConfiguration",
            EntityId = row.Id,
            Action = mode == ProjectWorkflowModes.Standard ? "Disabled" : oldValue == ProjectWorkflowModes.Standard ? "Enabled" : "Changed",
            Changes = System.Text.Json.JsonSerializer.Serialize(new { ProjectId = projectId, Domain = normalizedDomain, OldMode = oldValue, NewMode = mode, WorkflowDefinitionId = row.WorkflowDefinitionId }),
            UserId = TryUserId(actor),
            Timestamp = DateTime.UtcNow,
            CreatedBy = actor
        });
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectWorkflowConfigurationDto>.Failure(Error.Conflict("Project workflow configuration changed. Refresh and retry."));
        }

        row.WorkflowDefinition = definition;
        row.EffectiveWorkflowVersion = version;
        return Result<ProjectWorkflowConfigurationDto>.Success(ToDto(row, projectId, normalizedDomain));
    }

    public async Task<Result<ProjectWorkflowSelection?>> ResolveForNewRecordAsync(Guid? projectId, string domain, string entityType, WorkflowExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (!projectId.HasValue) return Result<ProjectWorkflowSelection?>.Success(null);
        var normalizedDomain = WorkflowDomains.Normalize(domain);
        if (!Domains.Contains(normalizedDomain, StringComparer.Ordinal))
            return Result<ProjectWorkflowSelection?>.Failure(Error.Validation("Project workflow settings are not available for this workflow domain."));
        if (!string.Equals(entityType, WorkflowDomains.EntityTypeFor(normalizedDomain), StringComparison.OrdinalIgnoreCase))
            return Result<ProjectWorkflowSelection?>.Failure(Error.Validation("Workflow entity type does not match the requested domain."));

        var tenantId = CurrentTenantId();
        if (!tenantId.HasValue)
            return Result<ProjectWorkflowSelection?>.Failure(Error.Forbidden("A verified tenant context is required to resolve a project workflow."));
        if (context.TenantId != tenantId.Value)
            return Result<ProjectWorkflowSelection?>.Failure(Error.Forbidden("The workflow execution tenant does not match the authenticated tenant."));
        var access = await EnsureProjectAccessAsync(projectId.Value, cancellationToken);
        if (access.IsFailure) return Result<ProjectWorkflowSelection?>.Failure(access.Error!);
        var row = await _context.Set<ProjectWorkflowConfiguration>()
            .AsNoTracking()
            .Include(item => item.WorkflowDefinition)
            .Include(item => item.EffectiveWorkflowVersion)
            .FirstOrDefaultAsync(item => item.TenantId == tenantId.Value && item.ProjectId == projectId.Value && item.Domain == normalizedDomain && !item.IsDeleted, cancellationToken);
        if (row is null || row.Mode != ProjectWorkflowModes.Custom)
            return Result<ProjectWorkflowSelection?>.Success(null);

        var definition = row.WorkflowDefinition;
        var version = row.EffectiveWorkflowVersion;
        if (definition is null || version is null || definition.TenantId != tenantId.Value || version.TenantId != tenantId.Value || !definition.IsActive || definition.ActivePublishedVersionId != version.Id || version.LifecycleStatus != WorkflowDefinitionVersionLifecycleStatus.Published)
            return Result<ProjectWorkflowSelection?>.Failure(Error.Conflict("The selected project workflow is no longer an active Published version."));
        var initial = await _context.Set<WorkflowState>().AsNoTracking().FirstOrDefaultAsync(item => item.WorkflowDefinitionVersionId == version.Id && item.TenantId == tenantId.Value && item.IsInitial && item.IsActive && !item.IsDeleted, cancellationToken);
        if (initial is null)
            return Result<ProjectWorkflowSelection?>.Failure(Error.Validation("The selected workflow has no active initial state."));
        return Result<ProjectWorkflowSelection?>.Success(new ProjectWorkflowSelection(normalizedDomain, entityType, true, definition.Id, version.Id, definition.Code, initial.Code));
    }

    private async Task<Result> EnsureProjectAccessAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var tenantId = CurrentTenantId();
        if (!tenantId.HasValue)
            return Result.Failure(Error.Forbidden("A verified tenant context is required for project workflow settings."));
        if (_managementContext.Current is { } managementContext && managementContext.TenantId != tenantId.Value)
            return Result.Failure(Error.Forbidden("The project workflow request tenant does not match the active management context."));
        if (await _projectLookup.FindAsync(projectId, cancellationToken) is null)
            return Result.Failure(Error.NotFound("Project not found."));
        var scope = _managementContext.Current?.Scope.ProjectIds;
        if (scope is not null && !scope.Contains(projectId))
            return Result.Failure(Error.Forbidden("The project is outside the current management scope."));
        return Result.Success();
    }

    private static ProjectWorkflowConfigurationDto ToDto(ProjectWorkflowConfiguration? row, Guid projectId, string domain) => row is null
        ? new(Guid.Empty, projectId, domain, ProjectWorkflowModes.Standard, null, null, null, null, false, "New records use Standard NEXUS behavior. Existing records remain unchanged.", DateTime.MinValue, string.Empty, null)
        : new(row.Id, projectId, row.Domain, row.Mode, row.WorkflowDefinitionId, row.WorkflowDefinition?.Name, row.EffectiveWorkflowVersionId, row.EffectiveWorkflowVersion?.VersionNumber, row.EffectiveWorkflowVersion?.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Published, row.Mode == ProjectWorkflowModes.Custom ? "New records use the selected Published workflow. Existing workflow instances remain unchanged." : "New records use Standard NEXUS behavior. Existing records remain unchanged.", row.ConfiguredAt, row.ConfiguredBy, row.RowVersion, row.SponsorApprovalRequired, row.AllowAnyOneOptionalApprover, row.AllowSequentialApprovers);

    private bool IsReadOnlyClaim() => string.Equals(_httpContextAccessor.HttpContext?.User.FindFirstValue("readOnly"), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(_httpContextAccessor.HttpContext?.User.FindFirstValue("viewAs"), "true", StringComparison.OrdinalIgnoreCase);

    private bool HasPermission(string permission) =>
        _httpContextAccessor.HttpContext?.User.HasClaim("permission", permission) == true;

    private Guid? TryUserId(string actor) => Guid.TryParse(actor, out var id) ? id : null;
    private Guid? CurrentTenantId() => Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("tenantId"), out var id) && id != Guid.Empty ? id : null;
}
