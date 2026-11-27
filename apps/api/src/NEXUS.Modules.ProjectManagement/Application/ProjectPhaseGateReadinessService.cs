using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;

namespace NEXUS.Modules.ProjectManagement.Application;

/// <summary>
/// Canonical Phase Gate reader and transition service. It derives completion from persisted
/// document/version records and configured storage metadata; it never trusts a client checklist flag.
/// </summary>
public sealed class ProjectPhaseGateReadinessService : IProjectPhaseGateReadinessService
{
    private const string ProjectViewPermission = "Project.View";
    private const string ProjectChangePermission = "ProjectPhase.Change";
    private const string DocumentViewPermission = "Document.View";
    private const string DocumentEditPermission = "Document.Edit";

    private readonly DbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor? _managementContext;
    private readonly IProjectPhaseGateConfigurationReader _configurationReader;
    private readonly IDocumentService _documentService;
    private readonly IStorageProvider _storage;
    private readonly IAuditService _auditService;
    private readonly IProjectOrganizationLookup _organizationLookup;

    public ProjectPhaseGateReadinessService(
        DbContext context,
        IHttpContextAccessor httpContextAccessor,
        IProjectPhaseGateConfigurationReader configurationReader,
        IDocumentService documentService,
        IStorageProvider storage,
        IAuditService auditService,
        IProjectOrganizationLookup organizationLookup,
        IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _configurationReader = configurationReader;
        _documentService = documentService;
        _storage = storage;
        _auditService = auditService;
        _organizationLookup = organizationLookup;
        _managementContext = managementContext;
    }

    public async Task<Result<ProjectPhaseGateReadinessDto>> GetAsync(
        Guid projectId,
        Guid? targetPhaseId = null,
        CancellationToken cancellationToken = default)
    {
        var accessError = RequireProjectAccess(projectId, ProjectViewPermission, requireDocumentView: true);
        if (accessError is not null)
            return Result<ProjectPhaseGateReadinessDto>.Failure(accessError);

        var rows = await GetManyCoreAsync([projectId], targetPhaseId, cancellationToken);
        if (!rows.TryGetValue(projectId, out var readiness))
            return Result<ProjectPhaseGateReadinessDto>.Failure(Error.NotFound("Project not found."));

        var project = await GetProjectInScopeAsync(projectId, tracking: false, cancellationToken);
        if (project is null)
            return Result<ProjectPhaseGateReadinessDto>.Failure(Error.NotFound("Project not found."));
        if (!project.ProjectPhaseId.HasValue)
            return Result<ProjectPhaseGateReadinessDto>.Success(readiness);

        var sourcePhase = await _context.Set<ProjectPhaseCatalogEntry>().AsNoTracking()
            .FirstOrDefaultAsync(phase => phase.TenantId == project.TenantId
                && phase.Id == project.ProjectPhaseId.Value && !phase.IsDeleted, cancellationToken);
        // Keep the existing pinned Phase Gate path for older phase catalog rows that have
        // no Reference Data category assignment. Once a phase is mapped to a category,
        // that mapping is authoritative and the selected target cannot change its checklist.
        if (sourcePhase?.DocumentCategoryId is null)
            return Result<ProjectPhaseGateReadinessDto>.Success(readiness);

        var organizationId = await ResolveOrganizationIdAsync(project, cancellationToken);
        var categoryEvaluation = await EvaluateCurrentPhaseCategoryReadinessAsync(
            project, organizationId, sourcePhase, cancellationToken);
        if (!categoryEvaluation.IsConfigured)
        {
            return Result<ProjectPhaseGateReadinessDto>.Success(readiness with
            {
                SnapshotId = null,
                ConfigurationId = null,
                ConfigurationVersionId = null,
                ConfigurationVersionNumber = null,
                ReadinessStatus = ProjectPhaseGateReadinessStatuses.NotConfigured,
                MandatoryRequirementCount = 0,
                SatisfiedMandatoryCount = 0,
                MissingMandatoryCount = 0,
                OptionalRequirementCount = 0,
                SatisfiedOptionalCount = 0,
                ReadinessPercentage = null,
                Requirements = Array.Empty<ProjectPhaseGateRequirementStatusDto>()
            });
        }

        var categoryCounts = CountRequirements(categoryEvaluation.Requirements);
        var categoryPercentage = categoryCounts.Mandatory == 0
            ? (decimal?)null
            : Math.Round(categoryCounts.SatisfiedMandatory * 100m / categoryCounts.Mandatory, 2, MidpointRounding.AwayFromZero);
        return Result<ProjectPhaseGateReadinessDto>.Success(readiness with
        {
            SnapshotId = null,
            ConfigurationId = null,
            ConfigurationVersionId = null,
            ConfigurationVersionNumber = null,
            ReadinessStatus = GetReadinessStatus(categoryEvaluation.Requirements),
            MandatoryRequirementCount = categoryCounts.Mandatory,
            SatisfiedMandatoryCount = categoryCounts.SatisfiedMandatory,
            MissingMandatoryCount = categoryCounts.MissingMandatory,
            OptionalRequirementCount = categoryCounts.Optional,
            SatisfiedOptionalCount = categoryCounts.SatisfiedOptional,
            ReadinessPercentage = categoryPercentage,
            Requirements = categoryEvaluation.Requirements
        });
    }

    public async Task<Result<ProjectPhaseGateReadinessDto>> LinkDocumentAsync(
        Guid projectId,
        Guid requirementSnapshotId,
        Guid documentId,
        Guid documentVersionId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var accessError = RequireProjectMutationAccess(projectId, actorId, DocumentEditPermission,
            requireDocumentView: true, requirePhaseChange: false);
        if (accessError is not null)
            return Result<ProjectPhaseGateReadinessDto>.Failure(accessError);
        if (requirementSnapshotId == Guid.Empty || documentId == Guid.Empty || documentVersionId == Guid.Empty)
            return Result<ProjectPhaseGateReadinessDto>.Failure(Error.Validation("Requirement, document, and document-version references are required."));

        var project = await GetProjectInScopeAsync(projectId, tracking: false, cancellationToken);
        if (project is null)
            return Result<ProjectPhaseGateReadinessDto>.Failure(Error.NotFound("Project not found."));
        var organizationId = await ResolveOrganizationIdAsync(project, cancellationToken);
        if (!organizationId.HasValue)
            return Result<ProjectPhaseGateReadinessDto>.Failure(Error.BusinessValidation(
                "PROJECT_ORGANIZATION_NOT_CONFIGURED", "The project is not associated with an active Organization."));

        var requirement = await _context.Set<ProjectPhaseGateRequirementSnapshot>()
            .Where(item => item.TenantId == project.TenantId && !item.IsDeleted && item.Id == requirementSnapshotId)
            .Join(_context.Set<ProjectPhaseGateSnapshot>().Where(snapshot => snapshot.TenantId == project.TenantId
                && !snapshot.IsDeleted && snapshot.ProjectId == project.Id && snapshot.OrganizationId == organizationId.Value),
                item => item.SnapshotId,
                snapshot => snapshot.Id,
                (item, snapshot) => new { Item = item, Snapshot = snapshot })
            .FirstOrDefaultAsync(cancellationToken);
        if (requirement is null)
            return Result<ProjectPhaseGateReadinessDto>.Failure(Error.NotFound("The phase-gate requirement is not part of this project."));

        var existingEvidence = await _context.Set<ProjectPhaseGateEvidence>()
            .FirstOrDefaultAsync(item => item.TenantId == project.TenantId && !item.IsDeleted
                && item.ProjectId == project.Id && item.OrganizationId == organizationId.Value
                && item.RequirementSnapshotId == requirementSnapshotId, cancellationToken);
        if (existingEvidence is not null
            && (existingEvidence.DocumentId != documentId || existingEvidence.DocumentVersionId != documentVersionId))
            return Result<ProjectPhaseGateReadinessDto>.Failure(Error.Conflict(
                "A document is already linked to this requirement. Unlink it before linking a different version."));

        await using var transaction = await BeginTransactionIfNeededAsync(cancellationToken);
        try
        {
            var linkedDocument = await _documentService.LinkProjectDocumentToTemplateAsync(
                project.Id,
                documentId,
                documentVersionId,
                requirement.Item.TemplateId,
                requirement.Item.TemplateVersionId,
                actorId,
                cancellationToken);
            if (linkedDocument.IsFailure)
            {
                await RollbackCurrentTransactionAsync(cancellationToken);
                return Result<ProjectPhaseGateReadinessDto>.Failure(linkedDocument.Error!);
            }

            var compliance = await CompareDocumentToTemplateAsync(
                project, organizationId.Value, requirement.Item, documentId, documentVersionId, cancellationToken);
            var evidence = existingEvidence ?? new ProjectPhaseGateEvidence
            {
                TenantId = project.TenantId,
                ProjectId = project.Id,
                OrganizationId = organizationId.Value,
                RequirementSnapshotId = requirementSnapshotId,
                DocumentId = documentId,
                DocumentVersionId = documentVersionId,
                CreatedBy = actorId.ToString()
            };
            evidence.ComplianceStatus = compliance.Status;
            evidence.ExpectedFileType = compliance.ExpectedFileType;
            evidence.DetectedFileType = compliance.DetectedFileType;
            evidence.ComplianceFindingsJson = JsonSerializer.Serialize(compliance.Findings);
            evidence.ComparedAt = compliance.ComparedAt;
            evidence.ValidatorVersion = compliance.ValidatorVersion;
            evidence.UpdatedAt = DateTime.UtcNow;
            evidence.UpdatedBy = actorId.ToString();
            if (existingEvidence is null)
            {
                _context.Set<ProjectPhaseGateEvidence>().Add(evidence);
                var append = await _auditService.AppendAsync(CreateEvidenceAudit(
                    project, evidence, "EvidenceLinked", actorId, requirement.Item.TemplateId, requirement.Item.TemplateVersionId), cancellationToken);
                if (append.IsFailure)
                {
                    await RollbackCurrentTransactionAsync(cancellationToken);
                    return Result<ProjectPhaseGateReadinessDto>.Failure(append.Error!);
                }
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                var append = await _auditService.AppendAsync(CreateEvidenceAudit(
                    project, evidence, "EvidenceComplianceValidated", actorId, requirement.Item.TemplateId, requirement.Item.TemplateVersionId), cancellationToken);
                if (append.IsFailure)
                {
                    await RollbackCurrentTransactionAsync(cancellationToken);
                    return Result<ProjectPhaseGateReadinessDto>.Failure(append.Error!);
                }
                await _context.SaveChangesAsync(cancellationToken);
            }

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await RollbackCurrentTransactionAsync(cancellationToken);
            return Result<ProjectPhaseGateReadinessDto>.Failure(Error.Conflict(
                "The requirement evidence changed concurrently. Refresh the checklist and try again."));
        }

        return await GetAsync(projectId, requirement.Snapshot.TargetPhaseId, cancellationToken);
    }

    public async Task<Result<ProjectPhaseGateReadinessDto>> UnlinkDocumentAsync(
        Guid projectId,
        Guid requirementSnapshotId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var accessError = RequireProjectMutationAccess(projectId, actorId, DocumentEditPermission,
            requireDocumentView: true, requirePhaseChange: false);
        if (accessError is not null)
            return Result<ProjectPhaseGateReadinessDto>.Failure(accessError);

        var project = await GetProjectInScopeAsync(projectId, tracking: true, cancellationToken);
        if (project is null)
            return Result<ProjectPhaseGateReadinessDto>.Failure(Error.NotFound("Project not found."));
        var organizationId = await ResolveOrganizationIdAsync(project, cancellationToken);
        if (!organizationId.HasValue)
            return Result<ProjectPhaseGateReadinessDto>.Failure(Error.NotFound("Project Organization not found."));

        var requirement = await _context.Set<ProjectPhaseGateRequirementSnapshot>()
            .Where(item => item.TenantId == project.TenantId && !item.IsDeleted && item.Id == requirementSnapshotId)
            .Join(_context.Set<ProjectPhaseGateSnapshot>().Where(snapshot => snapshot.TenantId == project.TenantId
                && !snapshot.IsDeleted && snapshot.ProjectId == project.Id && snapshot.OrganizationId == organizationId.Value),
                item => item.SnapshotId,
                snapshot => snapshot.Id,
                (item, snapshot) => new { Item = item, Snapshot = snapshot })
            .FirstOrDefaultAsync(cancellationToken);
        if (requirement is null)
            return Result<ProjectPhaseGateReadinessDto>.Failure(Error.NotFound("The phase-gate requirement is not part of this project."));

        var evidence = await _context.Set<ProjectPhaseGateEvidence>()
            .FirstOrDefaultAsync(item => item.TenantId == project.TenantId && !item.IsDeleted
                && item.ProjectId == project.Id && item.OrganizationId == organizationId.Value
                && item.RequirementSnapshotId == requirementSnapshotId, cancellationToken);
        if (evidence is null)
            return await GetAsync(projectId, requirement.Snapshot.TargetPhaseId, cancellationToken);

        await using var transaction = await BeginTransactionIfNeededAsync(cancellationToken);
        evidence.IsDeleted = true;
        evidence.UpdatedBy = actorId.ToString();
        evidence.UpdatedAt = DateTime.UtcNow;
        var append = await _auditService.AppendAsync(CreateEvidenceAudit(
            project, evidence, "EvidenceUnlinked", actorId, requirement.Item.TemplateId, requirement.Item.TemplateVersionId), cancellationToken);
        if (append.IsFailure)
        {
            await RollbackCurrentTransactionAsync(cancellationToken);
            return Result<ProjectPhaseGateReadinessDto>.Failure(append.Error!);
        }
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackCurrentTransactionAsync(cancellationToken);
            return Result<ProjectPhaseGateReadinessDto>.Failure(Error.Conflict(
                "The requirement evidence changed concurrently. Refresh the checklist and try again."));
        }

