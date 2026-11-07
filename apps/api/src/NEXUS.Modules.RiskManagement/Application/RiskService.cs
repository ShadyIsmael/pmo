using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.RiskManagement.Application.DTOs;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.RiskManagement.Application;

public sealed class RiskService : IRiskService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IProjectReferenceLookup _projectLookup;
    private readonly IResourceReferenceLookup _resourceLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IProjectWorkflowConfigurationService? _projectWorkflowConfiguration;
    private readonly IWorkflowInstanceService? _workflowInstanceService;
    private readonly IManagementContextAccessor? _managementContext;

    public RiskService(DbContext context, IAuditService auditService, IProjectReferenceLookup projectLookup, IResourceReferenceLookup resourceLookup, IHttpContextAccessor httpContextAccessor, IProjectWorkflowConfigurationService? projectWorkflowConfiguration = null, IWorkflowInstanceService? workflowInstanceService = null, IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _auditService = auditService;
        _projectLookup = projectLookup;
        _resourceLookup = resourceLookup;
        _httpContextAccessor = httpContextAccessor;
        _projectWorkflowConfiguration = projectWorkflowConfiguration;
        _workflowInstanceService = workflowInstanceService;
        _managementContext = managementContext;
    }

    public async Task<Result<RiskDto>> CreateAsync(CreateRiskDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var title = dto.Title.Trim();
        var description = dto.Description.Trim();
        if (dto.ProjectId == Guid.Empty) return Failure<RiskDto>("Project is required");
        if (string.IsNullOrWhiteSpace(title)) return Failure<RiskDto>("Title is required");
        if (string.IsNullOrWhiteSpace(description)) return Failure<RiskDto>("Description is required");
        if (!IsDefined(dto.RiskLevel)) return Failure<RiskDto>("Risk level is invalid");
        var impactAssessment = dto.ImpactAssessment;
        if (impactAssessment is not null && !ValidateImpact(impactAssessment.ImpactTypes, impactAssessment.Description)) return Failure<RiskDto>("Impact types must be valid, unique, and include a shared impact description");
        var project = await _projectLookup.FindAsync(dto.ProjectId, cancellationToken);
        if (project is null) return Result<RiskDto>.Failure(Error.NotFound("Project not found"));
        if (!await IsValidOwnerAsync(dto.OwnerResourceId, dto.OwnerUserId, cancellationToken)) return Failure<RiskDto>("Specify either a valid Resource owner or an active same-tenant System User owner, not both.");
        var workflowSelection = _projectWorkflowConfiguration is null
            ? Result<ProjectWorkflowSelection?>.Success(null)
            : await _projectWorkflowConfiguration.ResolveForNewRecordAsync(dto.ProjectId, WorkflowDomains.Risk, RiskWorkflow.EntityType, BuildWorkflowContext(userId), cancellationToken);
        if (workflowSelection.IsFailure) return Result<RiskDto>.Failure(workflowSelection.Error!);
        var initialStatus = RiskStatus.Identified;
        if (workflowSelection.Value is { IsCustom: true } custom && !Enum.TryParse(custom.InitialStateCode, true, out initialStatus))
            return Failure<RiskDto>("The selected Risk workflow has an unsupported initial state.");

        var risk = new Risk
        {
            TenantId = GetCurrentTenantId(), ProjectId = dto.ProjectId, Title = title, Description = description,
            RiskLevel = dto.RiskLevel, Impact = dto.Impact?.Trim() ?? string.Empty, Likelihood = dto.Likelihood?.Trim() ?? string.Empty, Status = initialStatus, OwnerResourceId = dto.OwnerResourceId, OwnerUserId = dto.OwnerUserId,
            IdentifiedDate = DateTime.UtcNow, CreatedBy = userId
        };
        if (impactAssessment is not null)
        {
            var assessment = new RiskImpactAssessment { TenantId = risk.TenantId, Description = impactAssessment.Description.Trim(), CreatedBy = userId };
            foreach (var type in impactAssessment.ImpactTypes) assessment.ImpactTypes.Add(new RiskImpactAssessmentType { TenantId = risk.TenantId, ImpactType = type, CreatedBy = userId });
            risk.ImpactAssessments.Add(assessment);
        }
        _context.Set<Risk>().Add(risk);
        await _context.SaveChangesAsync(cancellationToken);
        if (workflowSelection.Value is { IsCustom: true } selected)
        {
            if (_workflowInstanceService is null) return Failure<RiskDto>("Workflow execution is not configured.");
            var instance = await _workflowInstanceService.ProvisionAsync(RiskWorkflow.EntityType, risk.Id, selected.WorkflowDefinitionId!.Value, selected.WorkflowVersionId!.Value, BuildWorkflowContext(userId), cancellationToken);
            if (instance.IsFailure) return Result<RiskDto>.Failure(instance.Error!);
        }
        await AuditAsync("Risk", risk.Id, "Create", userId, dto, cancellationToken);
        return Result<RiskDto>.Success(await ToRiskDtoAsync(risk, project, cancellationToken));
    }

    public async Task<Result<RiskDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var risk = await GetActiveRiskAsync(id, cancellationToken);
        if (risk is null) return Result<RiskDto>.Failure(Error.NotFound("Risk not found"));
        var project = await _projectLookup.FindAsync(risk.ProjectId, cancellationToken);
        return project is null ? Result<RiskDto>.Failure(Error.NotFound("Project not found")) : Result<RiskDto>.Success(await ToRiskDtoAsync(risk, project, cancellationToken));
    }

    public async Task<Result<RiskDto>> UpdateAsync(Guid id, UpdateRiskDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var risk = await ScopedRisks().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (risk is null) return Result<RiskDto>.Failure(Error.NotFound("Risk not found"));
        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Description)) return Failure<RiskDto>("Title and description are required");
        if (!IsDefined(dto.RiskLevel)) return Failure<RiskDto>("Risk level is invalid");
        if (!await IsValidOwnerAsync(dto.OwnerResourceId, dto.OwnerUserId, cancellationToken)) return Failure<RiskDto>("Specify either a valid Resource owner or an active same-tenant System User owner, not both.");
        if (dto.RowVersion.Length > 0) _context.Entry(risk).Property(r => r.RowVersion).OriginalValue = dto.RowVersion;
        risk.Title = dto.Title.Trim(); risk.Description = dto.Description.Trim(); risk.RiskLevel = dto.RiskLevel; risk.Impact = dto.Impact?.Trim() ?? string.Empty; risk.Likelihood = dto.Likelihood?.Trim() ?? string.Empty; risk.OwnerResourceId = dto.OwnerResourceId; risk.OwnerUserId = dto.OwnerUserId; risk.UpdatedBy = userId;
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<RiskDto>.Failure(Error.Conflict("Risk was changed by another user")); }
        var project = await _projectLookup.FindAsync(risk.ProjectId, cancellationToken);
        await AuditAsync("Risk", risk.Id, "Update", userId, dto, cancellationToken);
        return project is null ? Result<RiskDto>.Failure(Error.NotFound("Project not found")) : Result<RiskDto>.Success(await ToRiskDtoAsync(risk, project, cancellationToken));
    }

    public async Task<Result<RiskDto>> UpdateStatusAsync(Guid id, UpdateRiskStatusDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (!IsDefined(dto.Status)) return Failure<RiskDto>("Risk status is invalid");
        var risk = await ScopedRisks().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (risk is null) return Result<RiskDto>.Failure(Error.NotFound("Risk not found"));
        var workflowSelection = _projectWorkflowConfiguration is null
            ? Result<ProjectWorkflowSelection?>.Success(null)
            : await _projectWorkflowConfiguration.ResolveForNewRecordAsync(risk.ProjectId, WorkflowDomains.Risk, RiskWorkflow.EntityType, BuildWorkflowContext(userId), cancellationToken);
        if (workflowSelection.IsFailure) return Result<RiskDto>.Failure(workflowSelection.Error!);
        if (workflowSelection.Value is { IsCustom: true }) return Failure<RiskDto>("A custom Risk workflow is active for this project. Change the Risk through a workflow transition.");
        if (dto.RowVersion.Length > 0) _context.Entry(risk).Property(r => r.RowVersion).OriginalValue = dto.RowVersion;
        risk.Status = dto.Status; risk.ClosedDate = dto.Status == RiskStatus.Closed ? DateTime.UtcNow : risk.ClosedDate; risk.UpdatedBy = userId;
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<RiskDto>.Failure(Error.Conflict("Risk was changed by another user")); }
        var project = await _projectLookup.FindAsync(risk.ProjectId, cancellationToken);
        await AuditAsync("Risk", risk.Id, "StatusChanged", userId, dto, cancellationToken);
        return project is null ? Result<RiskDto>.Failure(Error.NotFound("Project not found")) : Result<RiskDto>.Success(await ToRiskDtoAsync(risk, project, cancellationToken));
    }

    public async Task<PagedResult<RiskDto>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, RiskStatus? status = null, Guid? ownerResourceId = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = ScopedRisks();
        if (projectId.HasValue) query = query.Where(r => r.ProjectId == projectId.Value);
        if (status.HasValue) query = query.Where(r => r.Status == status.Value);
        if (ownerResourceId.HasValue) query = query.Where(r => r.OwnerResourceId == ownerResourceId.Value);
        var total = await query.CountAsync(cancellationToken);
        var risks = await query.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var projectNames = await _projectLookup.GetNamesAsync(risks.Select(r => r.ProjectId), cancellationToken);
        var ownerNames = await _resourceLookup.GetNamesAsync(risks.Where(r => r.OwnerResourceId.HasValue).Select(r => r.OwnerResourceId!.Value), cancellationToken);
        var riskIds = risks.Select(r => r.Id).ToArray();
        var impactTypes = await _context.Set<RiskImpactAssessmentType>()
            .Where(item => riskIds.Contains(item.RiskImpactAssessment.RiskId))
            .Select(item => new { RiskId = item.RiskImpactAssessment.RiskId, item.ImpactType })
            .ToListAsync(cancellationToken);
        var latestActionUpdates = await _context.Set<RiskActionUpdate>()
            .Where(item => riskIds.Contains(item.RiskAction.RiskId))
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new LatestActionUpdate(item.RiskAction.RiskId, item.UpdateText, item.CreatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken);
        var impactTypesByRisk = impactTypes.GroupBy(item => item.RiskId).ToDictionary(group => group.Key, group => (IReadOnlyList<RiskImpactType>)group.Select(item => item.ImpactType).Distinct().OrderBy(item => item).ToList());
        var latestUpdateByRisk = latestActionUpdates.GroupBy(item => item.RiskId).ToDictionary(group => group.Key, group => group.First());
        var projectPublicIds = (await _projectLookup.GetFinancialReferencesAsync(cancellationToken))
            .ToDictionary(project => project.Id, project => project.PublicId);
        return new PagedResult<RiskDto>(risks.Select(r => ToRiskDto(r, projectNames.GetValueOrDefault(r.ProjectId, string.Empty), projectPublicIds.GetValueOrDefault(r.ProjectId), ownerNames, impactTypesByRisk.GetValueOrDefault(r.Id), latestUpdateByRisk.GetValueOrDefault(r.Id))).ToList(), page, pageSize, total);
    }

    public async Task<ProjectRiskOverviewDto> GetProjectOverviewAsync(Guid projectId, int previewLimit = 3, CancellationToken cancellationToken = default)
    {
        previewLimit = Math.Clamp(previewLimit, 1, 10);
        var projectRiskQuery = ScopedRisks().AsNoTracking().Where(risk => risk.ProjectId == projectId);
        // Mitigated is not formally closed in the canonical Risk lifecycle; keep it in the overview until Closed.
        var nonClosedQuery = projectRiskQuery.Where(risk => risk.Status != RiskStatus.Closed);

        var groupedStatusCounts = await nonClosedQuery
            .GroupBy(risk => risk.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var statusCounts = groupedStatusCounts
            .Select(group => new ProjectRiskStatusCountDto { Status = group.Status.ToString(), Count = group.Count })
            .OrderBy(item => item.Status)
            .ToList();
        var groupedLevelCounts = await nonClosedQuery
            .GroupBy(risk => risk.RiskLevel)
            .Select(group => new { RiskLevel = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var levelCounts = groupedLevelCounts
            .Select(group => new ProjectRiskLevelCountDto { RiskLevel = group.RiskLevel.ToString(), Count = group.Count, DisplayOrder = (int)group.RiskLevel })
            .OrderBy(item => item.DisplayOrder)
            .ToList();

        var openCount = await nonClosedQuery.CountAsync(cancellationToken);
        var highCriticalOpenCount = await nonClosedQuery
            .CountAsync(risk => risk.RiskLevel == RiskLevel.High || risk.RiskLevel == RiskLevel.Critical, cancellationToken);
        var topRisks = await nonClosedQuery
            .OrderByDescending(risk => risk.RiskLevel)
            .ThenByDescending(risk => risk.UpdatedAt ?? risk.CreatedAt)
            .Take(previewLimit)
            .Select(risk => new ProjectRiskOverviewItemDto
            {
                Id = risk.Id,
                Title = risk.Title,
                RiskLevel = risk.RiskLevel,
                Status = risk.Status
            })
            .ToListAsync(cancellationToken);

        return new ProjectRiskOverviewDto
        {
            OpenCount = openCount,
            HighCriticalOpenCount = highCriticalOpenCount,
            StatusCounts = statusCounts,
            LevelCounts = levelCounts,
            TopRisks = topRisks
        };
    }

    public async Task<Result<IReadOnlyList<RiskImpactAssessmentDto>>> GetImpactsAsync(Guid riskId, CancellationToken cancellationToken = default)
    {
        var risk = await GetActiveRiskAsync(riskId, cancellationToken);
        if (risk is null) return Result<IReadOnlyList<RiskImpactAssessmentDto>>.Failure(Error.NotFound("Risk not found"));
        var assessment = await _context.Set<RiskImpactAssessment>().Include(item => item.ImpactTypes)
            .SingleOrDefaultAsync(item => item.RiskId == risk.Id && item.TenantId == risk.TenantId, cancellationToken);
        return Result<IReadOnlyList<RiskImpactAssessmentDto>>.Success(assessment is null ? Array.Empty<RiskImpactAssessmentDto>() : new[] { ToImpactDto(assessment) });
    }

    public async Task<Result<RiskImpactAssessmentDto>> CreateImpactAsync(Guid riskId, CreateRiskImpactAssessmentDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var risk = await ScopedRisks().FirstOrDefaultAsync(item => item.Id == riskId, cancellationToken);
        if (risk is null) return Result<RiskImpactAssessmentDto>.Failure(Error.NotFound("Risk not found"));
        if (await _context.Set<RiskImpactAssessment>().AnyAsync(item => item.RiskId == riskId, cancellationToken)) return Result<RiskImpactAssessmentDto>.Failure(Error.Conflict("This risk already has an impact assessment"));
        if (!ValidateImpact(dto.ImpactTypes, dto.Description)) return Failure<RiskImpactAssessmentDto>("Impact types and a shared impact description are required");
        var assessment = new RiskImpactAssessment { TenantId = risk.TenantId, RiskId = riskId, Description = dto.Description.Trim(), CreatedBy = userId };
        foreach (var type in dto.ImpactTypes.Distinct()) assessment.ImpactTypes.Add(new RiskImpactAssessmentType { TenantId = risk.TenantId, ImpactType = type, CreatedBy = userId });
        _context.Set<RiskImpactAssessment>().Add(assessment);
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("RiskImpactAssessment", assessment.Id, "Create", userId, dto, cancellationToken);
        return Result<RiskImpactAssessmentDto>.Success(ToImpactDto(assessment));
    }

    public async Task<Result<RiskImpactAssessmentDto>> UpdateImpactAsync(Guid riskId, Guid impactId, UpdateRiskImpactAssessmentDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (!await IsRiskInScopeAsync(riskId, cancellationToken)) return Result<RiskImpactAssessmentDto>.Failure(Error.NotFound("Impact assessment not found"));
        var assessment = await _context.Set<RiskImpactAssessment>().Include(item => item.ImpactTypes).FirstOrDefaultAsync(item => item.Id == impactId && item.RiskId == riskId, cancellationToken);
        if (assessment is null) return Result<RiskImpactAssessmentDto>.Failure(Error.NotFound("Impact assessment not found"));
        if (!ValidateImpact(dto.ImpactTypes, dto.Description)) return Failure<RiskImpactAssessmentDto>("Impact types and a shared impact description are required");
        if (dto.RowVersion.Length > 0) _context.Entry(assessment).Property(item => item.RowVersion).OriginalValue = dto.RowVersion;
        assessment.Description = dto.Description.Trim(); assessment.UpdatedBy = userId;
        // Replace the normalized child set as a single operation. ExecuteDelete
        // deliberately bypasses child rowversion checks; the parent assessment's
        // rowversion remains the concurrency boundary for this edit.
        var existingTypes = assessment.ImpactTypes.ToList();
        await _context.Set<RiskImpactAssessmentType>().Where(type => type.RiskImpactAssessmentId == assessment.Id).ExecuteDeleteAsync(cancellationToken);
        assessment.ImpactTypes.Clear();
        foreach (var existingType in existingTypes) _context.Entry(existingType).State = EntityState.Detached;
        var replacementTypes = dto.ImpactTypes.Distinct().Select(type => new RiskImpactAssessmentType { TenantId = assessment.TenantId, RiskImpactAssessmentId = assessment.Id, ImpactType = type, CreatedBy = userId }).ToList();
        _context.Set<RiskImpactAssessmentType>().AddRange(replacementTypes);
        try { await _context.SaveChangesAsync(cancellationToken); } catch (DbUpdateConcurrencyException) { return Result<RiskImpactAssessmentDto>.Failure(Error.Conflict("Impact assessment was changed by another user")); }
        await AuditAsync("RiskImpactAssessment", assessment.Id, "Update", userId, dto, cancellationToken);
        var refreshed = await _context.Set<RiskImpactAssessment>().Include(item => item.ImpactTypes).SingleAsync(item => item.Id == assessment.Id, cancellationToken);
        return Result<RiskImpactAssessmentDto>.Success(ToImpactDto(refreshed));
    }

    public async Task<Result> DeleteImpactAsync(Guid riskId, Guid impactId, string userId, CancellationToken cancellationToken = default)
    {
        if (!await IsRiskInScopeAsync(riskId, cancellationToken)) return Result.Failure(Error.NotFound("Impact assessment not found"));
        var assessment = await _context.Set<RiskImpactAssessment>().FirstOrDefaultAsync(item => item.Id == impactId && item.RiskId == riskId, cancellationToken);
        if (assessment is null) return Result.Failure(Error.NotFound("Impact assessment not found"));
        assessment.IsDeleted = true; assessment.UpdatedBy = userId; await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("RiskImpactAssessment", assessment.Id, "Delete", userId, null, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<RiskActionDto>>> GetActionsAsync(Guid riskId, CancellationToken cancellationToken = default)
    {
        var risk = await GetActiveRiskAsync(riskId, cancellationToken);
        if (risk is null) return Result<IReadOnlyList<RiskActionDto>>.Failure(Error.NotFound("Risk not found"));
        var actions = await _context.Set<RiskAction>().Include(a => a.Updates)
            .Where(a => a.RiskId == risk.Id && a.TenantId == risk.TenantId)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.CreatedAt).ToListAsync(cancellationToken);
        var names = await _resourceLookup.GetNamesAsync(actions.Where(a => a.OwnerResourceId.HasValue).Select(a => a.OwnerResourceId!.Value), cancellationToken);
        return Result<IReadOnlyList<RiskActionDto>>.Success(actions.Select(a => ToActionDto(a, names)).ToList());
    }

    public async Task<Result<RiskActionFollowUpTargetDto>> GetActionFollowUpTargetAsync(Guid actionPublicId, CancellationToken cancellationToken = default)
    {
        if (actionPublicId == Guid.Empty)
            return Result<RiskActionFollowUpTargetDto>.Failure(Error.NotFound("Risk Action not found"));

        var action = await _context.Set<RiskAction>()
            .Include(item => item.Risk)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.PublicId == actionPublicId, cancellationToken);
        if (action is null || !await IsRiskInScopeAsync(action.RiskId, cancellationToken))
            return Result<RiskActionFollowUpTargetDto>.Failure(Error.NotFound("Risk Action not found"));

        var project = await _projectLookup.FindAsync(action.Risk.ProjectId, cancellationToken);
        if (project is null)
            return Result<RiskActionFollowUpTargetDto>.Failure(Error.NotFound("Risk Action not found"));

        return Result<RiskActionFollowUpTargetDto>.Success(
            new RiskActionFollowUpTargetDto(project.PublicId, action.RiskId, action.Id));
    }

    public async Task<Result<RiskActionDto>> CreateActionAsync(Guid riskId, CreateRiskActionDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var risk = await ScopedRisks().FirstOrDefaultAsync(r => r.Id == riskId, cancellationToken);
        if (risk is null) return Result<RiskActionDto>.Failure(Error.NotFound("Risk not found"));
        if (string.IsNullOrWhiteSpace(dto.Title)) return Failure<RiskActionDto>("Action title is required");
        if (dto.Status.HasValue && !IsDefined(dto.Status.Value)) return Failure<RiskActionDto>("Action status is invalid");
        if (!await IsValidOwnerAsync(dto.OwnerResourceId, dto.OwnerUserId, cancellationToken)) return Failure<RiskActionDto>("Specify either a valid Resource owner or an active same-tenant System User owner, not both.");
        var action = new RiskAction { TenantId = risk.TenantId, RiskId = risk.Id, Title = dto.Title.Trim(), Description = dto.Description.Trim(), OwnerResourceId = dto.OwnerResourceId, OwnerUserId = dto.OwnerUserId, TargetDate = dto.TargetDate, NextFollowUpDate = ToPersistenceDate(dto.NextFollowUpDate), Status = dto.Status ?? RiskActionStatus.Planned, SortOrder = dto.SortOrder, CreatedBy = userId };
        _context.Set<RiskAction>().Add(action);
        if (risk.Status == RiskStatus.Identified) risk.Status = RiskStatus.ActionRequired;
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("RiskAction", action.Id, "Create", userId, dto, cancellationToken);
        var names = await _resourceLookup.GetNamesAsync(action.OwnerResourceId.HasValue ? new[] { action.OwnerResourceId.Value } : Array.Empty<Guid>(), cancellationToken);
        return Result<RiskActionDto>.Success(ToActionDto(action, names));
    }

    public async Task<Result<RiskActionDto>> UpdateActionAsync(Guid riskId, Guid actionId, UpdateRiskActionDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (!await IsRiskInScopeAsync(riskId, cancellationToken)) return Result<RiskActionDto>.Failure(Error.NotFound("Action not found"));
        var action = await _context.Set<RiskAction>().FirstOrDefaultAsync(a => a.Id == actionId && a.RiskId == riskId, cancellationToken);
        if (action is null) return Result<RiskActionDto>.Failure(Error.NotFound("Action not found"));
        if (string.IsNullOrWhiteSpace(dto.Title)) return Failure<RiskActionDto>("Action title is required");
        if (!IsDefined(dto.Status)) return Failure<RiskActionDto>("Action status is invalid");
        if (!await IsValidOwnerAsync(dto.OwnerResourceId, dto.OwnerUserId, cancellationToken)) return Failure<RiskActionDto>("Specify either a valid Resource owner or an active same-tenant System User owner, not both.");
        if (dto.RowVersion.Length > 0 && !dto.RowVersion.SequenceEqual(action.RowVersion)) return Result<RiskActionDto>.Failure(Error.Conflict("Action was changed by another user"));

        var title = dto.Title.Trim();
        var description = dto.Description.Trim();
        var changes = new List<RiskActionChangeDto>();
        AddChange(changes, "Title", action.Title, title);
        AddChange(changes, "Description", action.Description, description);
        if (action.OwnerResourceId != dto.OwnerResourceId || action.OwnerUserId != dto.OwnerUserId)
        {
            var ownerIds = new[] { action.OwnerResourceId, dto.OwnerResourceId }.Where(id => id.HasValue).Select(id => id!.Value).Distinct();
            var ownerNames = await _resourceLookup.GetNamesAsync(ownerIds, cancellationToken);
            var oldOwnerName = action.OwnerUserId.HasValue
                ? await GetActiveUserNameAsync(action.OwnerUserId.Value, action.TenantId, cancellationToken)
                : action.OwnerResourceId.HasValue ? ownerNames.GetValueOrDefault(action.OwnerResourceId.Value, "Unknown owner") : null;
            var newOwnerName = dto.OwnerUserId.HasValue
                ? await GetActiveUserNameAsync(dto.OwnerUserId.Value, action.TenantId, cancellationToken)
                : dto.OwnerResourceId.HasValue ? ownerNames.GetValueOrDefault(dto.OwnerResourceId.Value, "Unknown owner") : null;
            changes.Add(new RiskActionChangeDto
            {
                Field = "Owner",
                OldValue = oldOwnerName,
                NewValue = newOwnerName
            });
        }
        AddChange(changes, "TargetDate", FormatDate(action.TargetDate), FormatDate(dto.TargetDate));
        AddChange(changes, "NextFollowUpDate", FormatDateOnly(action.NextFollowUpDate), FormatDate(dto.NextFollowUpDate));
        AddChange(changes, "Status", action.Status.ToString(), dto.Status.ToString());
        AddChange(changes, "SortOrder", action.SortOrder.ToString(System.Globalization.CultureInfo.InvariantCulture), dto.SortOrder.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var names = await _resourceLookup.GetNamesAsync(dto.OwnerResourceId.HasValue ? new[] { dto.OwnerResourceId.Value } : Array.Empty<Guid>(), cancellationToken);
        if (changes.Count == 0) return Result<RiskActionDto>.Success(ToActionDto(action, names));

        if (dto.RowVersion.Length > 0) _context.Entry(action).Property(a => a.RowVersion).OriginalValue = dto.RowVersion;
        action.Title = title; action.Description = description; action.OwnerResourceId = dto.OwnerResourceId; action.OwnerUserId = dto.OwnerUserId; action.TargetDate = dto.TargetDate; action.NextFollowUpDate = ToPersistenceDate(dto.NextFollowUpDate); action.Status = dto.Status; action.SortOrder = dto.SortOrder; action.UpdatedBy = userId;
        var timelineEntry = new RiskActionUpdate
        {
            TenantId = action.TenantId,
            RiskActionId = action.Id,
            UpdateText = "Action updated",
            EntryType = RiskActionTimelineEntryType.SystemChange,
            ChangeDetailsJson = System.Text.Json.JsonSerializer.Serialize(changes),
            CreatedBy = userId
        };
        _context.Set<RiskActionUpdate>().Add(timelineEntry);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<RiskActionDto>.Failure(Error.Conflict("Action was changed by another user")); }
        await AuditAsync("RiskAction", action.Id, "Update", userId, dto, cancellationToken);
        return Result<RiskActionDto>.Success(ToActionDto(action, names));
    }

    public async Task<Result> DeleteActionAsync(Guid riskId, Guid actionId, string userId, CancellationToken cancellationToken = default)
    {
        if (!await IsRiskInScopeAsync(riskId, cancellationToken)) return Result.Failure(Error.NotFound("Action not found"));
        var action = await _context.Set<RiskAction>().FirstOrDefaultAsync(a => a.Id == actionId && a.RiskId == riskId, cancellationToken);
        if (action is null) return Result.Failure(Error.NotFound("Action not found"));
        action.IsDeleted = true; action.UpdatedBy = userId; await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("RiskAction", action.Id, "Delete", userId, null, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<RiskActionUpdateDto>>> GetActionUpdatesAsync(Guid riskId, Guid actionId, CancellationToken cancellationToken = default)
    {
        var action = await GetActionAsync(riskId, actionId, cancellationToken);
        if (action is null) return Result<IReadOnlyList<RiskActionUpdateDto>>.Failure(Error.NotFound("Action not found"));
        // Timeline order is oldest first so the history reads chronologically.
        var updates = await _context.Set<RiskActionUpdate>().Where(update => update.RiskActionId == actionId).OrderBy(update => update.CreatedAt).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<RiskActionUpdateDto>>.Success(updates.Select(ToActionUpdateDto).ToList());
    }

    public async Task<Result<RiskActionUpdateDto>> AddActionUpdateAsync(Guid riskId, Guid actionId, CreateRiskActionUpdateDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var action = await GetActionAsync(riskId, actionId, cancellationToken);
        if (action is null) return Result<RiskActionUpdateDto>.Failure(Error.NotFound("Action not found"));
        if (string.IsNullOrWhiteSpace(dto.UpdateText)) return Failure<RiskActionUpdateDto>("Update text is required");
        var update = new RiskActionUpdate { TenantId = action.TenantId, RiskActionId = actionId, UpdateText = dto.UpdateText.Trim(), CreatedBy = userId };
        _context.Set<RiskActionUpdate>().Add(update);
        if (action.Status == RiskActionStatus.Planned) action.Status = RiskActionStatus.InProgress;
        var risk = await ScopedRisks().FirstAsync(item => item.Id == riskId, cancellationToken);
        if (risk.Status is RiskStatus.Identified or RiskStatus.ActionRequired) risk.Status = RiskStatus.FollowUp;
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("RiskActionUpdate", update.Id, "Create", userId, dto, cancellationToken);
        return Result<RiskActionUpdateDto>.Success(ToActionUpdateDto(update));
    }

    private async Task<RiskAction?> GetActionAsync(Guid riskId, Guid actionId, CancellationToken cancellationToken)
    {
        if (!await IsRiskInScopeAsync(riskId, cancellationToken)) return null;
        return await _context.Set<RiskAction>().FirstOrDefaultAsync(a => a.Id == actionId && a.RiskId == riskId, cancellationToken);
    }
    /// <summary>
    /// Canonical lookup for a risk that can be addressed by the active API surface.
    /// The Risk global query filter applies the current tenant and soft-delete rules;
    /// every detail/child read starts here so they cannot drift into different scopes.
    /// </summary>
    private Task<Risk?> GetActiveRiskAsync(Guid riskId, CancellationToken cancellationToken) =>
        ScopedRisks().FirstOrDefaultAsync(risk => risk.Id == riskId, cancellationToken);
    private IQueryable<Risk> ScopedRisks()
    {
        var query = _context.Set<Risk>();
        var projectIds = _managementContext?.Current?.Scope.ProjectIds;
        return projectIds is null
            ? query.Where(_ => false)
            : query.Where(risk => projectIds.Contains(risk.ProjectId));
    }
    private Task<bool> IsRiskInScopeAsync(Guid riskId, CancellationToken cancellationToken) =>
        ScopedRisks().AnyAsync(risk => risk.Id == riskId, cancellationToken);
    private async Task<bool> IsValidResourceAsync(Guid? id, CancellationToken cancellationToken) => !id.HasValue || await _resourceLookup.ExistsAsync(id.Value, cancellationToken);
    private async Task<bool> IsValidOwnerAsync(Guid? resourceId, Guid? userId, CancellationToken cancellationToken)
    {
        if (resourceId.HasValue && userId.HasValue) return false;
        if (!await IsValidResourceAsync(resourceId, cancellationToken)) return false;
        if (!userId.HasValue) return true;
        var tenantId = GetCurrentTenantId();
        return await _context.Set<User>().AnyAsync(user => user.Id == userId.Value
            && user.TenantId == tenantId
            && user.IsActive
            && !user.IsDeleted,
            cancellationToken);
    }

    private async Task<string?> GetActiveUserNameAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken) =>
        await _context.Set<User>()
            .Where(user => user.Id == userId && user.TenantId == tenantId && user.IsActive && !user.IsDeleted)
            .Select(user => (user.FirstName + " " + user.LastName).Trim())
            .FirstOrDefaultAsync(cancellationToken);
    private static bool IsDefined<T>(T value) where T : struct, Enum => Enum.IsDefined(value);
    private static bool ValidateImpact(IReadOnlyList<RiskImpactType>? types, string? description) => types is not null && types.Count > 0 && types.Distinct().Count() == types.Count && types.All(IsDefined) && !string.IsNullOrWhiteSpace(description);
    private Guid GetCurrentTenantId() => Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value, out var parsed) && parsed != Guid.Empty
        ? parsed
        : throw new InvalidOperationException("A trusted tenant context is required.");
    private WorkflowExecutionContext BuildWorkflowContext(string userId) => new(GetCurrentTenantId(), userId, _httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value, _httpContextAccessor.HttpContext?.User.FindFirst("readOnly")?.Value == "true");
    private static Result<T> Failure<T>(string message) => Result<T>.Failure(Error.Validation(message));

    private async Task<RiskDto> ToRiskDtoAsync(Risk risk, ProjectReference project, CancellationToken cancellationToken)
    {
        var names = await _resourceLookup.GetNamesAsync(risk.OwnerResourceId.HasValue ? new[] { risk.OwnerResourceId.Value } : Array.Empty<Guid>(), cancellationToken);
        return ToRiskDto(risk, project.Name, project.PublicId, names);
    }
    private static RiskDto ToRiskDto(Risk risk, string projectName, Guid projectPublicId, IReadOnlyDictionary<Guid, string> names, IReadOnlyList<RiskImpactType>? impactTypes = null, LatestActionUpdate? latestActionUpdate = null) => new() { Id = risk.Id, ProjectId = risk.ProjectId, ProjectPublicId = projectPublicId, ProjectName = projectName, Title = risk.Title, Description = risk.Description, RiskLevel = risk.RiskLevel, Impact = risk.Impact, Likelihood = risk.Likelihood, Status = risk.Status, ImpactTypes = impactTypes ?? Array.Empty<RiskImpactType>(), LastActionUpdateText = latestActionUpdate?.UpdateText, LastActionUpdateAt = latestActionUpdate?.CreatedAt, LastActionUpdateCreatedBy = latestActionUpdate?.CreatedBy, OwnerResourceId = risk.OwnerResourceId, OwnerUserId = risk.OwnerUserId, OwnerResourceName = risk.OwnerResourceId.HasValue ? names.GetValueOrDefault(risk.OwnerResourceId.Value) : null, CreatedAt = risk.CreatedAt, UpdatedAt = risk.UpdatedAt, CreatedBy = risk.CreatedBy, UpdatedBy = risk.UpdatedBy, RowVersion = risk.RowVersion };
    private sealed record LatestActionUpdate(Guid RiskId, string UpdateText, DateTime CreatedAt, string CreatedBy);
    private static RiskImpactAssessmentDto ToImpactDto(RiskImpactAssessment assessment) => new() { Id = assessment.Id, RiskId = assessment.RiskId, ImpactTypes = assessment.ImpactTypes.OrderBy(type => type.ImpactType).Select(type => type.ImpactType).ToList(), Description = assessment.Description, CreatedAt = assessment.CreatedAt, UpdatedAt = assessment.UpdatedAt, CreatedBy = assessment.CreatedBy, RowVersion = assessment.RowVersion };
    private static RiskActionDto ToActionDto(RiskAction action, IReadOnlyDictionary<Guid, string> names) => new() { Id = action.Id, PublicId = action.PublicId, RiskId = action.RiskId, Title = action.Title, Description = action.Description, OwnerResourceId = action.OwnerResourceId, OwnerUserId = action.OwnerUserId, OwnerResourceName = action.OwnerResourceId.HasValue ? names.GetValueOrDefault(action.OwnerResourceId.Value) : null, TargetDate = action.TargetDate, NextFollowUpDate = ToDateOnly(action.NextFollowUpDate), Status = action.Status, SortOrder = action.SortOrder, CreatedAt = action.CreatedAt, UpdatedAt = action.UpdatedAt, RowVersion = action.RowVersion, UpdateCount = action.Updates.Count };
    private static RiskActionUpdateDto ToActionUpdateDto(RiskActionUpdate update) => new() { Id = update.Id, RiskActionId = update.RiskActionId, UpdateText = update.UpdateText, CreatedBy = update.CreatedBy, CreatedAt = update.CreatedAt, EntryType = update.EntryType, Changes = DeserializeChanges(update.ChangeDetailsJson) };
    private static IReadOnlyList<RiskActionChangeDto> DeserializeChanges(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<RiskActionChangeDto>();
        try { return System.Text.Json.JsonSerializer.Deserialize<List<RiskActionChangeDto>>(json) ?? new List<RiskActionChangeDto>(); }
        catch (System.Text.Json.JsonException) { return Array.Empty<RiskActionChangeDto>(); }
    }
    private static void AddChange(ICollection<RiskActionChangeDto> changes, string field, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal)) changes.Add(new RiskActionChangeDto { Field = field, OldValue = oldValue, NewValue = newValue });
    }
    private static string? FormatDate(DateTime? value) => value?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    private static string? FormatDate(DateOnly? value) => value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    private static string? FormatDateOnly(DateTime? value) => value.HasValue
        ? DateOnly.FromDateTime(value.Value).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
        : null;
    private static DateTime? ToPersistenceDate(DateOnly? value) => value?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
    private static DateOnly? ToDateOnly(DateTime? value) => value.HasValue ? DateOnly.FromDateTime(value.Value) : null;
    private async Task AuditAsync(string type, Guid id, string action, string userId, object? values, CancellationToken cancellationToken) => await _auditService.LogAsync(new AuditEntry { EntityType = type, EntityId = id, Action = action, UserId = Guid.TryParse(userId, out var parsed) ? parsed : null, Timestamp = DateTime.UtcNow, NewValues = values is null ? null : System.Text.Json.JsonSerializer.Serialize(values), CreatedBy = userId }, cancellationToken);
}