        return await GetAsync(projectId, requirement.Snapshot.TargetPhaseId, cancellationToken);
    }

    public async Task<Result<ProjectPhaseTransitionResultDto>> ChangePhaseAsync(
        Guid projectId,
        Guid targetPhaseId,
        string expectedProjectVersionToken,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var accessError = RequireProjectMutationAccess(projectId, actorId, ProjectChangePermission,
            requireDocumentView: false, requirePhaseChange: true);
        if (accessError is not null)
            return Result<ProjectPhaseTransitionResultDto>.Failure(accessError);
        if (targetPhaseId == Guid.Empty)
            return Result<ProjectPhaseTransitionResultDto>.Failure(Error.Validation("A target phase is required.", "targetPhaseId"));
        if (!TryDecodeVersionToken(expectedProjectVersionToken, out var expectedVersion))
            return Result<ProjectPhaseTransitionResultDto>.Failure(Error.Validation("A valid project concurrency token is required.", "expectedProjectVersionToken"));

        var project = await GetProjectInScopeAsync(projectId, tracking: true, cancellationToken);
        if (project is null)
            return Result<ProjectPhaseTransitionResultDto>.Failure(Error.NotFound("Project not found."));
        var sourcePhase = project.ProjectPhaseId.HasValue
            ? await _context.Set<ProjectPhaseCatalogEntry>().FirstOrDefaultAsync(item => item.Id == project.ProjectPhaseId.Value
                && item.TenantId == project.TenantId && !item.IsDeleted, cancellationToken)
            : null;
        var destinationPhase = await _context.Set<ProjectPhaseCatalogEntry>().FirstOrDefaultAsync(item => item.Id == targetPhaseId
            && item.TenantId == project.TenantId && !item.IsDeleted && item.IsActive, cancellationToken);
        if (sourcePhase is null)
            return Result<ProjectPhaseTransitionResultDto>.Failure(NotConfiguredError(project, project.Phase, null));
        if (destinationPhase is null)
            return Result<ProjectPhaseTransitionResultDto>.Failure(Error.Validation("The selected target phase is not active." , "targetPhaseId"));
        if (sourcePhase.Id == destinationPhase.Id)
            return Result<ProjectPhaseTransitionResultDto>.Failure(Error.Validation("The project is already in the requested phase.", "targetPhaseId"));
        var previousPhase = NormalizePhase(sourcePhase.Code);
        var normalizedTarget = NormalizePhase(destinationPhase.Code);
        if (!project.RowVersion.SequenceEqual(expectedVersion))
            return Result<ProjectPhaseTransitionResultDto>.Failure(Error.Conflict("The project changed after the checklist was loaded. Refresh it before changing phase."));

        var organizationId = await ResolveOrganizationIdAsync(project, cancellationToken);
        if ((sourcePhase.OrganizationId.HasValue && sourcePhase.OrganizationId != organizationId)
            || (destinationPhase.OrganizationId.HasValue && destinationPhase.OrganizationId != organizationId))
            return Result<ProjectPhaseTransitionResultDto>.Failure(Error.Validation("The selected phase is outside this project's lifecycle catalog scope.", "targetPhaseId"));

        ProjectPhaseGateSnapshot? snapshot = null;
        GateEvaluation? evaluation = null;
        if (organizationId.HasValue)
        {
            var snapshotResult = await GetOrCreateSnapshotAsync(project, organizationId.Value, sourcePhase, destinationPhase, actorId, cancellationToken);
            if (snapshotResult.IsFailure)
                return Result<ProjectPhaseTransitionResultDto>.Failure(snapshotResult.Error!);
            snapshot = snapshotResult.Value;
        }
        await using var transaction = await BeginTransactionIfNeededAsync(
            cancellationToken, System.Data.IsolationLevel.Serializable);

        if (organizationId.HasValue && sourcePhase.DocumentCategoryId.HasValue)
        {
            var categoryEvaluation = await EvaluateCurrentPhaseCategoryReadinessAsync(
                project, organizationId, sourcePhase, cancellationToken);
            evaluation = categoryEvaluation.Evaluation;
        }
        else if (snapshot is not null && organizationId.HasValue)
        {
            evaluation = await EvaluateSnapshotAsync(project, organizationId.Value, snapshot, cancellationToken);
        }

        project.ProjectPhaseId = destinationPhase.Id;
        project.Phase = destinationPhase.Code;
        project.UpdatedBy = actorId.ToString();
        project.UpdatedAt = DateTime.UtcNow;
        _context.Entry(project).Property(item => item.RowVersion).OriginalValue = expectedVersion;
        var transitionedAt = DateTimeOffset.UtcNow;
        var audit = CreateTransitionAudit(project, sourcePhase, destinationPhase, organizationId, snapshot, evaluation,
            previousPhase, normalizedTarget, actorId, transitionedAt);
        var appendResult = await _auditService.AppendAsync(audit, cancellationToken);
        if (appendResult.IsFailure)
        {
            await RollbackCurrentTransactionAsync(cancellationToken);
            return Result<ProjectPhaseTransitionResultDto>.Failure(appendResult.Error!);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackCurrentTransactionAsync(cancellationToken);
            return Result<ProjectPhaseTransitionResultDto>.Failure(Error.Conflict("The project changed while the phase transition was being submitted. Refresh and try again."));
        }

        return Result<ProjectPhaseTransitionResultDto>.Success(new ProjectPhaseTransitionResultDto(
            project.Id,
            sourcePhase.Id,
            destinationPhase.Id,
            previousPhase,
            normalizedTarget,
            snapshot?.ConfigurationVersionId,
            snapshot?.ConfigurationVersionNumber,
            transitionedAt,
            Convert.ToBase64String(project.RowVersion)));
    }

    public async Task<Result<IReadOnlyList<ProjectPhaseHistoryItemDto>>> GetPhaseHistoryAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        // History is project-scoped presentation data. It requires Project.View and the
        // project management scope only; unlike readiness it must not require Document.View.
        var accessError = RequireProjectAccess(projectId, ProjectViewPermission, requireDocumentView: false);
        if (accessError is not null)
            return Result<IReadOnlyList<ProjectPhaseHistoryItemDto>>.Failure(accessError);

        var tenantId = GetCurrentTenantId();
        var page = await _auditService.GetPagedAsync(1, 100, "ProjectPhase", "Change", projectId,
            cancellationToken: cancellationToken);

        // The AuditEntry store already applies a tenant query filter. Clamp again here so a
        // caller-supplied projection, or a future provider change, can never surface another
        // tenant's transition in this project's history.
        var history = page.Items
            .Where(entry => entry.TenantId == tenantId && !entry.IsDeleted)
            .OrderByDescending(entry => entry.Timestamp)
            .ThenByDescending(entry => entry.Id)
            .Select(entry => MapPhaseHistoryItem(projectId, entry))
            .ToArray();
        return Result<IReadOnlyList<ProjectPhaseHistoryItemDto>>.Success(history);
    }

    private async Task<IReadOnlyDictionary<Guid, ProjectPhaseGateReadinessDto>> GetManyCoreAsync(
        IReadOnlyCollection<Guid> projectIds,
        Guid? targetPhaseId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == Guid.Empty) return new Dictionary<Guid, ProjectPhaseGateReadinessDto>();
        var ids = projectIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, ProjectPhaseGateReadinessDto>();

        // The public project route enforces the current management scope. Batch callers provide
        // already-authorized ids; this query still clamps them to the authenticated Tenant.
        var projects = await _context.Set<Project>().AsNoTracking()
            .Where(project => ids.Contains(project.Id) && project.TenantId == tenantId && !project.IsDeleted)
            .Select(project => new ProjectScopeRow(project.Id, project.TenantId, project.ProgramId,
                project.PortfolioId, project.ProjectPhaseId, project.Phase, project.Status, project.RowVersion))
            .ToListAsync(cancellationToken);
        if (projects.Count == 0) return new Dictionary<Guid, ProjectPhaseGateReadinessDto>();

        var referencedPhaseIds = projects.Where(project => project.PhaseId.HasValue).Select(project => project.PhaseId!.Value).ToHashSet();
        if (targetPhaseId.HasValue) referencedPhaseIds.Add(targetPhaseId.Value);
        var phaseEntries = referencedPhaseIds.Count == 0
            ? new List<ProjectPhaseCatalogEntry>()
            : await _context.Set<ProjectPhaseCatalogEntry>().AsNoTracking()
                .Where(phase => phase.TenantId == tenantId && !phase.IsDeleted && referencedPhaseIds.Contains(phase.Id))
                .ToListAsync(cancellationToken);
        var phaseById = phaseEntries.ToDictionary(phase => phase.Id);
        var phaseByProjectId = projects.ToDictionary(project => project.Id,
            project => project.PhaseId.HasValue && phaseById.TryGetValue(project.PhaseId.Value, out var phase) ? phase : null);
        var requestedTargetPhase = targetPhaseId.HasValue && phaseById.TryGetValue(targetPhaseId.Value, out var requestedPhase)
            ? requestedPhase
            : null;

        var portfolioByProject = await ResolvePortfolioIdsAsync(projects, tenantId, cancellationToken);
        var organizationByPortfolio = await ResolveOrganizationIdsAsync(portfolioByProject.Values.Where(value => value.HasValue)
            .Select(value => value!.Value).Distinct().ToArray(), tenantId, cancellationToken);
        var organizationByProject = projects.ToDictionary(
            project => project.Id,
            project => portfolioByProject.TryGetValue(project.Id, out var portfolioId) && portfolioId.HasValue
                && organizationByPortfolio.TryGetValue(portfolioId.Value, out var organizationId)
                    ? organizationId
                    : (Guid?)null);

        var phaseByProject = projects.ToDictionary(project => project.Id, project =>
            NormalizePhase(phaseByProjectId[project.Id]?.Code ?? project.Phase));
        var existingSnapshots = await _context.Set<ProjectPhaseGateSnapshot>().AsNoTracking()
            .Where(snapshot => snapshot.TenantId == tenantId && !snapshot.IsDeleted && ids.Contains(snapshot.ProjectId))
            .OrderByDescending(snapshot => snapshot.CreatedAt)
            .ToListAsync(cancellationToken);
        var completedTransitions = new Dictionary<Guid, CompletedTransition>();
        if (targetPhaseId.HasValue && requestedTargetPhase is not null)
        {
            var historicalIds = projects.Where(project => project.PhaseId == targetPhaseId.Value)
                .Select(project => project.Id).ToArray();
            if (historicalIds.Length > 0)
            {
                var audits = await _context.Set<AuditEntry>().AsNoTracking()
                    .Where(entry => entry.TenantId == tenantId && !entry.IsDeleted
                        && entry.EntityType == "ProjectPhase" && entry.Action == "Change"
                        && entry.ProjectId.HasValue && historicalIds.Contains(entry.ProjectId.Value))
                    .OrderByDescending(entry => entry.Timestamp)
                    .ToListAsync(cancellationToken);
                foreach (var historicalId in historicalIds)
                {
                    var target = NormalizePhase(requestedTargetPhase.Code);
                    var completed = audits.Where(entry => entry.ProjectId == historicalId)
                        .Select(ParseCompletedTransition)
                        .FirstOrDefault(item => item is not null
                            && item.TargetPhase.Equals(target, StringComparison.OrdinalIgnoreCase));
                    if (completed is not null) completedTransitions[historicalId] = completed;
                }
            }
        }

        var configsByScope = new Dictionary<string, IReadOnlyList<ProjectPhaseGateConfigurationDto>>(StringComparer.Ordinal);
        foreach (var group in projects
            .Where(project => organizationByProject[project.Id].HasValue && project.PhaseId.HasValue)
            .GroupBy(project => (OrganizationId: organizationByProject[project.Id]!.Value, SourcePhaseId: project.PhaseId!.Value)))
        {
            var configs = await _configurationReader.GetCurrentBySourceAsync(tenantId, group.Key.OrganizationId,
                group.Key.SourcePhaseId, cancellationToken);
            configsByScope[ScopeKey(group.Key.OrganizationId, group.Key.SourcePhaseId)] = configs;
        }

        // Phase Gate configuration is a document-readiness rule for a source/target
        // pair. Its configured targets drive readiness previews; they do not limit
        // which catalog phases a project may select.
        var configuredTargetIds = configsByScope.Values.SelectMany(configs => configs)
            .Select(config => config.TargetPhaseId)
            .Concat(existingSnapshots.Where(snapshot => ids.Contains(snapshot.ProjectId))
                .Select(snapshot => snapshot.TargetPhaseId))
            .Where(id => !phaseById.ContainsKey(id)).Distinct().ToArray();
        if (configuredTargetIds.Length > 0)
        {
            var configuredTargets = await _context.Set<ProjectPhaseCatalogEntry>().AsNoTracking()
                .Where(phase => phase.TenantId == tenantId && !phase.IsDeleted && configuredTargetIds.Contains(phase.Id))
                .ToListAsync(cancellationToken);
            foreach (var phase in configuredTargets) phaseById[phase.Id] = phase;
        }

        var chosenTargetByProject = new Dictionary<Guid, string?>();
        foreach (var project in projects)
        {
            var source = phaseByProject[project.Id];
            if (targetPhaseId.HasValue && requestedTargetPhase is not null)
            {
                var sourcePhase = phaseByProjectId[project.Id];
                var isCurrentHistoricalTarget = project.PhaseId == requestedTargetPhase.Id;
                var isAvailableInProjectScope = !requestedTargetPhase.OrganizationId.HasValue
                    || requestedTargetPhase.OrganizationId == organizationByProject[project.Id];
                chosenTargetByProject[project.Id] = isCurrentHistoricalTarget
                    || (sourcePhase is not null && requestedTargetPhase.IsActive && isAvailableInProjectScope)
                    ? NormalizePhase(requestedTargetPhase.Code)
                    : null;
                continue;
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                chosenTargetByProject[project.Id] = null;
                continue;
            }

            var sourcePhaseId = project.PhaseId;
            var savedSnapshotsForSource = existingSnapshots.Where(snapshot => snapshot.ProjectId == project.Id
                && snapshot.OrganizationId == organizationByProject[project.Id]
                && snapshot.SourcePhaseId == sourcePhaseId).ToArray();
            var configList = organizationByProject[project.Id].HasValue
                && sourcePhaseId.HasValue
                && configsByScope.TryGetValue(ScopeKey(organizationByProject[project.Id]!.Value, sourcePhaseId.Value), out var foundConfigs)
                    ? foundConfigs
                    : Array.Empty<ProjectPhaseGateConfigurationDto>();

            var configuredTargets = configList.Select(config => config.TargetPhaseId)
                .Distinct()
                .Where(id => phaseById.TryGetValue(id, out var target) && target.IsActive
                    && (!target.OrganizationId.HasValue || target.OrganizationId == organizationByProject[project.Id]))
                .Select(id => phaseById[id].Code)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            chosenTargetByProject[project.Id] = configuredTargets.Length == 1
                ? configuredTargets[0]
                : null;
        }

        var selectedSnapshots = new Dictionary<Guid, ProjectPhaseGateSnapshot>();
        var newSnapshots = new List<ProjectPhaseGateSnapshot>();
        var newRequirements = new List<ProjectPhaseGateRequirementSnapshot>();
        foreach (var project in projects)
        {
            var source = phaseByProject[project.Id];
            var target = chosenTargetByProject[project.Id];
            var organizationId = organizationByProject[project.Id];
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target) || !organizationId.HasValue)
                continue;

            var sourcePhaseId = project.PhaseId;
            var targetPhase = phaseById.Values.FirstOrDefault(phase =>
                (!phase.OrganizationId.HasValue || phase.OrganizationId == organizationId)
                && phase.Code.Equals(target, StringComparison.OrdinalIgnoreCase));
            var effectiveTargetPhaseId = targetPhase?.Id
                ?? (requestedTargetPhase?.Code.Equals(target, StringComparison.OrdinalIgnoreCase) == true ? targetPhaseId : null);
            var saved = effectiveTargetPhaseId.HasValue ? existingSnapshots.FirstOrDefault(snapshot => snapshot.ProjectId == project.Id
                && snapshot.OrganizationId == organizationId.Value
                && snapshot.SourcePhaseId == sourcePhaseId
                && snapshot.TargetPhaseId == effectiveTargetPhaseId.Value) : null;
            if (saved is null && effectiveTargetPhaseId == sourcePhaseId
                && completedTransitions.TryGetValue(project.Id, out var completed))
            {
                saved = existingSnapshots.FirstOrDefault(snapshot => snapshot.ProjectId == project.Id
                    && snapshot.OrganizationId == organizationId.Value
                    && ((completed.SourcePhaseId.HasValue && completed.TargetPhaseId.HasValue
                            && snapshot.SourcePhaseId == completed.SourcePhaseId.Value
                            && snapshot.TargetPhaseId == completed.TargetPhaseId.Value)
                        || (snapshot.SourcePhase.Equals(completed.SourcePhase, StringComparison.OrdinalIgnoreCase)
                            && snapshot.TargetPhase.Equals(completed.TargetPhase, StringComparison.OrdinalIgnoreCase)))
                    && (!completed.ConfigurationVersionId.HasValue
                        || snapshot.ConfigurationVersionId == completed.ConfigurationVersionId.Value));
            }
            if (saved is not null)
            {
                selectedSnapshots[project.Id] = saved;
                continue;
            }

            if (targetPhase is null || sourcePhaseId is null || targetPhase.Id == sourcePhaseId.Value) continue;
            var configured = await GetConfigurationAsync(tenantId, organizationId.Value, sourcePhaseId!.Value,
                effectiveTargetPhaseId!.Value,
                configsByScope, cancellationToken);
            if (configured is null) continue;
            if (!await HasAvailableTemplateReferencesAsync(tenantId, organizationId.Value, configured, cancellationToken))
                continue;
            var sourceEntry = phaseByProjectId[project.Id];
            if (sourceEntry is null) continue;
            var snapshot = MakeSnapshot(project.Id, tenantId, organizationId.Value, sourceEntry, targetPhase, configured, GetCurrentActorId()?.ToString() ?? "System");
            newSnapshots.Add(snapshot);
            newRequirements.AddRange(MakeRequirementSnapshots(snapshot, tenantId, configured, GetCurrentActorId()?.ToString() ?? "System"));
            selectedSnapshots[project.Id] = snapshot;
        }

        if (newSnapshots.Count > 0)
        {
            await using var snapshotTransaction = await BeginTransactionIfNeededAsync(cancellationToken);
            _context.Set<ProjectPhaseGateSnapshot>().AddRange(newSnapshots);
            _context.Set<ProjectPhaseGateRequirementSnapshot>().AddRange(newRequirements);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                if (snapshotTransaction is not null)
                    await snapshotTransaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                await RollbackCurrentTransactionAsync(cancellationToken);
                foreach (var entity in newSnapshots) _context.Entry(entity).State = EntityState.Detached;
                foreach (var entity in newRequirements) _context.Entry(entity).State = EntityState.Detached;
                // Another request may have created the same immutable snapshot first.
                var racedIds = newSnapshots.Select(snapshot => snapshot.ProjectId).ToArray();
                var raced = await _context.Set<ProjectPhaseGateSnapshot>().AsNoTracking()
                    .Where(snapshot => snapshot.TenantId == tenantId && !snapshot.IsDeleted && racedIds.Contains(snapshot.ProjectId))
                    .ToListAsync(cancellationToken);
                foreach (var snapshot in raced)
                {
                    if (selectedSnapshots.TryGetValue(snapshot.ProjectId, out var expected)
                        && snapshot.OrganizationId == expected.OrganizationId
                        && snapshot.SourcePhaseId == expected.SourcePhaseId
                        && snapshot.TargetPhaseId == expected.TargetPhaseId)
                        selectedSnapshots[snapshot.ProjectId] = snapshot;
                }
            }
        }

        var snapshotIds = selectedSnapshots.Values.Select(snapshot => snapshot.Id).Distinct().ToArray();
        var requirementSnapshots = snapshotIds.Length == 0
            ? new List<ProjectPhaseGateRequirementSnapshot>()
            : await _context.Set<ProjectPhaseGateRequirementSnapshot>().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && snapshotIds.Contains(item.SnapshotId))
                .OrderBy(item => item.TemplateName)
                .ToListAsync(cancellationToken);
        var requirementIds = requirementSnapshots.Select(item => item.Id).ToArray();
        var evidence = requirementIds.Length == 0
            ? new List<ProjectPhaseGateEvidence>()
            : await _context.Set<ProjectPhaseGateEvidence>().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && requirementIds.Contains(item.RequirementSnapshotId))
                .ToListAsync(cancellationToken);

        var documentIds = evidence.Select(item => item.DocumentId).Distinct().ToArray();
        var documents = documentIds.Length == 0
            ? new List<Document>()
            : await _context.Set<Document>().AsNoTracking()
                .Where(document => document.TenantId == tenantId && !document.IsDeleted && documentIds.Contains(document.Id))
                .ToListAsync(cancellationToken);
        var versionIds = evidence.Select(item => item.DocumentVersionId).Distinct().ToArray();
        var versions = versionIds.Length == 0
            ? new List<DocumentVersion>()
            : await _context.Set<DocumentVersion>().AsNoTracking()
                .Where(version => version.TenantId == tenantId && !version.IsDeleted && versionIds.Contains(version.Id))
                .ToListAsync(cancellationToken);
        var templateIds = requirementSnapshots.Select(item => item.TemplateId).Distinct().ToArray();
        var templates = templateIds.Length == 0
            ? new List<Document>()
            : await _context.Set<Document>().AsNoTracking()
                .Where(template => template.TenantId == tenantId && !template.IsDeleted && template.IsTemplate
                    && templateIds.Contains(template.Id))
                .ToListAsync(cancellationToken);
        var templateVersionIds = requirementSnapshots.Select(item => item.TemplateVersionId).Distinct().ToArray();
        var templateVersions = templateVersionIds.Length == 0
            ? new List<DocumentVersion>()
            : await _context.Set<DocumentVersion>().AsNoTracking()
                .Where(version => version.TenantId == tenantId && !version.IsDeleted && templateVersionIds.Contains(version.Id))
                .ToListAsync(cancellationToken);

        var statusByRequirement = await BuildRequirementStatusesAsync(
            projects, organizationByProject, selectedSnapshots, requirementSnapshots, evidence, documents, versions,
            templates, templateVersions, cancellationToken);

        var result = new Dictionary<Guid, ProjectPhaseGateReadinessDto>();
        foreach (var project in projects)
        {
            var currentPhase = phaseByProject[project.Id] ?? string.Empty;
            var currentPhaseEntry = phaseByProjectId[project.Id];
            var orgId = organizationByProject[project.Id];
            var sourceSnapshots = existingSnapshots.Where(snapshot => snapshot.ProjectId == project.Id
                && !string.IsNullOrWhiteSpace(currentPhase)
                && snapshot.OrganizationId == orgId
                && snapshot.SourcePhaseId == project.PhaseId).ToArray();
            var sourceConfigs = orgId.HasValue && !string.IsNullOrWhiteSpace(currentPhase)
                && project.PhaseId.HasValue
                && configsByScope.TryGetValue(ScopeKey(orgId.Value, project.PhaseId.Value), out var found)
                    ? found
                    : Array.Empty<ProjectPhaseGateConfigurationDto>();
            var selectedTarget = chosenTargetByProject[project.Id];
            var selectedTargetEntry = selectedTarget is null || currentPhaseEntry is null
                ? null
                : phaseById.Values.FirstOrDefault(phase =>
                    (phase.IsActive || phase.Id == project.PhaseId)
                    && (!phase.OrganizationId.HasValue || phase.OrganizationId == orgId)
                    && phase.Code.Equals(selectedTarget, StringComparison.OrdinalIgnoreCase));
            var selectedSnapshot = selectedSnapshots.GetValueOrDefault(project.Id);
            var transitionSnapshots = sourceSnapshots.ToList();
            if (selectedSnapshot is not null && selectedSnapshot.SourcePhaseId == project.PhaseId
                && transitionSnapshots.All(snapshot => snapshot.Id != selectedSnapshot.Id))
                transitionSnapshots.Add(selectedSnapshot);
            var selectedStatusItems = selectedSnapshot is null
                ? Array.Empty<ProjectPhaseGateRequirementStatusDto>()
                : statusByRequirement.Where(item => requirementSnapshots.Any(requirement =>
                    requirement.Id == item.RequirementSnapshotId && requirement.SnapshotId == selectedSnapshot!.Id)).ToArray();
            var optionReadiness = selectedSnapshot is null
                ? new Dictionary<Guid, string>()
                : new Dictionary<Guid, string> { [selectedSnapshot.Id] = GetReadinessStatus(selectedStatusItems) };
            var availability = BuildTransitionOptions(project.PhaseId, orgId, phaseById,
                transitionSnapshots, sourceConfigs, optionReadiness);

            var readinessStatus = selectedSnapshot is not null
                ? GetReadinessStatus(selectedStatusItems)
                : ProjectPhaseGateReadinessStatuses.NotApplicable;

            var counts = CountRequirements(selectedStatusItems);
            var applicableSnapshot = selectedSnapshot;
            result[project.Id] = new ProjectPhaseGateReadinessDto(
                project.Id,
                orgId,
                currentPhaseEntry?.Id,
                currentPhase,
                currentPhaseEntry?.NameEn ?? (string.IsNullOrWhiteSpace(currentPhase) ? null : currentPhase),
                currentPhaseEntry?.NameAr,
                selectedTargetEntry?.Id,
                selectedTarget,
                selectedTargetEntry?.NameEn,
                selectedTargetEntry?.NameAr,
                applicableSnapshot?.Id,
                applicableSnapshot?.ConfigurationId,
                applicableSnapshot?.ConfigurationVersionId,
                applicableSnapshot?.ConfigurationVersionNumber,
                readinessStatus,
                selectedTargetEntry is null
                    ? ProjectPhaseTransitionStatuses.NotApplicable
                    : selectedSnapshot is null
                        ? ProjectPhaseTransitionStatuses.NotApplicable
                        : selectedSnapshot is not null
                            && selectedSnapshot.TargetPhaseId == selectedTargetEntry.Id
                            && selectedSnapshot.SourcePhaseId != selectedTargetEntry.Id
                            && project.PhaseId == selectedTargetEntry.Id
                                ? ProjectPhaseTransitionStatuses.Completed
                                : selectedTargetEntry.Id == project.PhaseId
                                    ? ProjectPhaseTransitionStatuses.NotApplicable
                                    : ProjectPhaseTransitionStatuses.AwaitingTransition,
                counts.Mandatory,
                counts.SatisfiedMandatory,
                counts.MissingMandatory,
                counts.Optional,
                counts.SatisfiedOptional,
                counts.Mandatory == 0
                    ? (selectedSnapshot is not null && selectedStatusItems.Length > 0 ? 100m : (decimal?)null)
                    : decimal.Round(100m * counts.SatisfiedMandatory / counts.Mandatory, 2),
                Convert.ToBase64String(project.RowVersion ?? []),
                selectedStatusItems,
                availability);
        }
        return result;
    }

    private async Task<EvidenceComplianceResult> CompareDocumentToTemplateAsync(
        Project project,
        Guid organizationId,
        ProjectPhaseGateRequirementSnapshot requirement,
        Guid documentId,
        Guid documentVersionId,
        CancellationToken cancellationToken)
    {
        var comparedAt = DateTime.UtcNow;
        var projectDocument = await _context.Set<Document>().AsNoTracking()
            .FirstOrDefaultAsync(document => document.TenantId == project.TenantId
                && document.Id == documentId && !document.IsDeleted && !document.IsTemplate
                && !document.IsArchived && document.ProjectId == project.Id,
                cancellationToken);
        var projectVersion = projectDocument is null
            ? null
            : await _context.Set<DocumentVersion>().AsNoTracking()
                .FirstOrDefaultAsync(version => version.TenantId == project.TenantId
                    && version.DocumentId == projectDocument.Id && version.Id == documentVersionId && !version.IsDeleted,
                    cancellationToken);
        var template = await _context.Set<Document>().AsNoTracking()
            .FirstOrDefaultAsync(document => document.TenantId == project.TenantId
                && document.Id == requirement.TemplateId && !document.IsDeleted && document.IsTemplate
                && document.OrganizationId == organizationId,
                cancellationToken);
        var templateVersion = template is null
            ? null
            : await _context.Set<DocumentVersion>().AsNoTracking()
                .FirstOrDefaultAsync(version => version.TenantId == project.TenantId
                    && version.DocumentId == template.Id && version.Id == requirement.TemplateVersionId && !version.IsDeleted,
                    cancellationToken);

        if (projectDocument is null || projectVersion is null || template is null || templateVersion is null)
        {
            return NotVerifiedCompliance(
                null, null, comparedAt, "ComparisonInputUnavailable",
                "The uploaded document or pinned template version is unavailable for comparison.",
                "المستند المرفوع أو إصدار القالب المحدد غير متاح للمقارنة.");
        }

        var templateContent = await OpenValidatedContentAsync(template, templateVersion, cancellationToken);
        var projectContent = await OpenValidatedContentAsync(projectDocument, projectVersion, cancellationToken);
        if (templateContent is null || projectContent is null)
        {
            if (templateContent is not null) await templateContent.DisposeAsync();
            if (projectContent is not null) await projectContent.DisposeAsync();
            return NotVerifiedCompliance(
                null, null, comparedAt, "ComparisonInputUnavailable",
                "The stored document bytes could not be verified against storage metadata.",
                "تعذر التحقق من بايتات المستند المخزنة ومقارنتها ببيانات التخزين.");
        }

        await using (templateContent)
        await using (projectContent)
        {
            var expected = DocumentFileTypeValidator.Detect(templateContent);
            var detected = DocumentFileTypeValidator.Detect(projectContent);
            var expectedType = expected.IsValid ? expected.DetectedType.ToString() : null;
            var detectedType = detected.IsValid ? detected.DetectedType.ToString() : null;
            if (!expected.IsValid || !detected.IsValid)
            {
                return NotVerifiedCompliance(
                    expectedType, detectedType, comparedAt, "ContentTypeNotVerified",
                    "The file contents did not provide a reliable format for template comparison.",
                    "لم يقدم محتوى الملف نوعاً موثوقاً لإجراء مقارنة القالب.");
            }

            if (!DocumentFileTypeValidator.IsCompatible(expected.DetectedType, detected))
            {
                return new EvidenceComplianceResult(
                    ProjectDocumentComplianceStatuses.Mismatch,
                    expectedType,
                    detectedType,
                    comparedAt,
                    DocumentTemplateComparison.ValidatorVersion,
                    [new ProjectDocumentComplianceFindingDto(
                        "FileTypeMismatch",
                        $"The uploaded file type ({DocumentFileTypeValidator.GetEnglishLabel(detected.DetectedType)}) differs from the template type ({DocumentFileTypeValidator.GetEnglishLabel(expected.DetectedType)}).",
                        $"يختلف نوع الملف المرفوع ({DocumentFileTypeValidator.GetArabicLabel(detected.DetectedType)}) عن نوع القالب ({DocumentFileTypeValidator.GetArabicLabel(expected.DetectedType)}).",
                        DocumentFileTypeValidator.GetEnglishLabel(expected.DetectedType),
                        DocumentFileTypeValidator.GetEnglishLabel(detected.DetectedType))]);
            }

            var expectedReference = new DocumentTemplateReference(requirement.TemplateId, requirement.TemplateVersionId);
            var hasFullSourceReference = projectDocument.SourceTemplateId.HasValue
                && projectDocument.SourceTemplateVersionId.HasValue;
            // Compare owns the identity finding: passing the pinned expected reference (and a null
            // uploaded source reference when it is incomplete) makes the comparison emit exactly one
            // TemplateIdentityNotVerified, so this service must not add a second identity finding.
            var comparison = DocumentTemplateComparison.Compare(
                templateContent,
                projectContent,
                expected.DetectedType,
                expectedReference,
                hasFullSourceReference
                    ? new DocumentTemplateReference(projectDocument.SourceTemplateId!.Value, projectDocument.SourceTemplateVersionId!.Value)
                    : null);
            var findings = comparison.Differences.Select(difference => new ProjectDocumentComplianceFindingDto(
                difference.Code, difference.MessageEn, difference.MessageAr, difference.Expected, difference.Detected)).ToList();
            var status = comparison.Status;

            if (!hasFullSourceReference)
            {
                var sourceTemplateDiffers = projectDocument.SourceTemplateId.HasValue
                    && projectDocument.SourceTemplateId.Value != requirement.TemplateId;
                var sourceVersionDiffers = projectDocument.SourceTemplateId == requirement.TemplateId
                    && projectDocument.SourceTemplateVersionNumber.HasValue
                    && projectDocument.SourceTemplateVersionNumber.Value != templateVersion.VersionNumber;
                if (sourceTemplateDiffers || sourceVersionDiffers)
                {
                    // A confirmed partial mismatch replaces (never duplicates) the comparison's
                    // single not-verified identity finding.
                    findings.RemoveAll(finding => string.Equals(finding.Code, "TemplateIdentityNotVerified", StringComparison.Ordinal));
                    findings.Add(new ProjectDocumentComplianceFindingDto(
                        "TemplateIdentityMismatch",
                        "The uploaded document records a different source template or template version.",
                        "يسجل المستند المرفوع قالباً مصدرياً أو إصداراً مختلفاً.",
                        $"{requirement.TemplateId:D}/{requirement.TemplateVersionId:D}",
                        $"{projectDocument.SourceTemplateId?.ToString("D") ?? "unknown"}/{projectDocument.SourceTemplateVersionId?.ToString("D") ?? projectDocument.SourceTemplateVersionNumber?.ToString() ?? "unknown"}"));
                    status = ProjectDocumentComplianceStatuses.Mismatch;
                }
                else if (status == DocumentTemplateComparison.Compliant)
                {
                    // Identity cannot be established, so a structurally matching document is not Compliant.
                    status = ProjectDocumentComplianceStatuses.NotVerified;
                }
            }

            return new EvidenceComplianceResult(
                status,
                expectedType,
                detectedType,
                comparedAt,
                comparison.ValidatorVersion,
                findings);
        }
    }

    private async Task<Stream?> OpenValidatedContentAsync(
        Document document,
        DocumentVersion version,
        CancellationToken cancellationToken)
    {
        var storageKey = version.StorageKey ?? version.StoragePath ?? (version.IsCurrent ? document.StoragePath : null);
        if (string.IsNullOrWhiteSpace(storageKey)
            || (!string.IsNullOrWhiteSpace(version.StorageProviderId)
                && !string.Equals(version.StorageProviderId, _storage.ProviderId, StringComparison.OrdinalIgnoreCase)))
            return null;

        try
        {
            var metadata = await _storage.GetMetadataAsync(storageKey, cancellationToken);
            if (metadata is null
                || !string.Equals(metadata.ProviderId, _storage.ProviderId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(metadata.StorageKey, storageKey, StringComparison.Ordinal)
                || metadata.Length != version.FileSize
                || metadata.Length > DocumentFileTypeValidator.MaximumFileBytes
                || (!string.IsNullOrWhiteSpace(version.Sha256)
                    && !string.Equals(metadata.Sha256, version.Sha256, StringComparison.OrdinalIgnoreCase)))
                return null;

            var content = await _storage.OpenReadAsync(storageKey, cancellationToken);
            if (!content.CanRead || !content.CanSeek || content.Length != version.FileSize)
            {
                await content.DisposeAsync();
                return null;
            }
            return content;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    private static EvidenceComplianceResult NotVerifiedCompliance(
        string? expectedType,
        string? detectedType,
        DateTime comparedAt,
        string code,
        string messageEn,
        string messageAr) => new(
            ProjectDocumentComplianceStatuses.NotVerified,
            expectedType,
            detectedType,
            comparedAt,
            DocumentTemplateComparison.ValidatorVersion,
            [new ProjectDocumentComplianceFindingDto(code, messageEn, messageAr, expectedType, detectedType)]);

    private static IReadOnlyList<ProjectDocumentComplianceFindingDto> ParseComplianceFindings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<ProjectDocumentComplianceFindingDto>();
        try
        {
            return JsonSerializer.Deserialize<ProjectDocumentComplianceFindingDto[]>(json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? Array.Empty<ProjectDocumentComplianceFindingDto>();
        }
        catch (JsonException)
        {
            return [new ProjectDocumentComplianceFindingDto(
                "StoredFindingsNotReadable",
                "Stored comparison details could not be read; the document must be validated again.",
                "تعذرت قراءة تفاصيل المقارنة المخزنة؛ يجب إعادة التحقق من المستند.",
                null,
                null)];
        }
    }

    private async Task<IReadOnlyList<ProjectPhaseGateRequirementStatusDto>> BuildRequirementStatusesAsync(
        IReadOnlyList<ProjectScopeRow> projects,
        IReadOnlyDictionary<Guid, Guid?> organizationByProject,
        IReadOnlyDictionary<Guid, ProjectPhaseGateSnapshot> selectedSnapshots,
        IReadOnlyList<ProjectPhaseGateRequirementSnapshot> requirements,
        IReadOnlyList<ProjectPhaseGateEvidence> evidence,
        IReadOnlyList<Document> documents,
        IReadOnlyList<DocumentVersion> versions,
        IReadOnlyList<Document> templates,
        IReadOnlyList<DocumentVersion> templateVersions,
        CancellationToken cancellationToken)
    {
        var projectById = projects.ToDictionary(project => project.Id);
        var documentById = documents.ToDictionary(document => document.Id);
        var versionById = versions.ToDictionary(version => version.Id);
        var templateById = templates.ToDictionary(template => template.Id);
        var templateVersionById = templateVersions.ToDictionary(version => version.Id);
        var evidenceByRequirement = evidence
            .GroupBy(item => item.RequirementSnapshotId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CreatedAt).First());
        var projectIdBySnapshot = selectedSnapshots.ToDictionary(item => item.Value.Id, item => item.Key);
        var results = new List<ProjectPhaseGateRequirementStatusDto>(requirements.Count);
        var metadataByStorageKey = new Dictionary<string, StoredFileMetadata?>(StringComparer.Ordinal);

        foreach (var requirement in requirements)
        {
            if (!projectIdBySnapshot.TryGetValue(requirement.SnapshotId, out var projectId)
                || !projectById.TryGetValue(projectId, out var project))
                continue;

            evidenceByRequirement.TryGetValue(requirement.Id, out var link);
            templateById.TryGetValue(requirement.TemplateId, out var template);
            templateVersionById.TryGetValue(requirement.TemplateVersionId, out var templateVersion);
            if (link is null)
            {
                results.Add(new ProjectPhaseGateRequirementStatusDto(
                    requirement.Id, requirement.SourceRequirementId, requirement.TemplateId, requirement.TemplateVersionId,
                    requirement.TemplateName, requirement.Category, requirement.TemplateVersionLabel, requirement.RequirementType,
                    string.Equals(requirement.RequirementType, "Optional", StringComparison.OrdinalIgnoreCase)
                        ? ProjectPhaseGateRequirementStatuses.OptionalNotProvided
                        : ProjectPhaseGateRequirementStatuses.Missing,
                    null, null, null, null, null, null,
                    ProjectDocumentComplianceStatuses.Missing,
                    TemplateVersionNumber: templateVersion?.VersionNumber,
                    TemplateFileName: templateVersion?.FileName));
                continue;
            }

            documentById.TryGetValue(link.DocumentId, out var document);
            versionById.TryGetValue(link.DocumentVersionId, out var version);
            var statusMessage = ValidateDocumentEvidence(link, project, organizationByProject[projectId],
                document, version, template, templateVersion, out var storageKey);
            var available = false;
            if (statusMessage is null && !string.IsNullOrWhiteSpace(storageKey))
            {
                var linkedVersion = version!;
                try
                {
                    if (!metadataByStorageKey.TryGetValue(storageKey, out var metadata))
                    {
                        metadata = await _storage.GetMetadataAsync(storageKey, cancellationToken);
                        metadataByStorageKey[storageKey] = metadata;
                    }
                    available = metadata is not null
                        && string.Equals(metadata.ProviderId, _storage.ProviderId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(metadata.StorageKey, storageKey, StringComparison.Ordinal)
                        && metadata.Length == linkedVersion.FileSize
                        && (string.IsNullOrWhiteSpace(linkedVersion.StorageProviderId)
                            || string.Equals(metadata.ProviderId, linkedVersion.StorageProviderId, StringComparison.OrdinalIgnoreCase))
                        && (string.IsNullOrWhiteSpace(linkedVersion.Sha256)
                            || string.Equals(metadata.Sha256, linkedVersion.Sha256, StringComparison.OrdinalIgnoreCase));
                }
                catch (FileNotFoundException)
                {
                    available = false;
                }
                catch (IOException)
                {
                    available = false;
                }
                catch (InvalidOperationException)
                {
                    available = false;
                }
                if (!available) statusMessage = "The linked document file is unavailable.";
            }

            results.Add(new ProjectPhaseGateRequirementStatusDto(
                requirement.Id,
                requirement.SourceRequirementId,
                requirement.TemplateId,
                requirement.TemplateVersionId,
                requirement.TemplateName,
                requirement.Category,
                requirement.TemplateVersionLabel,
                requirement.RequirementType,
                statusMessage is null && available
                    ? ProjectPhaseGateRequirementStatuses.Satisfied
                    : ProjectPhaseGateRequirementStatuses.InvalidOrUnavailable,
                link.DocumentId,
                link.DocumentVersionId,
                document?.Name,
                version?.FileName,
                version?.VersionNumber,
                statusMessage,
                string.IsNullOrWhiteSpace(link.ComplianceStatus)
                    ? ProjectDocumentComplianceStatuses.PendingValidation
                    : link.ComplianceStatus,
                link.ExpectedFileType,
                link.DetectedFileType,
                link.ComparedAt,
                link.ValidatorVersion,
                ParseComplianceFindings(link.ComplianceFindingsJson),
                templateVersion?.VersionNumber,
                templateVersion?.FileName));
        }

        return results;
    }

    private async Task<CurrentPhaseCategoryEvaluation> EvaluateCurrentPhaseCategoryReadinessAsync(
        Project project,
        Guid? organizationId,
        ProjectPhaseCatalogEntry currentPhase,
        CancellationToken cancellationToken)
    {
        if (!organizationId.HasValue || !currentPhase.DocumentCategoryId.HasValue)
            return new CurrentPhaseCategoryEvaluation(false, Array.Empty<ProjectPhaseGateRequirementStatusDto>(), null);

        var category = await _context.Set<DocumentCategory>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == currentPhase.DocumentCategoryId.Value
                && item.TenantId == project.TenantId
                && (item.OrganizationId == organizationId.Value || item.OrganizationId == null)
                && item.IsActive && !item.IsDeleted, cancellationToken);
        if (category is null)
            return new CurrentPhaseCategoryEvaluation(false, Array.Empty<ProjectPhaseGateRequirementStatusDto>(), null);

        var templates = await _context.Set<Document>().AsNoTracking()
            .Where(item => item.TenantId == project.TenantId
                && item.OrganizationId == organizationId.Value
                && item.CategoryId == category.Id
                && item.IsTemplate && !item.IsArchived && !item.IsDeleted
                && item.Status == "Active")
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (templates.Count == 0)
            return new CurrentPhaseCategoryEvaluation(false, Array.Empty<ProjectPhaseGateRequirementStatusDto>(), null);

        var templateIds = templates.Select(item => item.Id).ToArray();
        var templateVersions = await _context.Set<DocumentVersion>().AsNoTracking()
            .Where(item => item.TenantId == project.TenantId && !item.IsDeleted
                && item.IsCurrent && templateIds.Contains(item.DocumentId))
            .OrderByDescending(item => item.VersionNumber)
            .ToListAsync(cancellationToken);
        var templateVersionByDocumentId = templateVersions
            .GroupBy(item => item.DocumentId)
            .ToDictionary(group => group.Key, group => group.First());

        var projectDocuments = await _context.Set<Document>().AsNoTracking()
            .Where(item => item.TenantId == project.TenantId && item.ProjectId == project.Id
                && item.OrganizationId == null && !item.IsTemplate && !item.IsArchived && !item.IsDeleted
                && item.Status == "Active" && item.SourceTemplateId.HasValue
                && templateIds.Contains(item.SourceTemplateId.Value))
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);
        var projectDocumentIds = projectDocuments.Select(item => item.Id).ToArray();
        var projectVersions = projectDocumentIds.Length == 0
            ? new List<DocumentVersion>()
            : await _context.Set<DocumentVersion>().AsNoTracking()
                .Where(item => item.TenantId == project.TenantId && !item.IsDeleted
                    && item.IsCurrent && projectDocumentIds.Contains(item.DocumentId))
                .OrderByDescending(item => item.VersionNumber)
                .ToListAsync(cancellationToken);
        var currentProjectVersionByDocumentId = projectVersions
            .GroupBy(item => item.DocumentId)
            .ToDictionary(group => group.Key, group => group.First());
        var metadataByStorageKey = new Dictionary<string, StoredFileMetadata?>(StringComparer.Ordinal);
        var requirements = new List<ProjectPhaseGateRequirementStatusDto>(templates.Count);

        foreach (var template in templates)
        {
            templateVersionByDocumentId.TryGetValue(template.Id, out var templateVersion);
            Document? matchedDocument = null;
            DocumentVersion? matchedVersion = null;
            Document? unavailableDocument = null;
            DocumentVersion? unavailableVersion = null;

            if (templateVersion is not null)
            {
                var candidates = projectDocuments.Where(document => document.SourceTemplateId == template.Id
                        && document.SourceTemplateVersionId == templateVersion.Id
                        && (!document.SourceTemplateVersionNumber.HasValue
                            || document.SourceTemplateVersionNumber.Value == templateVersion.VersionNumber))
                    .ToArray();
                foreach (var document in candidates)
                {
                    if (!currentProjectVersionByDocumentId.TryGetValue(document.Id, out var documentVersion))
                    {
                        unavailableDocument ??= document;
                        continue;
                    }

                    var storageKey = documentVersion.StorageKey ?? documentVersion.StoragePath
                        ?? (documentVersion.IsCurrent ? document.StoragePath : null);
                    var providerMatches = string.IsNullOrWhiteSpace(documentVersion.StorageProviderId)
                        || string.Equals(documentVersion.StorageProviderId, _storage.ProviderId, StringComparison.OrdinalIgnoreCase);
                    if (string.IsNullOrWhiteSpace(storageKey) || !providerMatches)
                    {
                        unavailableDocument ??= document;
                        unavailableVersion ??= documentVersion;
                        continue;
                    }

                    var available = false;
                    try
                    {
                        if (!metadataByStorageKey.TryGetValue(storageKey, out var metadata))
                        {
                            metadata = await _storage.GetMetadataAsync(storageKey, cancellationToken);
                            metadataByStorageKey[storageKey] = metadata;
                        }
                        available = metadata is not null
                            && string.Equals(metadata.ProviderId, _storage.ProviderId, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(metadata.StorageKey, storageKey, StringComparison.Ordinal)
                            && metadata.Length == documentVersion.FileSize
                            && (string.IsNullOrWhiteSpace(documentVersion.StorageProviderId)
                                || string.Equals(metadata.ProviderId, documentVersion.StorageProviderId, StringComparison.OrdinalIgnoreCase))
                            && (string.IsNullOrWhiteSpace(documentVersion.Sha256)
                                || string.Equals(metadata.Sha256, documentVersion.Sha256, StringComparison.OrdinalIgnoreCase));
                    }
                    catch (FileNotFoundException)
                    {
                        available = false;
                    }
                    catch (IOException)
                    {
                        available = false;
                    }
                    catch (InvalidOperationException)
                    {
                        available = false;
                    }

                    if (available)
                    {
                        matchedDocument = document;
                        matchedVersion = documentVersion;
                        break;
                    }

                    unavailableDocument ??= document;
                    unavailableVersion ??= documentVersion;
                }
            }

            var requirementType = template.IsMandatory ? "Mandatory" : "Optional";
            var status = matchedDocument is not null
                ? ProjectPhaseGateRequirementStatuses.Satisfied
                : unavailableDocument is not null
                    ? ProjectPhaseGateRequirementStatuses.InvalidOrUnavailable
                    : template.IsMandatory
                        ? ProjectPhaseGateRequirementStatuses.Missing
                        : ProjectPhaseGateRequirementStatuses.OptionalNotProvided;
            var displayedDocument = matchedDocument ?? unavailableDocument;
            var displayedVersion = matchedVersion ?? unavailableVersion;
            requirements.Add(new ProjectPhaseGateRequirementStatusDto(
                template.Id,
                template.Id,
                template.Id,
                templateVersion?.Id ?? Guid.Empty,
                template.Name,
                category.Name,
                templateVersion is null ? "Unavailable" : $"v{templateVersion.VersionNumber}",
                requirementType,
                status,
                displayedDocument?.Id,
                displayedVersion?.Id,
                displayedDocument?.Name,
                displayedVersion?.FileName,
                displayedVersion?.VersionNumber,
                null,
                displayedDocument is null ? ProjectDocumentComplianceStatuses.Missing : ProjectDocumentComplianceStatuses.PendingValidation,
                TemplateVersionNumber: templateVersion?.VersionNumber,
                TemplateFileName: templateVersion?.FileName,
                SupportsEvidenceLinking: false));
        }

        var counts = CountRequirements(requirements);
        var evaluation = new GateEvaluation(requirements, counts.Mandatory, counts.SatisfiedMandatory,
            counts.MissingMandatory, counts.Optional, counts.SatisfiedOptional);
        return new CurrentPhaseCategoryEvaluation(true, requirements, evaluation);
    }

    private string? ValidateDocumentEvidence(
        ProjectPhaseGateEvidence evidence,
        ProjectScopeRow project,
        Guid? organizationId,
        Document? document,
        DocumentVersion? version,
        Document? template,
        DocumentVersion? templateVersion,
        out string? storageKey)
    {
        storageKey = null;
        if (evidence.TenantId != project.TenantId || evidence.ProjectId != project.Id
            || !organizationId.HasValue || evidence.OrganizationId != organizationId.Value)
            return "The evidence is outside this project or Organization scope.";
        if (document is null || document.TenantId != project.TenantId || document.IsDeleted
            || document.ProjectId != project.Id || document.IsTemplate || document.IsArchived)
            return "The linked project document is unavailable.";
        if (document.OrganizationId.HasValue)
            return "The linked document Organization does not match this project.";
        if (template is null || template.TenantId != project.TenantId || template.IsDeleted || !template.IsTemplate
            || template.OrganizationId != organizationId || templateVersion is null || templateVersion.IsDeleted
            || templateVersion.TenantId != project.TenantId || templateVersion.DocumentId != template.Id)
            return "The pinned approved template version is unavailable.";
        if (document.SourceTemplateId != template.Id
            || document.SourceTemplateVersionId != templateVersion.Id
            || (document.SourceTemplateVersionNumber.HasValue
                && document.SourceTemplateVersionNumber.Value != templateVersion.VersionNumber))
            return "The project document does not reference the exact required template version.";
        if (version is null || version.TenantId != project.TenantId || version.IsDeleted
            || version.DocumentId != document.Id || version.Id != evidence.DocumentVersionId)
            return "The selected document version is unavailable.";
        if (string.IsNullOrWhiteSpace(version.FileName)
            || DocumentContentTypes.ForFileName(version.FileName) == DocumentContentTypes.Fallback)
            return "The linked project document has an unsupported file type.";

        storageKey = version.StorageKey ?? version.StoragePath ?? (version.IsCurrent ? document.StoragePath : null);
        if (string.IsNullOrWhiteSpace(storageKey)
            || (!string.IsNullOrWhiteSpace(version.StorageProviderId)
                && !string.Equals(version.StorageProviderId, _storage.ProviderId, StringComparison.OrdinalIgnoreCase)))
            return "The selected document version is unavailable in the configured storage provider.";
        return null;
    }

    private async Task<Result<ProjectPhaseGateSnapshot?>> GetOrCreateSnapshotAsync(
        Project project,
        Guid organizationId,
        ProjectPhaseCatalogEntry sourcePhaseEntry,
        ProjectPhaseCatalogEntry targetPhaseEntry,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var sourcePhase = NormalizePhase(sourcePhaseEntry.Code);
        var targetPhase = NormalizePhase(targetPhaseEntry.Code);
        var existing = await _context.Set<ProjectPhaseGateSnapshot>()
            .FirstOrDefaultAsync(snapshot => snapshot.TenantId == project.TenantId && !snapshot.IsDeleted
                && snapshot.ProjectId == project.Id && snapshot.OrganizationId == organizationId
                && snapshot.SourcePhaseId == sourcePhaseEntry.Id && snapshot.TargetPhaseId == targetPhaseEntry.Id, cancellationToken);
        if (existing is not null) return Result<ProjectPhaseGateSnapshot?>.Success(existing);

        var configured = await _configurationReader.GetCurrentAsync(
            project.TenantId, organizationId, sourcePhaseEntry.Id, targetPhaseEntry.Id, cancellationToken);
        if (configured is null
            || configured.OrganizationId != organizationId
            || configured.SourcePhaseId != sourcePhaseEntry.Id
            || configured.TargetPhaseId != targetPhaseEntry.Id
            || !configured.SourcePhase.Equals(sourcePhase, StringComparison.OrdinalIgnoreCase)
            || !configured.TargetPhase.Equals(targetPhase, StringComparison.OrdinalIgnoreCase))
            return Result<ProjectPhaseGateSnapshot?>.Success(null);
        if (configured.Requirements is null
            || configured.Requirements.Any(requirement => requirement.Id == Guid.Empty
                || requirement.TemplateId == Guid.Empty
                || requirement.TemplateVersionId == Guid.Empty
                || string.IsNullOrWhiteSpace(requirement.TemplateName)
                || string.IsNullOrWhiteSpace(requirement.TemplateVersionLabel)
                || !(requirement.RequirementType.Equals("Mandatory", StringComparison.OrdinalIgnoreCase)
                    || requirement.RequirementType.Equals("Optional", StringComparison.OrdinalIgnoreCase)))
            || configured.Requirements.GroupBy(requirement => requirement.TemplateId).Any(group => group.Count() > 1))
            return Result<ProjectPhaseGateSnapshot?>.Failure(Error.BusinessValidation(
                "INVALID_PHASE_GATE_CONFIGURATION", "The Organization Phase Gate configuration contains invalid or duplicate requirements."));

        if (!await HasAvailableTemplateReferencesAsync(project.TenantId, organizationId, configured, cancellationToken))
            return Result<ProjectPhaseGateSnapshot?>.Failure(Error.BusinessValidation(
                "TEMPLATE_DELETION_APPROVED", "A new project snapshot cannot reference a deletion-approved template."));

        var snapshot = MakeSnapshot(project.Id, project.TenantId, organizationId, sourcePhaseEntry, targetPhaseEntry, configured, actorId.ToString());
        var requirements = MakeRequirementSnapshots(snapshot, project.TenantId, configured, actorId.ToString());
        var ownsTransaction = _context.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await BeginTransactionIfNeededAsync(cancellationToken)
            : null;
        _context.Set<ProjectPhaseGateSnapshot>().Add(snapshot);
        _context.Set<ProjectPhaseGateRequirementSnapshot>().AddRange(requirements);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Result<ProjectPhaseGateSnapshot?>.Success(snapshot);
        }
        catch (DbUpdateException)
        {
            await RollbackCurrentTransactionAsync(cancellationToken);
            _context.Entry(snapshot).State = EntityState.Detached;
            foreach (var item in requirements) _context.Entry(item).State = EntityState.Detached;
            var racedSnapshot = await _context.Set<ProjectPhaseGateSnapshot>()
                .FirstOrDefaultAsync(item => item.TenantId == project.TenantId && !item.IsDeleted
                    && item.ProjectId == project.Id && item.OrganizationId == organizationId
                    && item.SourcePhaseId == sourcePhaseEntry.Id && item.TargetPhaseId == targetPhaseEntry.Id, cancellationToken);
            return racedSnapshot is null
                ? Result<ProjectPhaseGateSnapshot?>.Failure(Error.Conflict("The project phase-gate snapshot could not be saved."))
                : Result<ProjectPhaseGateSnapshot?>.Success(racedSnapshot);
        }
    }

    private async Task<GateEvaluation> EvaluateSnapshotAsync(
        Project project,
        Guid organizationId,
        ProjectPhaseGateSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var projectRow = new ProjectScopeRow(project.Id, project.TenantId, project.ProgramId, project.PortfolioId,
            project.ProjectPhaseId, project.Phase, project.Status, project.RowVersion);
        var orgMap = new Dictionary<Guid, Guid?> { [project.Id] = organizationId };
        var snapshots = new Dictionary<Guid, ProjectPhaseGateSnapshot> { [project.Id] = snapshot };
        var requirements = await _context.Set<ProjectPhaseGateRequirementSnapshot>().AsNoTracking()
            .Where(item => item.TenantId == project.TenantId && !item.IsDeleted && item.SnapshotId == snapshot.Id)
            .OrderBy(item => item.TemplateName)
            .ToListAsync(cancellationToken);
        var requirementIds = requirements.Select(item => item.Id).ToArray();
        var links = requirementIds.Length == 0
            ? new List<ProjectPhaseGateEvidence>()
            : await _context.Set<ProjectPhaseGateEvidence>().AsNoTracking()
                .Where(item => item.TenantId == project.TenantId && !item.IsDeleted
                    && item.ProjectId == project.Id && item.OrganizationId == organizationId
                    && requirementIds.Contains(item.RequirementSnapshotId))
                .ToListAsync(cancellationToken);
        var docIds = links.Select(item => item.DocumentId).Distinct().ToArray();
        var documents = docIds.Length == 0
            ? new List<Document>()
            : await _context.Set<Document>().AsNoTracking()
                .Where(item => item.TenantId == project.TenantId && !item.IsDeleted && docIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
        var versionIds = links.Select(item => item.DocumentVersionId).Distinct().ToArray();
        var versions = versionIds.Length == 0
            ? new List<DocumentVersion>()
            : await _context.Set<DocumentVersion>().AsNoTracking()
                .Where(item => item.TenantId == project.TenantId && !item.IsDeleted && versionIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
        var templateIds = requirements.Select(item => item.TemplateId).Distinct().ToArray();
        var templates = templateIds.Length == 0
            ? new List<Document>()
            : await _context.Set<Document>().AsNoTracking()
                .Where(item => item.TenantId == project.TenantId && !item.IsDeleted && item.IsTemplate
                    && templateIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
        var templateVersionIds = requirements.Select(item => item.TemplateVersionId).Distinct().ToArray();
        var templateVersions = templateVersionIds.Length == 0
            ? new List<DocumentVersion>()
            : await _context.Set<DocumentVersion>().AsNoTracking()
                .Where(item => item.TenantId == project.TenantId && !item.IsDeleted && templateVersionIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
        var items = await BuildRequirementStatusesAsync(
            [projectRow], orgMap, snapshots, requirements, links, documents, versions, templates, templateVersions, cancellationToken);
        var counts = CountRequirements(items);
        return new GateEvaluation(items, counts.Mandatory, counts.SatisfiedMandatory, counts.MissingMandatory,
            counts.Optional, counts.SatisfiedOptional);
    }

    private async Task<bool> HasAvailableTemplateReferencesAsync(
        Guid tenantId,
        Guid organizationId,
        ProjectPhaseGateConfigurationDto configuration,
        CancellationToken cancellationToken)
    {
        if (configuration.Requirements.Count == 0)
            return true;

        var configuredReferences = configuration.Requirements
            .Select(requirement => (requirement.TemplateId, requirement.TemplateVersionId))
            .ToHashSet();
        var templateIds = configuredReferences.Select(reference => reference.TemplateId).Distinct().ToArray();
        var versionIds = configuredReferences.Select(reference => reference.TemplateVersionId).Distinct().ToArray();
        var validReferences = await (
                from document in _context.Set<Document>().IgnoreQueryFilters().AsNoTracking()
                join version in _context.Set<DocumentVersion>().IgnoreQueryFilters().AsNoTracking()
                    on new { document.TenantId, DocumentId = document.Id }
                    equals new { version.TenantId, DocumentId = version.DocumentId }
                where document.TenantId == tenantId
                    && document.OrganizationId == organizationId
                    && templateIds.Contains(document.Id)
                    && document.IsTemplate
                    && !document.IsDeleted
                    && !document.IsDeletionApproved
                    && versionIds.Contains(version.Id)
                    && !version.IsDeleted
                select new { document.Id, VersionId = version.Id })
            .ToListAsync(cancellationToken);
        var validSet = validReferences.Select(item => (item.Id, item.VersionId)).ToHashSet();
        return configuredReferences.All(validSet.Contains);
    }

    private async Task<ProjectPhaseGateConfigurationDto?> GetConfigurationAsync(
        Guid tenantId,
        Guid organizationId,
        Guid sourcePhaseId,
        Guid targetPhaseId,
        IReadOnlyDictionary<string, IReadOnlyList<ProjectPhaseGateConfigurationDto>> configsByScope,
        CancellationToken cancellationToken)
    {
        if (configsByScope.TryGetValue(ScopeKey(organizationId, sourcePhaseId), out var configs))
            return configs.FirstOrDefault(config => config.TargetPhaseId == targetPhaseId);
        return await _configurationReader.GetCurrentAsync(tenantId, organizationId, sourcePhaseId, targetPhaseId, cancellationToken);
    }

    private static ProjectPhaseGateSnapshot MakeSnapshot(
        Guid projectId,
        Guid tenantId,
        Guid organizationId,
        ProjectPhaseCatalogEntry sourcePhase,
        ProjectPhaseCatalogEntry targetPhase,
        ProjectPhaseGateConfigurationDto configuration,
        string actor) => new()
    {
        TenantId = tenantId,
        ProjectId = projectId,
        OrganizationId = organizationId,
        SourcePhaseId = sourcePhase.Id,
        TargetPhaseId = targetPhase.Id,
        SourcePhase = NormalizePhase(configuration.SourcePhase),
        TargetPhase = NormalizePhase(configuration.TargetPhase),
        ConfigurationId = configuration.Id,
        ConfigurationVersionId = configuration.VersionId,
        ConfigurationVersionNumber = configuration.VersionNumber,
        CreatedBy = actor
    };

    private static List<ProjectPhaseGateRequirementSnapshot> MakeRequirementSnapshots(
        ProjectPhaseGateSnapshot snapshot,
        Guid tenantId,
        ProjectPhaseGateConfigurationDto configuration,
        string actor) => configuration.Requirements.Select(requirement => new ProjectPhaseGateRequirementSnapshot
    {
        TenantId = tenantId,
        SnapshotId = snapshot.Id,
        SourceRequirementId = requirement.Id,
        TemplateId = requirement.TemplateId,
        TemplateVersionId = requirement.TemplateVersionId,
        TemplateName = requirement.TemplateName,
        Category = requirement.Category,
        TemplateVersionLabel = requirement.TemplateVersionLabel,
        RequirementType = requirement.RequirementType,
        CreatedBy = actor
    }).ToList();

    private static IReadOnlyList<ProjectPhaseGateTransitionOptionDto> BuildTransitionOptions(
        Guid? sourcePhaseId,
        Guid? projectOrganizationId,
        IReadOnlyDictionary<Guid, ProjectPhaseCatalogEntry> phasesById,
        IReadOnlyCollection<ProjectPhaseGateSnapshot> savedSnapshots,
        IReadOnlyCollection<ProjectPhaseGateConfigurationDto> configurations,
        IReadOnlyDictionary<Guid, string> readinessBySnapshot)
    {
        if (!sourcePhaseId.HasValue || !phasesById.ContainsKey(sourcePhaseId.Value))
            return Array.Empty<ProjectPhaseGateTransitionOptionDto>();

        var options = new Dictionary<Guid, ProjectPhaseGateTransitionOptionDto>();
        var targetIds = configurations.Select(item => item.TargetPhaseId)
            .Concat(savedSnapshots.Where(snapshot => snapshot.SourcePhaseId == sourcePhaseId.Value)
                .Select(snapshot => snapshot.TargetPhaseId))
            .Distinct();
        foreach (var targetId in targetIds)
        {
            if (!phasesById.TryGetValue(targetId, out var target)
                || (target.OrganizationId.HasValue && target.OrganizationId != projectOrganizationId)
                || !target.IsActive)
                continue;
            var config = configurations.FirstOrDefault(item => item.TargetPhaseId == target.Id);
            var saved = savedSnapshots.FirstOrDefault(snapshot => snapshot.SourcePhaseId == sourcePhaseId.Value && snapshot.TargetPhaseId == target.Id);
            string? readiness = null;
            var wasEvaluated = saved is not null && readinessBySnapshot.TryGetValue(saved.Id, out readiness);
            options[target.Id] = new ProjectPhaseGateTransitionOptionDto(
                target.Id, target.Code, target.NameEn, target.NameAr,
                config?.Id ?? saved?.ConfigurationId,
                config?.VersionId ?? saved?.ConfigurationVersionId,
                config?.VersionNumber ?? saved?.ConfigurationVersionNumber,
                wasEvaluated ? readiness! : config is null
                    ? ProjectPhaseGateReadinessStatuses.NotConfigured
                    : ProjectPhaseGateReadinessStatuses.NotApplicable,
                wasEvaluated);
        }
        return options.Values.OrderBy(option => option.TargetPhase, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.TargetPhaseId).ToArray();
    }

    private static string GetReadinessStatus(IReadOnlyCollection<ProjectPhaseGateRequirementStatusDto> items)
    {
        if (items.Count == 0) return ProjectPhaseGateReadinessStatuses.NotApplicable;
        var mandatory = items.Where(item => item.RequirementType.Equals("Mandatory", StringComparison.OrdinalIgnoreCase)).ToArray();
        return mandatory.All(item => item.Status == ProjectPhaseGateRequirementStatuses.Satisfied)
            ? ProjectPhaseGateReadinessStatuses.Ready
            : ProjectPhaseGateReadinessStatuses.Incomplete;
    }

    private static RequirementCounts CountRequirements(IReadOnlyCollection<ProjectPhaseGateRequirementStatusDto> items)
    {
        var mandatory = items.Where(item => item.RequirementType.Equals("Mandatory", StringComparison.OrdinalIgnoreCase)).ToArray();
        var optional = items.Where(item => item.RequirementType.Equals("Optional", StringComparison.OrdinalIgnoreCase)).ToArray();
        var satisfiedMandatory = mandatory.Count(item => item.Status == ProjectPhaseGateRequirementStatuses.Satisfied);
        var satisfiedOptional = optional.Count(item => item.Status == ProjectPhaseGateRequirementStatuses.Satisfied);
        return new RequirementCounts(mandatory.Length, satisfiedMandatory, mandatory.Length - satisfiedMandatory,
            optional.Length, satisfiedOptional);
    }

    private static Error NotConfiguredError(Project project, string? source, string? target, Guid? organizationId = null) => new(
        "PHASE_GATE_NOT_CONFIGURED",
        "No Organization Phase Gate configuration exists for this project phase transition.",
        409,
        details: new Dictionary<string, object?>
        {
            ["projectId"] = project.Id,
            ["organizationId"] = organizationId,
            ["sourcePhase"] = source,
            ["targetPhase"] = target,
            ["readinessStatus"] = ProjectPhaseGateReadinessStatuses.NotConfigured
        });

    private AuditEntry CreateEvidenceAudit(
        Project project,
        ProjectPhaseGateEvidence evidence,
        string action,
        Guid actorId,
        Guid templateId,
        Guid templateVersionId) => new()
    {
        ProjectId = project.Id,
        TenantId = project.TenantId,
        EntityType = "ProjectPhaseGateEvidence",
        EntityId = evidence.Id,
        Action = action,
        UserId = actorId,
        UserName = CurrentUserName,
        Timestamp = DateTime.UtcNow,
        Description = action switch
        {
            "EvidenceLinked" => "Project document version linked to a pinned Phase Gate requirement; compliance is evaluated separately.",
            "EvidenceComplianceValidated" => $"Project document compliance was compared with the pinned template version; result: {evidence.ComplianceStatus}.",
            _ => "Project document evidence link removed from a Phase Gate requirement."
        },
        Changes = JsonSerializer.Serialize(new
        {
            requirementSnapshotId = evidence.RequirementSnapshotId,
            documentId = evidence.DocumentId,
            documentVersionId = evidence.DocumentVersionId,
            templateId,
            templateVersionId,
            complianceStatus = evidence.ComplianceStatus,
            expectedFileType = evidence.ExpectedFileType,
            detectedFileType = evidence.DetectedFileType,
            comparedAt = evidence.ComparedAt,
            validatorVersion = evidence.ValidatorVersion,
        }),
        NewValues = JsonSerializer.Serialize(new
        {
            evidence.ProjectId,
            evidence.OrganizationId,
            evidence.RequirementSnapshotId,
            evidence.DocumentId,
            evidence.DocumentVersionId,
            templateId,
            templateVersionId,
            evidence.ComplianceStatus,
            evidence.ExpectedFileType,
            evidence.DetectedFileType,
            evidence.ComparedAt,
            evidence.ValidatorVersion,
            isDeleted = evidence.IsDeleted
        }),
        CreatedBy = actorId.ToString()
    };

    private AuditEntry CreateTransitionAudit(
        Project project,
        ProjectPhaseCatalogEntry sourcePhase,
        ProjectPhaseCatalogEntry targetPhase,
        Guid? organizationId,
        ProjectPhaseGateSnapshot? snapshot,
        GateEvaluation? evaluation,
        string previousPhase,
        string newPhase,
        Guid actorId,
        DateTimeOffset transitionedAt)
    {
        var evidence = (evaluation?.Requirements ?? Array.Empty<ProjectPhaseGateRequirementStatusDto>())
            .Where(item => item.Status == ProjectPhaseGateRequirementStatuses.Satisfied)
            .Select(item => new { item.RequirementSnapshotId, item.TemplateId, item.TemplateVersionId, item.DocumentId, item.DocumentVersionId })
            .ToArray();
        // The snapshot below is the immutable source of the phase-history endpoint. Every
        // value is copied into the audit row now; later catalog, template, or configuration
        // edits cannot change what a past transition reports.
        var phaseLabels = new
        {
            fromEn = sourcePhase.NameEn,
            fromAr = sourcePhase.NameAr,
            toEn = targetPhase.NameEn,
            toAr = targetPhase.NameAr
        };
        var requirements = (evaluation?.Requirements ?? Array.Empty<ProjectPhaseGateRequirementStatusDto>())
            .Select(CreateRequirementSnapshot)
            .ToArray();
        var evaluationOutcome = evaluation is null ? "NotApplicable"
            : evaluation.MissingMandatoryCount > 0 ? "Incomplete" : "Ready";
        var mandatoryRequirementCount = evaluation?.MandatoryCount ?? 0;
        var satisfiedMandatoryCount = evaluation?.SatisfiedMandatoryCount ?? 0;
        var missingMandatoryCount = evaluation?.MissingMandatoryCount ?? 0;
        var optionalRequirementCount = evaluation?.OptionalCount ?? 0;
        var satisfiedOptionalCount = evaluation?.SatisfiedOptionalCount ?? 0;
        return new AuditEntry
        {
            ProjectId = project.Id,
            TenantId = project.TenantId,
            EntityType = "ProjectPhase",
            EntityId = project.Id,
            Action = "Change",
            UserId = actorId,
            UserName = CurrentUserName,
            Timestamp = transitionedAt.UtcDateTime,
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString(),
            Description = evaluation is null
                ? $"Project phase changed from {previousPhase} to {newPhase}; no current-phase document requirements were resolved."
                : $"Project phase changed from {previousPhase} to {newPhase}; current-phase document readiness was recorded for reference.",
            Changes = JsonSerializer.Serialize(new
            {
                phase = new { from = previousPhase, to = newPhase },
                phaseIds = new { from = sourcePhase.Id, to = targetPhase.Id },
                phaseLabels,
                organizationId,
                configurationId = snapshot?.ConfigurationId,
                configurationVersionId = snapshot?.ConfigurationVersionId,
                configurationVersionNumber = snapshot?.ConfigurationVersionNumber,
                evaluationOutcome,
                mandatoryRequirementCount,
                satisfiedMandatoryCount,
                missingMandatoryCount,
                optionalRequirementCount,
                satisfiedOptionalCount,
                requirements,
                evidence
            }),
            OriginalValues = JsonSerializer.Serialize(new { phase = previousPhase, phaseId = sourcePhase.Id }),
            NewValues = JsonSerializer.Serialize(new
            {
                phase = newPhase,
                phaseId = targetPhase.Id,
                phaseLabels,
                organizationId,
                configurationId = snapshot?.ConfigurationId,
                configurationVersionId = snapshot?.ConfigurationVersionId,
                configurationVersionNumber = snapshot?.ConfigurationVersionNumber,
                requirementEvaluation = evaluationOutcome,
                mandatoryRequirementCount,
                satisfiedMandatoryCount,
                missingMandatoryCount,
                optionalRequirementCount,
                satisfiedOptionalCount,
                requirements,
                evidence
            }),
            ChangedFields = "Phase",
            CreatedBy = actorId.ToString()
        };
    }

    /// <summary>
    /// Copies the evaluated requirement state into the audit snapshot. Kept as a helper so
    /// the persisted shape is identical in Changes and NewValues.
    /// </summary>
    private static object CreateRequirementSnapshot(ProjectPhaseGateRequirementStatusDto requirement) => new
    {
        requirementSnapshotId = requirement.RequirementSnapshotId,
        sourceRequirementId = requirement.SourceRequirementId,
        templateId = requirement.TemplateId,
        templateVersionId = requirement.TemplateVersionId,
        templateName = requirement.TemplateName,
        category = requirement.Category,
        templateVersionLabel = requirement.TemplateVersionLabel,
        requirementType = requirement.RequirementType,
        status = requirement.Status,
        complianceStatus = requirement.ComplianceStatus,
        expectedFileType = requirement.ExpectedFileType,
        detectedFileType = requirement.DetectedFileType,
        comparedAt = requirement.ComparedAt,
        validatorVersion = requirement.ValidatorVersion,
        complianceFindings = (requirement.ComplianceFindings ?? Array.Empty<ProjectDocumentComplianceFindingDto>())
            .Select(finding => new
            {
                code = finding.Code,
                messageEn = finding.MessageEn,
                messageAr = finding.MessageAr,
                expected = finding.Expected,
                detected = finding.Detected
            })
            .ToArray()
    };

    private async Task<Project?> GetProjectInScopeAsync(Guid projectId, bool tracking, CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == Guid.Empty) return null;
        var management = _managementContext?.Current;
        if (management is null || management.TenantId != tenantId || !management.Scope.ProjectIds.Contains(projectId))
            return null;
        var query = _context.Set<Project>().Where(project => project.Id == projectId
            && project.TenantId == tenantId && !project.IsDeleted);
        if (!tracking) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveOrganizationIdAsync(Project project, CancellationToken cancellationToken)
    {
        // Use the canonical Project -> Portfolio/Program/Department -> Organization resolver
        // so Phase Gate uses the same effective Organization contract as Documents and
        // Management Scope, including the department/business-unit fallback.
        return await _organizationLookup.ResolveOrganizationIdAsync(project.TenantId, project.Id, cancellationToken);
    }

    private async Task<Dictionary<Guid, Guid?>> ResolvePortfolioIdsAsync(
        IReadOnlyCollection<ProjectScopeRow> projects,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var result = projects.ToDictionary(project => project.Id, project => project.PortfolioId);
        var programIds = projects.Where(project => project.ProgramId.HasValue)
            .Select(project => project.ProgramId!.Value).Distinct().ToArray();
        if (programIds.Length == 0) return result;
        var programPortfolios = await _context.Set<ProgramEntity>().AsNoTracking()
            .Where(program => program.TenantId == tenantId && !program.IsDeleted && programIds.Contains(program.Id))
            .Select(program => new { program.Id, program.PortfolioId })
            .ToDictionaryAsync(program => program.Id, program => program.PortfolioId, cancellationToken);
        foreach (var project in projects.Where(project => project.ProgramId.HasValue))
        {
            if (programPortfolios.TryGetValue(project.ProgramId!.Value, out var programPortfolio))
            {
                if (!project.PortfolioId.HasValue || project.PortfolioId == programPortfolio)
                    result[project.Id] = programPortfolio;
                else
                    result[project.Id] = null;
            }
            else result[project.Id] = null;
        }
        return result;
    }

    private async Task<Dictionary<Guid, Guid>> ResolveOrganizationIdsAsync(
        IReadOnlyCollection<Guid> portfolioIds,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (portfolioIds.Count == 0) return new Dictionary<Guid, Guid>();
        return await _context.Set<PortfolioEntity>().AsNoTracking()
            .Where(portfolio => portfolio.TenantId == tenantId && !portfolio.IsDeleted
                && portfolio.OrganizationId.HasValue && portfolioIds.Contains(portfolio.Id))
            .ToDictionaryAsync(portfolio => portfolio.Id, portfolio => portfolio.OrganizationId!.Value, cancellationToken);
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfNeededAsync(
        CancellationToken cancellationToken,
        System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted)
    {
        if (!_context.Database.IsRelational() || _context.Database.CurrentTransaction is not null)
            return null;
        return await _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
    }

    private async Task RollbackCurrentTransactionAsync(CancellationToken cancellationToken)
    {
        var current = _context.Database.CurrentTransaction;
        if (current is not null)
            await current.RollbackAsync(cancellationToken);
    }

    private NEXUS.SharedKernel.Error? RequireProjectAccess(Guid projectId, string permission, bool requireDocumentView)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return Error.Unauthorized("An authenticated user is required.");
        if (!TryGetIdentity(principal, out var tenantId, out var userId))
            return Error.Forbidden("A valid tenant and user identity is required.");
        if (!HasPermission(principal, permission)
            || (requireDocumentView && !HasPermission(principal, DocumentViewPermission)))
            return Error.Forbidden("Project and Document view permissions are required.");
        var management = _managementContext?.Current;
        if (management is null || management.TenantId != tenantId || management.UserId != userId
            || !management.Scope.ProjectIds.Contains(projectId))
            return Error.Forbidden("The project is outside the current management scope.");
        return null;
    }

    private NEXUS.SharedKernel.Error? RequireProjectMutationAccess(
        Guid projectId,
        Guid actorId,
        string additionalPermission,
        bool requireDocumentView,
        bool requirePhaseChange)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return Error.Unauthorized("An authenticated user is required.");
        if (!TryGetIdentity(principal, out var tenantId, out var userId))
            return Error.Forbidden("A valid tenant and user identity is required.");
        if (actorId == Guid.Empty || actorId != userId)
            return Error.Forbidden("The audit actor must match the authenticated user.");
        if (!HasPermission(principal, ProjectViewPermission)
            || (requirePhaseChange && !HasPermission(principal, ProjectChangePermission))
            || !HasPermission(principal, additionalPermission)
            || (requireDocumentView && !HasPermission(principal, DocumentViewPermission)))
            return Error.Forbidden(requirePhaseChange
                ? "Project.View and ProjectPhase.Change permissions are required."
                : "Project.View and required Document permissions are required.");
        var management = _managementContext?.Current;
        if (management is null || management.TenantId != tenantId || management.UserId != userId
            || !management.Scope.ProjectIds.Contains(projectId))
            return Error.Forbidden("The project is outside the current management scope.");
        if (management.EffectiveReadOnly)
            return Error.Forbidden("Phase-gate changes are unavailable in read-only management mode.");
        return null;
    }

    private Guid GetCurrentTenantId()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true
            && Guid.TryParse(principal.FindFirst("tenantId")?.Value, out var tenantId)
                ? tenantId
                : Guid.Empty;
    }

    private Guid? GetCurrentActorId()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true
            && Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value, out var actorId)
                ? actorId
                : null;
    }

    private string? CurrentUserName => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value
        ?? _httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value;

    private static bool TryGetIdentity(ClaimsPrincipal principal, out Guid tenantId, out Guid userId)
    {
        tenantId = Guid.Empty;
        userId = Guid.Empty;
        return Guid.TryParse(principal.FindFirst("tenantId")?.Value, out tenantId)
            && tenantId != Guid.Empty
            && Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value, out userId)
            && userId != Guid.Empty;
    }

    private static CompletedTransition? ParseCompletedTransition(AuditEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.NewValues) || string.IsNullOrWhiteSpace(entry.OriginalValues))
            return null;
        try
        {
            using var newValues = JsonDocument.Parse(entry.NewValues);
            using var originalValues = JsonDocument.Parse(entry.OriginalValues);
            if (!newValues.RootElement.TryGetProperty("phase", out var target)
                || target.ValueKind != JsonValueKind.String
                || !originalValues.RootElement.TryGetProperty("phase", out var source)
                || source.ValueKind != JsonValueKind.String)
                return null;
            var configVersionId = newValues.RootElement.TryGetProperty("configurationVersionId", out var versionElement)
                && versionElement.ValueKind == JsonValueKind.String
                && Guid.TryParse(versionElement.GetString(), out var parsedVersion)
                    ? parsedVersion
                    : (Guid?)null;
            var sourcePhase = NormalizePhase(source.GetString());
            var targetPhase = NormalizePhase(target.GetString());
            var sourcePhaseId = TryReadGuid(originalValues.RootElement, "phaseId");
            var targetPhaseId = TryReadGuid(newValues.RootElement, "phaseId");
            return string.IsNullOrWhiteSpace(sourcePhase) || string.IsNullOrWhiteSpace(targetPhase)
                ? null
                : new CompletedTransition(sourcePhase, targetPhase, configVersionId, sourcePhaseId, targetPhaseId);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool HasPermission(ClaimsPrincipal principal, string permission) =>
        principal.HasClaim("permission", permission) && !principal.HasClaim("deny", permission);

    private static string NormalizePhase(string? phase) => (phase ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Projects one persisted transition audit row into the public history contract. Every
    /// JSON read is defensive: historical rows written before the snapshot was enriched (or
    /// rows whose JSON is unreadable) yield an empty requirement list and null labels instead
    /// of failing the entire history request.
    /// </summary>
    private static ProjectPhaseHistoryItemDto MapPhaseHistoryItem(Guid projectId, AuditEntry entry)
    {
        var changes = TryParseJsonObject(entry.Changes);
        var originalValues = TryParseJsonObject(entry.OriginalValues);
        var newValues = TryParseJsonObject(entry.NewValues);

        var phaseLabels = GetObjectProperty(changes, "phaseLabels") ?? GetObjectProperty(newValues, "phaseLabels");
        var changePhases = GetObjectProperty(changes, "phase");
        var previousPhase = ReadString(originalValues, "phase")
            ?? ReadString(GetObjectProperty(originalValues, "phase"), "from")
            ?? ReadString(changePhases, "from")
            ?? string.Empty;
        var newPhase = ReadString(newValues, "phase")
            ?? ReadString(GetObjectProperty(newValues, "phase"), "to")
            ?? ReadString(changePhases, "to")
            ?? string.Empty;
        var changePhaseIds = GetObjectProperty(changes, "phaseIds");
        var previousPhaseId = ReadGuid(originalValues, "phaseId")
            ?? ReadGuid(changePhaseIds, "from")
            ?? Guid.Empty;
        var newPhaseId = ReadGuid(newValues, "phaseId")
            ?? ReadGuid(changePhaseIds, "to")
            ?? Guid.Empty;
        var mandatoryRequirementCount = ReadInt(changes, "mandatoryRequirementCount")
            ?? ReadInt(newValues, "mandatoryRequirementCount") ?? 0;
        var satisfiedMandatoryCount = ReadInt(changes, "satisfiedMandatoryCount")
            ?? ReadInt(newValues, "satisfiedMandatoryCount") ?? 0;
        var missingMandatoryCount = ReadInt(changes, "missingMandatoryCount")
            ?? ReadInt(newValues, "missingMandatoryCount")
            ?? Math.Max(0, mandatoryRequirementCount - satisfiedMandatoryCount);
        var transitionedAt = new DateTimeOffset(DateTime.SpecifyKind(entry.Timestamp, DateTimeKind.Utc));

        return new ProjectPhaseHistoryItemDto(
            entry.Id,
            projectId,
            previousPhaseId,
            newPhaseId,
            previousPhase,
            newPhase,
            ReadString(phaseLabels, "fromEn"),
            ReadString(phaseLabels, "fromAr"),
            ReadString(phaseLabels, "toEn"),
            ReadString(phaseLabels, "toAr"),
            ReadGuid(changes, "configurationVersionId") ?? ReadGuid(newValues, "configurationVersionId"),
            ReadInt(changes, "configurationVersionNumber") ?? ReadInt(newValues, "configurationVersionNumber"),
            ReadString(changes, "evaluationOutcome")
                ?? ReadString(newValues, "requirementEvaluation")
                ?? ProjectPhaseGateReadinessStatuses.NotApplicable,
            mandatoryRequirementCount,
            satisfiedMandatoryCount,
            missingMandatoryCount,
            ReadInt(changes, "optionalRequirementCount") ?? ReadInt(newValues, "optionalRequirementCount") ?? 0,
            ReadInt(changes, "satisfiedOptionalCount") ?? ReadInt(newValues, "satisfiedOptionalCount") ?? 0,
            entry.UserId,
            entry.UserName,
            transitionedAt,
            ReadHistoryRequirements(changes, newValues));
    }

    private static IReadOnlyList<ProjectPhaseHistoryRequirementDto> ReadHistoryRequirements(
        JsonElement? changes,
        JsonElement? newValues)
    {
        var array = GetArrayProperty(changes, "requirements") ?? GetArrayProperty(newValues, "requirements");
        if (array is not { } items) return Array.Empty<ProjectPhaseHistoryRequirementDto>();
        var requirements = new List<ProjectPhaseHistoryRequirementDto>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            requirements.Add(new ProjectPhaseHistoryRequirementDto(
                ReadGuid(item, "requirementSnapshotId") ?? Guid.Empty,
                ReadGuid(item, "sourceRequirementId") ?? Guid.Empty,
                ReadGuid(item, "templateId") ?? Guid.Empty,
                ReadGuid(item, "templateVersionId") ?? Guid.Empty,
                ReadString(item, "templateName") ?? string.Empty,
                ReadString(item, "category"),
                ReadString(item, "templateVersionLabel") ?? string.Empty,
                ReadString(item, "requirementType") ?? string.Empty,
                ReadString(item, "status") ?? string.Empty,
                ReadString(item, "complianceStatus") ?? ProjectDocumentComplianceStatuses.NotVerified,
                ReadString(item, "expectedFileType"),
                ReadString(item, "detectedFileType"),
                ReadDateTime(item, "comparedAt"),
                ReadString(item, "validatorVersion"),
                ReadHistoryFindings(item)));
        }
        return requirements;
    }

    private static IReadOnlyList<ProjectDocumentComplianceFindingDto> ReadHistoryFindings(JsonElement? requirement)
    {
        var array = GetArrayProperty(requirement, "complianceFindings");
        if (array is not { } items) return Array.Empty<ProjectDocumentComplianceFindingDto>();
        var findings = new List<ProjectDocumentComplianceFindingDto>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            findings.Add(new ProjectDocumentComplianceFindingDto(
                ReadString(item, "code") ?? string.Empty,
                ReadString(item, "messageEn") ?? string.Empty,
                ReadString(item, "messageAr") ?? string.Empty,
                ReadString(item, "expected"),
                ReadString(item, "detected")));
        }
        return findings;
    }

    private static JsonElement? TryParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.Clone()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement? GetObjectProperty(JsonElement? element, string propertyName)
    {
        if (element is not { } value || value.ValueKind != JsonValueKind.Object) return null;
        return value.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Object
                ? property
                : null;
    }

    private static JsonElement? GetArrayProperty(JsonElement? element, string propertyName)
    {
        if (element is not { } value || value.ValueKind != JsonValueKind.Object) return null;
        return value.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Array
                ? property
                : null;
    }

    private static string? ReadString(JsonElement? element, string propertyName)
    {
        if (element is not { } value || value.ValueKind != JsonValueKind.Object) return null;
        return value.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static Guid? ReadGuid(JsonElement? element, string propertyName) =>
        ReadString(element, propertyName) is { } text && Guid.TryParse(text, out var id) ? id : null;

    private static int? ReadInt(JsonElement? element, string propertyName)
    {
        if (element is not { } value || value.ValueKind != JsonValueKind.Object) return null;
        return value.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out var number)
                ? number
                : null;
    }

    private static DateTime? ReadDateTime(JsonElement? element, string propertyName)
    {
        var text = ReadString(element, propertyName);
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    private static Guid? TryReadGuid(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
        && Guid.TryParse(value.GetString(), out var id)
            ? id
            : null;

    private static string ScopeKey(Guid organizationId, Guid sourcePhaseId) =>
        $"{organizationId:N}|{sourcePhaseId:N}";

    private static bool TryDecodeVersionToken(string? token, out byte[] version)
    {
        version = [];
        if (string.IsNullOrWhiteSpace(token)) return false;
        try
        {
            version = Convert.FromBase64String(token);
            return version.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private sealed record ProjectScopeRow(
        Guid Id,
        Guid TenantId,
        Guid? ProgramId,
        Guid? PortfolioId,
        Guid? PhaseId,
        string? Phase,
        string Status,
        byte[] RowVersion);

    private sealed record RequirementCounts(
        int Mandatory,
        int SatisfiedMandatory,
        int MissingMandatory,
        int Optional,
        int SatisfiedOptional);

    private sealed record GateEvaluation(
        IReadOnlyList<ProjectPhaseGateRequirementStatusDto> Requirements,
        int MandatoryCount,
        int SatisfiedMandatoryCount,
        int MissingMandatoryCount,
        int OptionalCount,
        int SatisfiedOptionalCount);

    private sealed record CurrentPhaseCategoryEvaluation(
        bool IsConfigured,
        IReadOnlyList<ProjectPhaseGateRequirementStatusDto> Requirements,
        GateEvaluation? Evaluation);

    private sealed record EvidenceComplianceResult(
        string Status,
        string? ExpectedFileType,
        string? DetectedFileType,
        DateTime ComparedAt,
        string ValidatorVersion,
        IReadOnlyList<ProjectDocumentComplianceFindingDto> Findings);

    private sealed record CompletedTransition(
        string SourcePhase,
        string TargetPhase,
        Guid? ConfigurationVersionId,
        Guid? SourcePhaseId,
        Guid? TargetPhaseId);
}
