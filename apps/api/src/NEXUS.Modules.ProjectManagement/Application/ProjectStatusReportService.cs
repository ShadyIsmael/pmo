using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.IssueManagement.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Application;

public sealed class ProjectStatusReportService : IProjectStatusReportService
{
    private const int MaxSnapshotItems = 5;
    private static readonly string[] HealthValues = ["Green", "Amber", "Red"];

    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IProjectReportingService _projectReportingService;
    private readonly IProjectPlanProgressLookup _planProgressLookup;
    private readonly ICapacityService _capacityService;
    private readonly IProjectStatusReportWorkflowProvisioningService _workflowProvisioning;
    private readonly IWorkflowInstanceService _workflowInstances;
    private readonly IWorkflowExecutionService _workflowExecution;
    private readonly IManagementContextAccessor _managementContextAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly IStakeholderInvitationService? _stakeholderInvitations;
    private readonly ILogger<ProjectStatusReportService>? _logger;
    private readonly IProjectOrganizationLookup? _projectOrganizationLookup;

    public ProjectStatusReportService(DbContext context, IAuditService auditService, IHttpContextAccessor httpContextAccessor,
        IProjectReportingService projectReportingService, IProjectPlanProgressLookup planProgressLookup, ICapacityService capacityService,
        IProjectStatusReportWorkflowProvisioningService workflowProvisioning, IWorkflowInstanceService workflowInstances,
        IWorkflowExecutionService workflowExecution, IManagementContextAccessor managementContextAccessor, TimeProvider? timeProvider = null,
        IStakeholderInvitationService? stakeholderInvitations = null, ILogger<ProjectStatusReportService>? logger = null,
        IProjectOrganizationLookup? projectOrganizationLookup = null)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _projectReportingService = projectReportingService;
        _planProgressLookup = planProgressLookup;
        _capacityService = capacityService;
        _workflowProvisioning = workflowProvisioning;
        _workflowInstances = workflowInstances;
        _workflowExecution = workflowExecution;
        _managementContextAccessor = managementContextAccessor;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _stakeholderInvitations = stakeholderInvitations;
        _logger = logger;
        _projectOrganizationLookup = projectOrganizationLookup;
    }

    public async Task<Result<PagedResult<ProjectStatusReportListDto>>> GetPagedAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (!await ProjectExistsAsync(projectId, cancellationToken)) return Result<PagedResult<ProjectStatusReportListDto>>.Failure(Error.NotFound("Project not found"));

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _context.Set<ProjectStatusReport>().AsNoTracking().Where(x => x.ProjectId == projectId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.ReportDate).ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ProjectStatusReportListDto
            {
                Id = x.Id,
                ProjectId = x.ProjectId,
                ReportDate = x.ReportDate,
                PeriodFrom = x.PeriodFrom,
                PeriodTo = x.PeriodTo,
                StatusDate = x.StatusDate,
                ReportState = x.ReportState,
                ProjectLifecycleStatusAtReport = x.ProjectLifecycleStatusAtReport,
                OverallHealth = x.OverallHealth,
                OverallProgressPercent = x.OverallProgressPercent,
                PreparedByNameSnapshot = x.PreparedByNameSnapshot,
                PublishedAt = x.PublishedAt
            }).ToListAsync(cancellationToken);

        return Result<PagedResult<ProjectStatusReportListDto>>.Success(new PagedResult<ProjectStatusReportListDto>(items, page, pageSize, total));
    }

    public async Task<Result<ProjectStatusReportOverviewDto>> GetOverviewAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!await ProjectExistsAsync(projectId, cancellationToken)) return Result<ProjectStatusReportOverviewDto>.Failure(Error.NotFound("Project not found"));

        var query = _context.Set<ProjectStatusReport>().AsNoTracking().Where(report => report.ProjectId == projectId);
        var totalCount = await query.CountAsync(cancellationToken);
        var publishedQuery = query.Where(report => report.ReportState == ProjectStatusReportState.Published);
        var publishedCount = await publishedQuery.CountAsync(cancellationToken);
        var latestPublished = await publishedQuery
            .Include(report => report.RiskSnapshots)
            .Include(report => report.IssueSnapshots)
            .Include(report => report.ChangeRequestSnapshots)
            .OrderByDescending(report => report.ReportDate)
            .ThenByDescending(report => report.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Result<ProjectStatusReportOverviewDto>.Success(new ProjectStatusReportOverviewDto
        {
            TotalCount = totalCount,
            PublishedCount = publishedCount,
            LatestPublishedReport = latestPublished is null ? null : MapDetail(latestPublished)
        });
    }

    public async Task<Result<ProjectStatusReportCurrentProgressDto>> GetCurrentProgressAsync(
        Guid projectId,
        DateTime? statusDate = null,
        DateTime? periodFrom = null,
        DateTime? periodTo = null,
        CancellationToken cancellationToken = default)
    {
        var project = await FindProjectInScopeAsync(projectId, cancellationToken);
        if (project is null) return Result<ProjectStatusReportCurrentProgressDto>.Failure(Error.NotFound("Project not found"));

        var planProgress = await _planProgressLookup.GetAsync(projectId, cancellationToken);
        var effectiveDate = (statusDate ?? _timeProvider.GetUtcNow().UtcDateTime).Date;
        var defaultStart = new DateTime(effectiveDate.Year, effectiveDate.Month, 1);
        var defaultEnd = new DateTime(effectiveDate.Year, effectiveDate.Month, DateTime.DaysInMonth(effectiveDate.Year, effectiveDate.Month));
        var periodStart = (periodFrom ?? defaultStart).Date;
        var periodEnd = (periodTo ?? defaultEnd).Date;
        var resourceUtilization = await GetResourceUtilizationAsync(project, periodStart, periodEnd, cancellationToken);

        return Result<ProjectStatusReportCurrentProgressDto>.Success(new ProjectStatusReportCurrentProgressDto
        {
            // Match Project Overview: active Working-plan progress, then the persisted project progress.
            ProjectPlanProgressPercent = planProgress ?? project.Progress,
            ResourceUtilizationStatus = resourceUtilization.Status,
            ResourceUtilizationPercent = resourceUtilization.Percent
        });
    }

    public async Task<Result<ProjectStatusReportDto>> GetByIdAsync(Guid projectId, Guid reportId, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectInScopeAsync(projectId, cancellationToken);
        if (project is null) return Result<ProjectStatusReportDto>.Failure(Error.NotFound("Project not found"));
        var report = await _context.Set<ProjectStatusReport>().AsNoTracking()
            .Include(x => x.RiskSnapshots).Include(x => x.IssueSnapshots).Include(x => x.ChangeRequestSnapshots)
            .FirstOrDefaultAsync(x => x.Id == reportId && x.ProjectId == projectId
                && x.TenantId == project.TenantId && !x.IsDeleted, cancellationToken);
        return report is null
            ? Result<ProjectStatusReportDto>.Failure(Error.NotFound("Status report not found"))
            : Result<ProjectStatusReportDto>.Success(MapDetail(report));
    }

    public async Task<Result<ProjectStatusReportComparisonDto>> ComparePublishedReportsAsync(
        Guid projectId, Guid firstReportId, Guid secondReportId, CancellationToken cancellationToken = default)
    {
        if (firstReportId == secondReportId)
            return Result<ProjectStatusReportComparisonDto>.Failure(Error.Validation("Select two different reports to compare."));

        var firstResult = await GetByIdAsync(projectId, firstReportId, cancellationToken);
        if (firstResult.IsFailure)
            return Result<ProjectStatusReportComparisonDto>.Failure(firstResult.Error!);
        var secondResult = await GetByIdAsync(projectId, secondReportId, cancellationToken);
        if (secondResult.IsFailure)
            return Result<ProjectStatusReportComparisonDto>.Failure(secondResult.Error!);

        var reports = new[] { firstResult.Value!, secondResult.Value! };
        if (reports.Any(report => report.ReportState != ProjectStatusReportState.Published || report.PerformanceSnapshot is null))
            return Result<ProjectStatusReportComparisonDto>.Failure(Error.Conflict("Only published reports with frozen performance snapshots can be compared."));

        return Result<ProjectStatusReportComparisonDto>.Success(ProjectStatusReportComparison.Build(reports[0], reports[1]));
    }

    public async Task<Result<ProjectStatusReportDto>> CreateAsync(Guid projectId, CreateProjectStatusReportDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectInScopeAsync(projectId, cancellationToken);
        if (project is null) return Result<ProjectStatusReportDto>.Failure(Error.NotFound("Project not found"));
        var lifecycleStatus = await ResolveReportLifecycleStatusAsync(project, dto.ProjectLifecycleStatusAtReport, cancellationToken);
        if (lifecycleStatus.IsFailure) return Result<ProjectStatusReportDto>.Failure(lifecycleStatus.Error!);
        var today = await GetBusinessTodayAsync(projectId, cancellationToken);
        var automaticDate = _timeProvider.GetUtcNow().UtcDateTime.Date;
        var validation = ValidateDraft(today, dto.PeriodFrom, dto.PeriodTo, dto.OverallProgressPercent);
        if (validation is not null) return Result<ProjectStatusReportDto>.Failure(validation);
        var planProgress = await _planProgressLookup.GetAsync(projectId, cancellationToken);
        var utilization = await GetResourceUtilizationAsync(project, dto.PeriodFrom, dto.PeriodTo, cancellationToken);
        var preparedByUserId = ParseUserId(userId);
        var preparedByName = await ResolveUserNameAsync(preparedByUserId, userId, cancellationToken);

        var report = new ProjectStatusReport
        {
            ProjectId = projectId,
            ReportDate = automaticDate,
            PeriodFrom = dto.PeriodFrom,
            PeriodTo = dto.PeriodTo,
            StatusDate = automaticDate,
            ReportState = ProjectStatusReportState.Draft,
            ProjectLifecycleStatusAtReport = lifecycleStatus.Value,
            OverallHealth = string.IsNullOrWhiteSpace(dto.OverallHealth) ? UsableHealth(project.Health) : dto.OverallHealth,
            OverallProgressPercent = dto.OverallProgressPercent ?? planProgress ?? project.Progress,
            ScheduleHealth = null,
            CostHealth = dto.CostHealth,
            ScopeHealth = null,
            QualityHealth = dto.QualityHealth,
            ResourceHealth = utilization.Status,
            ExecutiveSummary = dto.ExecutiveSummary,
            KeyAchievements = dto.KeyAchievements,
            ChallengesConcerns = dto.ChallengesConcerns,
            NextPeriodPriorities = dto.NextPeriodPriorities,
            ActionsNeeded = dto.ActionsNeeded,
            SupportNeeded = dto.SupportNeeded,
            PreparedByUserId = preparedByUserId,
            PreparedByNameSnapshot = preparedByName,
            TenantId = project.TenantId,
            CreatedBy = userId
        };

        _context.Set<ProjectStatusReport>().Add(report);
        await _context.SaveChangesAsync(cancellationToken);
        await LogAsync(report, "Create", userId, cancellationToken);
        return await GetByIdAsync(projectId, report.Id, cancellationToken);
    }

    public async Task<Result<ProjectStatusReportDto>> UpdateAsync(Guid projectId, Guid reportId, UpdateProjectStatusReportDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectInScopeAsync(projectId, cancellationToken);
        if (project is null) return Result<ProjectStatusReportDto>.Failure(Error.NotFound("Project not found"));
        var report = await _context.Set<ProjectStatusReport>().FirstOrDefaultAsync(x => x.Id == reportId
            && x.ProjectId == projectId && x.TenantId == project.TenantId && !x.IsDeleted, cancellationToken);
        if (report is null) return Result<ProjectStatusReportDto>.Failure(Error.NotFound("Status report not found"));
        if (report.ReportState != ProjectStatusReportState.Draft) return Result<ProjectStatusReportDto>.Failure(Error.Conflict("Published status reports are immutable"));
        if (dto.RowVersion is { Length: > 0 }) _context.Entry(report).Property(x => x.RowVersion).OriginalValue = dto.RowVersion;

        var lifecycleStatus = await ResolveReportLifecycleStatusAsync(project, dto.ProjectLifecycleStatusAtReport, cancellationToken);
        if (lifecycleStatus.IsFailure) return Result<ProjectStatusReportDto>.Failure(lifecycleStatus.Error!);

        var today = await GetBusinessTodayAsync(projectId, cancellationToken);
        var automaticDate = _timeProvider.GetUtcNow().UtcDateTime.Date;
        var validation = ValidateDraft(today, dto.PeriodFrom, dto.PeriodTo, dto.OverallProgressPercent);
        if (validation is not null) return Result<ProjectStatusReportDto>.Failure(validation);
        ApplyDraftContent(report, dto, automaticDate, lifecycleStatus.Value!);
        if (report.OverallProgressPercent is null)
            report.OverallProgressPercent = await _planProgressLookup.GetAsync(projectId, cancellationToken) ?? project.Progress;
        var utilization = await GetResourceUtilizationAsync(project, dto.PeriodFrom, dto.PeriodTo, cancellationToken);
        report.ResourceHealth = utilization.Status;
        report.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        report.UpdatedBy = userId;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectStatusReportDto>.Failure(Error.Conflict("Status report was changed by another user"));
        }

        await LogAsync(report, "Update", userId, cancellationToken);
        return await GetByIdAsync(projectId, report.Id, cancellationToken);
    }

    public async Task<Result<ProjectStatusReportDto>> PublishAsync(Guid projectId, Guid reportId, UpdateProjectStatusReportDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var scopedProject = await FindProjectInScopeAsync(projectId, cancellationToken);
        if (scopedProject is null) return Result<ProjectStatusReportDto>.Failure(Error.NotFound("Project not found"));
        var reportInfo = await _context.Set<ProjectStatusReport>().AsNoTracking()
            .Where(item => item.Id == reportId && item.ProjectId == projectId
                && item.TenantId == scopedProject.TenantId && !item.IsDeleted)
            .Select(item => new { item.TenantId, item.ReportState })
            .FirstOrDefaultAsync(cancellationToken);
        if (reportInfo is null) return Result<ProjectStatusReportDto>.Failure(Error.NotFound("Status report not found"));
        if (reportInfo.ReportState != ProjectStatusReportState.Draft)
            return Result<ProjectStatusReportDto>.Failure(Error.Conflict("Only Draft reports can be published."));
        if (dto.RowVersion is not { Length: > 0 })
            return Result<ProjectStatusReportDto>.Failure(Error.Conflict("Refresh the Draft before publishing so the latest version is used."));

        var definition = await _workflowProvisioning.EnsureAsync(reportInfo.TenantId, userId, cancellationToken);
        if (definition.IsFailure) return Result<ProjectStatusReportDto>.Failure(definition.Error!);

        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var report = await _context.Set<ProjectStatusReport>()
                .FirstOrDefaultAsync(item => item.Id == reportId && item.ProjectId == projectId
                    && item.TenantId == scopedProject.TenantId && !item.IsDeleted, cancellationToken);
            if (report is null) return Result<ProjectStatusReportDto>.Failure(Error.NotFound("Status report not found"));
            if (report.ReportState != ProjectStatusReportState.Draft)
                return Result<ProjectStatusReportDto>.Failure(Error.Conflict("This status report has already left Draft."));

            var project = await FindProjectInScopeAsync(projectId, cancellationToken);
            if (project is null) return Result<ProjectStatusReportDto>.Failure(Error.NotFound("Project not found"));
            var lifecycleStatus = await ResolveReportLifecycleStatusAsync(project, dto.ProjectLifecycleStatusAtReport, cancellationToken);
            if (lifecycleStatus.IsFailure) return Result<ProjectStatusReportDto>.Failure(lifecycleStatus.Error!);

            _context.Entry(report).Property(item => item.RowVersion).OriginalValue = dto.RowVersion;
            if (HasDraftContentChanged(report, dto, lifecycleStatus.Value!)
                && _httpContextAccessor.HttpContext?.User.HasClaim("permission", "StatusReport.Edit") != true)
                return Result<ProjectStatusReportDto>.Failure(Error.Forbidden("Editing a Draft while publishing also requires StatusReport.Edit."));
            ApplyDraftContent(report, dto, _timeProvider.GetUtcNow().UtcDateTime.Date, lifecycleStatus.Value!);

            var today = await GetBusinessTodayAsync(projectId, cancellationToken);
            var capturedAt = _timeProvider.GetUtcNow().UtcDateTime;
            report.ReportDate = capturedAt.Date;
            report.StatusDate = capturedAt.Date;
            report.ScheduleHealth = null;
            report.ScopeHealth = null;
            if (report.OverallProgressPercent is null)
                report.OverallProgressPercent = await _planProgressLookup.GetAsync(projectId, cancellationToken) ?? project.Progress;
            var utilization = await GetResourceUtilizationAsync(project, report.PeriodFrom, report.PeriodTo, cancellationToken);
            report.ResourceHealth = utilization.Status;

            var validation = ValidateForPublish(report, today);
            if (validation is not null) return Result<ProjectStatusReportDto>.Failure(validation);
            var performance = await _projectReportingService.GetPerformanceAsync(projectId, report.StatusDate, cancellationToken: cancellationToken);
            if (!performance.IsSuccess || performance.Value is null)
                return Result<ProjectStatusReportDto>.Failure(performance.Error ?? Error.Validation("Project performance is unavailable for the report status date."));
            if (performance.Value.ReportingConfigurationVersion is null || performance.Value.IncludedReportSections.Count == 0)
                return Result<ProjectStatusReportDto>.Failure(Error.Conflict("An approved reporting configuration with at least one included section is required for the report status date before publishing."));

            var preparedBy = report.PreparedByUserId ?? ParseUserId(userId);
            var preparedByName = report.PreparedByNameSnapshot ?? await ResolveUserNameAsync(preparedBy, userId, cancellationToken);
            var managerName = await ResolveUserNameAsync(project.ProjectManagerId, "System", cancellationToken);
            report.PerformanceSnapshotJson = JsonSerializer.Serialize(performance.Value);
            report.ReportingConfigurationVersionSnapshot = performance.Value.ReportingConfigurationVersion;
            CaptureProjectSnapshot(report, project, managerName, preparedBy, preparedByName);
            await CaptureFinancialSnapshotAsync(report, project, cancellationToken);
            await CaptureScheduleSnapshotAsync(report, projectId, cancellationToken);
            await CaptureBaselineSnapshotAsync(report, projectId, cancellationToken);
            await CaptureRelatedSnapshotsAsync(report, projectId, cancellationToken);
            report.SourceDataCapturedAt = capturedAt;
            report.PreparedByUserId ??= preparedBy;
            report.PreparedByNameSnapshot ??= preparedByName;
            report.UpdatedAt = capturedAt;
            report.UpdatedBy = userId;

            var workflow = await EnsureWorkflowInstanceAsync(report, userId, cancellationToken);
            if (workflow.IsFailure) return Result<ProjectStatusReportDto>.Failure(workflow.Error!);
            if (!string.Equals(workflow.Value!.CurrentStateCode, ProjectStatusReportState.Draft.ToString(), StringComparison.OrdinalIgnoreCase))
                return Result<ProjectStatusReportDto>.Failure(Error.Conflict("The report and workflow are not both in Draft. Refresh before publishing."));
            var transition = await ExecuteWorkflowTransitionAsync(report, workflow.Value, ProjectStatusReportWorkflow.Publish, userId, cancellationToken);
            if (transition.IsFailure) return Result<ProjectStatusReportDto>.Failure(transition.Error!);

            await LogAsync(report, "Publish", userId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ProjectStatusReportDto>.Failure(Error.Conflict("Status report was changed or published by another user. Refresh and try again."));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ProjectStatusReportDto>.Failure(Error.Conflict("The report changed during publication. Refresh and try again."));
        }
        if (_stakeholderInvitations is not null)
        {
            try
            {
                var published = await _context.Set<ProjectStatusReport>().AsNoTracking()
                    .Where(item => item.Id == reportId && item.ProjectId == projectId && item.ReportState == ProjectStatusReportState.Published && !item.IsDeleted)
                    .Select(item => new
                    {
                        item.StatusDate,
                        item.ProjectLifecycleStatusAtReport,
                        item.OverallHealth,
                        item.OverallProgressPercent,
                        item.ExecutiveSummary,
                        item.KeyAchievements,
                        item.ChallengesConcerns,
                        item.NextPeriodPriorities
                    })
                    .FirstOrDefaultAsync(cancellationToken);
                if (published is not null)
                {
                    var reportContent = string.Join(Environment.NewLine, new[]
                    {
                        string.IsNullOrWhiteSpace(published.ProjectLifecycleStatusAtReport) ? null : $"Project status: {published.ProjectLifecycleStatusAtReport}",
                        string.IsNullOrWhiteSpace(published.OverallHealth) ? null : $"Overall health: {published.OverallHealth}",
                        published.OverallProgressPercent.HasValue ? $"Progress: {published.OverallProgressPercent.Value:0.##}%" : null,
                        string.IsNullOrWhiteSpace(published.ExecutiveSummary) ? null : $"Executive summary: {published.ExecutiveSummary.Trim()}",
                        string.IsNullOrWhiteSpace(published.KeyAchievements) ? null : $"Key achievements: {published.KeyAchievements.Trim()}",
                        string.IsNullOrWhiteSpace(published.ChallengesConcerns) ? null : $"Challenges and concerns: {published.ChallengesConcerns.Trim()}",
                        string.IsNullOrWhiteSpace(published.NextPeriodPriorities) ? null : $"Next period priorities: {published.NextPeriodPriorities.Trim()}"
                    }.Where(value => value is not null)!);
                    await _stakeholderInvitations.NotifyProjectReportPublishedAsync(
                        projectId, reportId, published.StatusDate, reportContent, cancellationToken);
                }
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "Project status report {ReportId} was published, but stakeholder email delivery did not complete.", reportId);
            }
        }
        return await GetByIdAsync(projectId, reportId, cancellationToken);
    }

    private async Task<Result<WorkflowInstanceSummary>> EnsureWorkflowInstanceAsync(ProjectStatusReport report, string userId, CancellationToken cancellationToken)
    {
        var execution = BuildWorkflowContext(userId);
        if (execution.TenantId == Guid.Empty || execution.TenantId != report.TenantId)
            return Result<WorkflowInstanceSummary>.Failure(Error.Forbidden("The authenticated tenant does not own this report."));
        var definition = await _workflowProvisioning.EnsureAsync(report.TenantId, userId, cancellationToken);
        if (definition.IsFailure) return Result<WorkflowInstanceSummary>.Failure(definition.Error!);
        var existing = await _workflowInstances.GetInstanceAsync(ProjectStatusReportWorkflow.EntityType, report.Id, execution, cancellationToken);
        if (existing.IsFailure) return Result<WorkflowInstanceSummary>.Failure(existing.Error!);
        if (existing.Value is { } currentInstance)
        {
            if (currentInstance.VersionId == definition.Value!.VersionId)
                return Result<WorkflowInstanceSummary>.Success(currentInstance);
            if (report.ReportState != ProjectStatusReportState.Draft
                || !string.Equals(currentInstance.CurrentStateCode, ProjectStatusReportState.Draft.ToString(), StringComparison.OrdinalIgnoreCase))
                return Result<WorkflowInstanceSummary>.Failure(Error.Conflict("Legacy workflow records remain unchanged; only a Draft report can move to the simplified workflow."));

            var legacyInstance = await _context.Set<WorkflowInstance>()
                .FirstOrDefaultAsync(item => item.Id == currentInstance.InstanceId && item.TenantId == report.TenantId && !item.IsDeleted, cancellationToken);
            if (legacyInstance is null)
                return Result<WorkflowInstanceSummary>.Failure(Error.Conflict("The legacy Draft workflow record could not be migrated safely."));
            legacyInstance.IsActive = false;
                legacyInstance.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            legacyInstance.UpdatedBy = userId;
            await _context.SaveChangesAsync(cancellationToken);
            await LogAsync(report, "WorkflowVersionMigrated", userId, cancellationToken);
        }
        var provisioned = await _workflowInstances.ProvisionAsync(ProjectStatusReportWorkflow.EntityType, report.Id,
            definition.Value!.DefinitionId, definition.Value.VersionId, execution, cancellationToken);
        if (provisioned.IsSuccess) return provisioned;

        // Another publish request may have provisioned the same Draft instance or
        // advanced the definition while this request was resolving it. Re-read both
        // records before turning that benign race into a user-visible conflict.
        var concurrentInstance = await _workflowInstances.GetInstanceAsync(
            ProjectStatusReportWorkflow.EntityType, report.Id, execution, cancellationToken);
        if (concurrentInstance.IsSuccess && concurrentInstance.Value is { } winner)
            return Result<WorkflowInstanceSummary>.Success(winner);

        var currentDefinition = await _workflowProvisioning.EnsureAsync(report.TenantId, userId, cancellationToken);
        if (currentDefinition.IsFailure) return Result<WorkflowInstanceSummary>.Failure(currentDefinition.Error!);
        if (currentDefinition.Value!.VersionId != definition.Value.VersionId)
            return await _workflowInstances.ProvisionAsync(ProjectStatusReportWorkflow.EntityType, report.Id,
                currentDefinition.Value.DefinitionId, currentDefinition.Value.VersionId, execution, cancellationToken);

        return provisioned;
    }

    private async Task<Result<WorkflowTransitionResult>> ExecuteWorkflowTransitionAsync(ProjectStatusReport report,
        WorkflowInstanceSummary workflow, string transitionCode, string userId, CancellationToken cancellationToken)
    {
        var execution = BuildWorkflowContext(userId);
        return await _workflowExecution.ExecuteTransitionAsync(new WorkflowTransitionRequest(
            ProjectStatusReportWorkflow.EntityType, report.Id, transitionCode,
            ExpectedStateCode: workflow.CurrentStateCode, ExpectedRowVersion: workflow.RowVersion,
            CorrelationId: execution.CorrelationId), execution, cancellationToken);
    }

    private WorkflowExecutionContext BuildWorkflowContext(string userId)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var tenantId = Guid.TryParse(principal?.FindFirst("tenantId")?.Value, out var parsedTenant) ? parsedTenant : Guid.Empty;
        var effectiveReadOnly = principal?.FindFirst("readOnly")?.Value == "true"
            || principal?.FindFirst("viewAs")?.Value == "true"
            || _managementContextAccessor.Current?.EffectiveReadOnly == true;
        return new WorkflowExecutionContext(
            tenantId,
            userId,
            principal?.FindFirst(ClaimTypes.Name)?.Value ?? principal?.FindFirst("name")?.Value,
            effectiveReadOnly,
            _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault(),
            null,
            new Dictionary<string, string?>
            {
                ["permissions"] = principal?.FindFirst("permissions")?.Value,
                ["roles"] = principal?.FindFirst("roles")?.Value
            });
    }

    public async Task<Result> DeleteAsync(Guid projectId, Guid reportId, string userId, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectInScopeAsync(projectId, cancellationToken);
        if (project is null) return Result.Failure(Error.NotFound("Project not found"));
        var report = await _context.Set<ProjectStatusReport>().FirstOrDefaultAsync(x => x.Id == reportId
            && x.ProjectId == projectId && x.TenantId == project.TenantId && !x.IsDeleted, cancellationToken);
        if (report is null) return Result.Failure(Error.NotFound("Status report not found"));
        if (report.ReportState != ProjectStatusReportState.Draft) return Result.Failure(Error.Conflict("Published status reports are immutable"));
        report.IsDeleted = true;
        report.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        report.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        await LogAsync(report, "Delete", userId, cancellationToken);
        return Result.Success();
    }

    private async Task CaptureRelatedSnapshotsAsync(ProjectStatusReport report, Guid projectId, CancellationToken cancellationToken)
    {
        _context.Set<ProjectStatusReportRiskSnapshot>().RemoveRange(await _context.Set<ProjectStatusReportRiskSnapshot>()
            .Where(item => item.ProjectStatusReportId == report.Id).ToListAsync(cancellationToken));
        _context.Set<ProjectStatusReportIssueSnapshot>().RemoveRange(await _context.Set<ProjectStatusReportIssueSnapshot>()
            .Where(item => item.ProjectStatusReportId == report.Id).ToListAsync(cancellationToken));
        _context.Set<ProjectStatusReportChangeRequestSnapshot>().RemoveRange(await _context.Set<ProjectStatusReportChangeRequestSnapshot>()
            .Where(item => item.ProjectStatusReportId == report.Id).ToListAsync(cancellationToken));

        var users = await _context.Set<User>().AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        var risks = (await _context.Set<Risk>().AsNoTracking().Where(x => x.ProjectId == projectId).ToListAsync(cancellationToken))
            .OrderByDescending(x => RiskLevelRank(x.RiskLevel)).ThenByDescending(x => RiskStatusRank(x.Status))
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenBy(x => x.Id).Take(MaxSnapshotItems).ToList();
        var issues = (await _context.Set<Issue>().AsNoTracking().Where(x => x.ProjectId == projectId).ToListAsync(cancellationToken))
            .OrderByDescending(x => IssueSeverityRank(x.Severity)).ThenByDescending(x => IssuePriorityRank(x.Priority))
            .ThenByDescending(x => IssueStatusRank(x.Status)).ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenBy(x => x.Id).Take(MaxSnapshotItems).ToList();
        var requests = (await _context.Set<ProjectChangeRequest>().AsNoTracking().Where(x => x.ProjectId == projectId).ToListAsync(cancellationToken))
            .OrderByDescending(x => ChangeRequestStatusRank(x.Status)).ThenByDescending(x => x.RequestedAt).ThenBy(x => x.Number).Take(MaxSnapshotItems).ToList();

        var riskSnapshots = risks.Select((x, index) => new ProjectStatusReportRiskSnapshot
        {
            TenantId = report.TenantId, ProjectStatusReportId = report.Id, SourceRiskId = x.Id, Rank = index + 1,
            Title = x.Title, Description = x.Description, Impact = x.Impact, Likelihood = x.Likelihood,
            RiskLevel = x.RiskLevel.ToString(), RiskStatus = x.Status.ToString(), PotentialCost = x.PotentialCost,
            PotentialDelay = x.PotentialDelay, RiskResponse = x.RiskResponse, OwnerId = x.OwnerId,
            OwnerNameSnapshot = x.OwnerId.HasValue && users.TryGetValue(x.OwnerId.Value, out var owner) ? owner : null,
            IdentifiedDate = x.IdentifiedDate, ClosedDate = x.ClosedDate, CreatedBy = report.UpdatedBy ?? report.CreatedBy
        }).ToList();
        var issueSnapshots = issues.Select((x, index) => new ProjectStatusReportIssueSnapshot
        {
            TenantId = report.TenantId, ProjectStatusReportId = report.Id, SourceIssueId = x.Id, Rank = index + 1,
            Title = x.Title, Description = x.Description, Severity = x.Severity, Priority = x.Priority, IssueStatus = x.Status,
            AssigneeId = x.AssigneeId, AssigneeNameSnapshot = x.AssigneeId.HasValue && users.TryGetValue(x.AssigneeId.Value, out var assignee) ? assignee : null,
            IdentifiedDate = x.IdentifiedDate, TargetResolutionDate = x.TargetResolutionDate, ResolvedDate = x.ResolvedDate,
            Resolution = x.Resolution, CreatedBy = report.UpdatedBy ?? report.CreatedBy
        }).ToList();
        var changeRequestSnapshots = requests.Select((x, index) => new ProjectStatusReportChangeRequestSnapshot
        {
            TenantId = report.TenantId, ProjectStatusReportId = report.Id, SourceChangeRequestId = x.Id, Rank = index + 1,
            Number = x.Number, ChangeType = x.Type.ToString(), Reason = x.Reason, Justification = x.Justification, Impact = x.Impact,
            ChangeRequestStatus = x.Status.ToString(), RequestedBy = x.RequestedBy,
            RequestedByNameSnapshot = users.TryGetValue(x.RequestedBy, out var requester) ? requester : null,
            RequestedAt = x.RequestedAt, ReviewedAt = x.ReviewedAt, DecisionNotes = x.DecisionNotes,
            CreatedBy = report.UpdatedBy ?? report.CreatedBy
        }).ToList();
        report.RiskSnapshots = riskSnapshots;
        report.IssueSnapshots = issueSnapshots;
        report.ChangeRequestSnapshots = changeRequestSnapshots;
        _context.Set<ProjectStatusReportRiskSnapshot>().AddRange(riskSnapshots);
        _context.Set<ProjectStatusReportIssueSnapshot>().AddRange(issueSnapshots);
        _context.Set<ProjectStatusReportChangeRequestSnapshot>().AddRange(changeRequestSnapshots);
    }

    private void CaptureProjectSnapshot(ProjectStatusReport report, Project project, string? managerName, Guid? preparedBy, string? preparedByName)
    {
        report.ProjectNameSnapshot = project.Name;
        report.ProjectCodeSnapshot = project.Code;
        report.ProjectManagerIdAtReport = project.ProjectManagerId;
        report.ProjectManagerNameSnapshot = managerName;
        report.PreparedByUserId ??= preparedBy;
        report.PreparedByNameSnapshot ??= preparedByName;
    }

    private async Task CaptureFinancialSnapshotAsync(ProjectStatusReport report, Project project, CancellationToken cancellationToken)
    {
        var currency = project.Currency;
        var budgets = await _context.Set<Budget>().AsNoTracking().Where(x => x.ProjectId == project.Id && x.Currency == currency).ToListAsync(cancellationToken);
        var costEntries = await _context.Set<CostEntry>().AsNoTracking().Where(x => x.ProjectId == project.Id && x.Currency == currency).ToListAsync(cancellationToken);
        report.CurrencyCodeSnapshot = currency;
        report.BudgetAmountSnapshot = budgets.Count > 0 ? budgets.Sum(x => x.ApprovedAmount) : project.Budget;
        report.ActualCostAmountSnapshot = budgets.Count > 0 ? budgets.Sum(x => x.ActualAmount) : project.ActualCost ?? (costEntries.Count > 0 ? costEntries.Sum(x => x.Amount) : null);
        report.CommittedCostAmountSnapshot = budgets.Count > 0 ? budgets.Sum(x => x.CommittedAmount) : null;
        report.ForecastCostAmountSnapshot = budgets.Count > 0 ? budgets.Sum(x => x.ForecastAmount) : null;
        report.CostVarianceAmountSnapshot = report.BudgetAmountSnapshot.HasValue && report.ActualCostAmountSnapshot.HasValue
            ? report.BudgetAmountSnapshot.Value - report.ActualCostAmountSnapshot.Value : null;

        var periodTo = report.PeriodTo.Date;
        var periodFrom = report.PeriodFrom.Date;

        var obligations = await _context.Set<PaymentObligation>().AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.ProjectId == project.Id
                && item.Currency == currency)
            .ToListAsync(cancellationToken);

        var obligationIds = obligations.Select(item => item.Id).ToList();

        var settlements = await _context.Set<PaymentSettlement>().AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.PaymentObligationId.HasValue
                && obligationIds.Contains(item.PaymentObligationId.Value)
                && item.SettlementDate.Date <= periodTo)
            .ToListAsync(cancellationToken);

        var settledByObligation = settlements
            .GroupBy(item => item.PaymentObligationId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

        decimal settled(PaymentObligation o) => settledByObligation.TryGetValue(o.Id, out var amount) ? amount : 0m;

        report.ActualPaidSnapshot = obligations.Sum(item => settled(item));
        report.OutstandingPlannedPaymentsSnapshot = obligations
            .Select(item => Math.Max(0m, item.Amount - settled(item)))
            .Where(amount => amount > 0m)
            .Sum();
        report.PaymentsDueWithinPeriodSnapshot = obligations
            .Where(item => {
                var outstanding = Math.Max(0m, item.Amount - settled(item));
                return outstanding > 0m && item.DueDate.Date >= periodFrom && item.DueDate.Date <= periodTo;
            })
            .Sum(item => Math.Max(0m, item.Amount - settled(item)));
        report.RemainingUnpaidAtPeriodEndSnapshot = obligations
            .Where(item => {
                var outstanding = Math.Max(0m, item.Amount - settled(item));
                return outstanding > 0m && item.DueDate.Date <= periodTo;
            })
            .Sum(item => Math.Max(0m, item.Amount - settled(item)));
    }

    private async Task CaptureScheduleSnapshotAsync(ProjectStatusReport report, Guid projectId, CancellationToken cancellationToken)
    {
        var schedule = await _context.Set<ProjectSchedule>().AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (schedule?.ForecastEndDate is not null && schedule.BaselineEndDate is not null)
        {
            var varianceDays = (decimal)(schedule.ForecastEndDate.Value.Date - schedule.BaselineEndDate.Value.Date).TotalDays;
            report.ScheduleVarianceDaysSnapshot = varianceDays;
            if (schedule.BaselineStartDate.HasValue && schedule.BaselineEndDate.Value > schedule.BaselineStartDate.Value)
            {
                report.ScheduleVariancePercentSnapshot = varianceDays / (decimal)(schedule.BaselineEndDate.Value.Date - schedule.BaselineStartDate.Value.Date).TotalDays * 100;
            }
        }
    }

    private async Task CaptureBaselineSnapshotAsync(ProjectStatusReport report, Guid projectId, CancellationToken cancellationToken)
    {
        var baseline = await _context.Set<Baseline>().AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.IsCurrent).ThenByDescending(x => x.Version).ThenByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (baseline is null) return;
        report.BaselineIdAtReport = baseline.Id;
        report.BaselineNameSnapshot = baseline.Name;
        report.BaselineVersionSnapshot = baseline.Version;
    }

    private async Task<string?> ResolveUserNameAsync(Guid? userId, string fallback, CancellationToken cancellationToken)
    {
        if (userId.HasValue)
        {
            var name = await _context.Set<User>().AsNoTracking().Where(x => x.Id == userId.Value).Select(x => x.FullName).FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return fallback == "System" ? null : fallback;
    }

    private sealed record ResourceUtilizationSnapshot(string Status, decimal? Percent);

    private async Task<ResourceUtilizationSnapshot> GetResourceUtilizationAsync(
        Project project,
        DateTime periodFrom,
        DateTime periodTo,
        CancellationToken cancellationToken)
    {
        var startDate = periodFrom.Date;
        var endDate = periodTo.Date;
        if (startDate > endDate) return new(ProjectResourceUtilizationStatuses.Unavailable, null);
        if (startDate == endDate) endDate = endDate.AddDays(1);

        var assignments = await _context.Set<ProjectMember>().AsNoTracking()
            .Where(member => member.ProjectId == project.Id
                && member.TenantId == project.TenantId && !member.IsDeleted
                && member.Status == "Active"
                && member.StartDate.Date < endDate
                && member.EndDate.Date >= startDate)
            .ToListAsync(cancellationToken);

        var capacityCandidates = new List<ProjectTeamCapacityMemberInputDto>();
        var directResourceIds = assignments.Where(member => member.ResourceId.HasValue)
            .Select(member => member.ResourceId!.Value).Distinct().ToArray();
        var activeResources = directResourceIds.Length == 0 ? new HashSet<Guid>() : await _context.Set<Resource>().AsNoTracking()
            .Where(resource => directResourceIds.Contains(resource.Id) && resource.TenantId == project.TenantId
                && resource.IsActive && !resource.IsDeleted)
            .Select(resource => resource.Id).ToHashSetAsync(cancellationToken);
        foreach (var member in assignments.Where(member => member.ResourceId.HasValue && activeResources.Contains(member.ResourceId.Value)))
            capacityCandidates.Add(new ProjectTeamCapacityMemberInputDto
            {
                ResourceId = member.ResourceId!.Value,
                ProjectId = project.Id,
                StartDate = member.StartDate,
                EndDate = member.EndDate,
                AllocationPercentage = member.AllocationPercentage,
                HoursPerWeek = null,
                UseProjectCalendar = member.CalendarPolicy == CalendarPolicyConstants.FollowProjectCalendar,
                ProjectCalendarId = project.WorkingCalendarId
            });

        var groupAssignments = assignments.Where(member => member.TeamId.HasValue).ToList();
        var teamIds = groupAssignments.Select(member => member.TeamId!.Value).Distinct().ToArray();
        var activeTeamIds = teamIds.Length == 0 ? Array.Empty<Guid>() : await _context.Set<Team>().AsNoTracking()
            .Where(team => teamIds.Contains(team.Id) && team.TenantId == project.TenantId && team.IsActive && !team.IsDeleted)
            .Select(team => team.Id).ToArrayAsync(cancellationToken);
        if (activeTeamIds.Length > 0)
        {
            var teamMemberships = await _context.Set<TeamMember>().AsNoTracking()
                .Where(member => activeTeamIds.Contains(member.TeamId) && member.IsActive && !member.IsDeleted
                    && member.EffectiveFrom.Date < endDate
                    && (!member.EffectiveTo.HasValue || member.EffectiveTo.Value.Date >= startDate))
                .Join(_context.Set<Resource>().AsNoTracking().Where(resource => resource.TenantId == project.TenantId
                        && resource.IsActive && !resource.IsDeleted),
                    membership => membership.ResourceId, resource => resource.Id,
                    (membership, resource) => new { membership.TeamId, membership.ResourceId, membership.EffectiveFrom, membership.EffectiveTo })
                .ToListAsync(cancellationToken);
            foreach (var assignment in groupAssignments.Where(item => activeTeamIds.Contains(item.TeamId!.Value)))
            foreach (var membership in teamMemberships.Where(item => item.TeamId == assignment.TeamId))
            {
                var memberStart = membership.EffectiveFrom.Date > assignment.StartDate.Date
                    ? membership.EffectiveFrom.Date : assignment.StartDate.Date;
                var memberEnd = membership.EffectiveTo?.Date ?? assignment.EndDate.Date;
                if (memberEnd > assignment.EndDate.Date) memberEnd = assignment.EndDate.Date;
                if (memberStart >= endDate || memberEnd < startDate || memberStart > memberEnd) continue;
                capacityCandidates.Add(new ProjectTeamCapacityMemberInputDto
                {
                    ResourceId = membership.ResourceId,
                    ProjectId = project.Id,
                    StartDate = memberStart,
                    EndDate = memberEnd,
                    AllocationPercentage = assignment.AllocationPercentage,
                    HoursPerWeek = null,
                    UseProjectCalendar = assignment.CalendarPolicy == CalendarPolicyConstants.FollowProjectCalendar,
                    ProjectCalendarId = project.WorkingCalendarId
                });
            }
        }

        var members = capacityCandidates.GroupBy(member => member.ResourceId).Select(group =>
        {
            var selected = group.OrderByDescending(member => member.AllocationPercentage ?? 0m).ThenBy(member => member.StartDate).First();
            return new ProjectTeamCapacityMemberInputDto
            {
                ResourceId = group.Key,
                ProjectId = project.Id,
                StartDate = group.Min(member => member.StartDate),
                EndDate = group.Max(member => member.EndDate),
                AllocationPercentage = group.Max(member => member.AllocationPercentage ?? 0m),
                HoursPerWeek = null,
                UseProjectCalendar = selected.UseProjectCalendar,
                ProjectCalendarId = project.WorkingCalendarId
            };
        }).ToArray();

        var capacity = await _capacityService.GetProjectTeamOverviewAsync(members, new CapacityFilterDto
        {
            PeriodType = "Month",
            StartDate = startDate,
            EndDate = endDate,
            IncludeProposed = true
        }, cancellationToken);
        if (capacity.IsFailure || capacity.Value is null
            || !capacity.Value.IsCapacityDataAvailable
            || !capacity.Value.IsAllocationDataAvailable)
            return new(ProjectResourceUtilizationStatuses.Unavailable, null);

        var percent = capacity.Value.UtilizedPercent;
        if (capacity.Value.OverallocatedHours.GetValueOrDefault() > 0m || (percent.HasValue && percent.Value > 100m))
            return new(ProjectResourceUtilizationStatuses.Overallocated, percent);
        if (!percent.HasValue) return new(ProjectResourceUtilizationStatuses.Unavailable, null);
        if (capacity.Value.UtilizedHours.GetValueOrDefault() <= 0m)
            return new(ProjectResourceUtilizationStatuses.NoAssignedWork, percent);
        return new(ProjectResourceUtilizationStatuses.WithinCapacity, percent);
    }

    private IQueryable<Project> InScope(IQueryable<Project> projects)
    {
        var management = _managementContextAccessor.Current;
        if (management is null || management.TenantId == Guid.Empty)
            return projects.Where(_ => false);

        var projectIds = management.Scope.ProjectIds.ToArray();
        return projects.Where(project => project.TenantId == management.TenantId && projectIds.Contains(project.Id));
    }

    private Task<Project?> FindProjectInScopeAsync(Guid projectId, CancellationToken cancellationToken) =>
        InScope(_context.Set<Project>())
            .FirstOrDefaultAsync(project => project.Id == projectId && !project.IsDeleted, cancellationToken);

    private async Task<bool> ProjectExistsAsync(Guid projectId, CancellationToken cancellationToken) =>
        await FindProjectInScopeAsync(projectId, cancellationToken) is not null;

    private async Task<DateTime> GetBusinessTodayAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var config = await _context.Set<ProjectReportingConfiguration>()
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.IsCurrent && x.Status == "Approved")
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        var timeZoneId = config?.TimeZoneId ?? "UTC";
        var timeZone = ResolveBusinessTimezone(timeZoneId);
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        return TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone).Date;
    }

    private static TimeZoneInfo ResolveBusinessTimezone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return TimeZoneInfo.Utc; }
    }

    private async Task LogAsync(ProjectStatusReport report, string action, string userId, CancellationToken cancellationToken)
    {
        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ProjectStatusReport", EntityId = report.Id, Action = action, UserId = ParseUserId(userId),
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime, CreatedBy = userId
        }, cancellationToken);
    }

    private static void ApplyDraftContent(ProjectStatusReport report, UpdateProjectStatusReportDto dto, DateTime automaticDate, string lifecycleStatusCode)
    {
        report.ReportDate = automaticDate.Date;
        report.PeriodFrom = dto.PeriodFrom;
        report.PeriodTo = dto.PeriodTo;
        report.StatusDate = automaticDate.Date;
        report.ProjectLifecycleStatusAtReport = lifecycleStatusCode;
        report.OverallHealth = dto.OverallHealth;
        report.OverallProgressPercent = dto.OverallProgressPercent;
        report.ScheduleHealth = null;
        report.CostHealth = dto.CostHealth;
        report.ScopeHealth = null;
        report.QualityHealth = dto.QualityHealth;
        // Resource utilization is server-derived from the canonical capacity service.
        report.ExecutiveSummary = dto.ExecutiveSummary;
        report.KeyAchievements = dto.KeyAchievements;
        report.ChallengesConcerns = dto.ChallengesConcerns;
        report.NextPeriodPriorities = dto.NextPeriodPriorities;
        report.ActionsNeeded = dto.ActionsNeeded;
        report.SupportNeeded = dto.SupportNeeded;
    }

    private async Task<Result<string>> ResolveReportLifecycleStatusAsync(
        Project project,
        string? requestedStatus,
        CancellationToken cancellationToken)
    {
        Guid? organizationId = null;
        if (_projectOrganizationLookup is not null)
        {
            organizationId = await _projectOrganizationLookup.ResolveOrganizationIdAsync(
                project.TenantId, project.Id, cancellationToken);
        }
        else if (_managementContextAccessor.Current?.Scope.ProjectOrganizationIds.TryGetValue(project.Id, out var scopedOrganizationId) == true
            && scopedOrganizationId != Guid.Empty)
        {
            organizationId = scopedOrganizationId;
        }

        var statuses = await _context.Set<ProjectStatusCatalogEntry>().AsNoTracking()
            .Where(item => item.TenantId == project.TenantId
                && !item.IsDeleted
                && (item.OrganizationId == null || item.OrganizationId == organizationId))
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);

        var currentStatus = project.ProjectStatusId.HasValue
            ? statuses.FirstOrDefault(item => item.Id == project.ProjectStatusId.Value)
            : null;
        if (string.IsNullOrWhiteSpace(requestedStatus) && project.ProjectStatusId.HasValue)
        {
            return currentStatus is null
                ? Result<string>.Failure(Error.Validation("The project's current status is unavailable in its lifecycle catalog scope."))
                : Result<string>.Success(currentStatus.Code);
        }

        var requestedCode = string.IsNullOrWhiteSpace(requestedStatus) ? project.Status : requestedStatus;
        var normalizedCode = requestedCode?.Trim().ToUpperInvariant();
        var selectedStatus = string.IsNullOrWhiteSpace(normalizedCode)
            ? null
            : statuses.Where(item => item.IsActive
                    && (string.Equals(item.NormalizedCode, normalizedCode, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Code, requestedCode?.Trim(), StringComparison.OrdinalIgnoreCase))
                )
                .OrderByDescending(item => item.OrganizationId.HasValue)
                .FirstOrDefault();

        if (selectedStatus is null && currentStatus is not null
            && (string.Equals(currentStatus.NormalizedCode, normalizedCode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentStatus.Code, requestedCode?.Trim(), StringComparison.OrdinalIgnoreCase)))
            selectedStatus = currentStatus;

        return selectedStatus is null
            ? Result<string>.Failure(Error.Validation("Select an active project status from the in-scope lifecycle catalog."))
            : Result<string>.Success(selectedStatus.Code);
    }

    private static bool HasDraftContentChanged(ProjectStatusReport report, UpdateProjectStatusReportDto dto, string lifecycleStatusCode) =>
        report.PeriodFrom.Date != dto.PeriodFrom.Date
        || report.PeriodTo.Date != dto.PeriodTo.Date
        || !string.Equals(report.ProjectLifecycleStatusAtReport, lifecycleStatusCode, StringComparison.Ordinal)
        || !string.Equals(report.OverallHealth, dto.OverallHealth, StringComparison.Ordinal)
        || report.OverallProgressPercent != dto.OverallProgressPercent
        || !string.Equals(report.CostHealth, dto.CostHealth, StringComparison.Ordinal)
        || !string.Equals(report.QualityHealth, dto.QualityHealth, StringComparison.Ordinal)
        || !string.Equals(report.ExecutiveSummary, dto.ExecutiveSummary, StringComparison.Ordinal)
        || !string.Equals(report.KeyAchievements, dto.KeyAchievements, StringComparison.Ordinal)
        || !string.Equals(report.ChallengesConcerns, dto.ChallengesConcerns, StringComparison.Ordinal)
        || !string.Equals(report.NextPeriodPriorities, dto.NextPeriodPriorities, StringComparison.Ordinal)
        || !string.Equals(report.ActionsNeeded, dto.ActionsNeeded, StringComparison.Ordinal)
        || !string.Equals(report.SupportNeeded, dto.SupportNeeded, StringComparison.Ordinal);

    private static string? UsableHealth(string? value) =>
        HealthValues.FirstOrDefault(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));

    private static Error? ValidateDraft(DateTime today, DateTime periodFrom, DateTime periodTo, decimal? progress)
    {
        if (periodFrom == default || periodTo == default) return Error.Validation("Reporting period is required");
        if (periodFrom.Date > periodTo.Date) return Error.Validation("Reporting period start must be before its end");
        if (periodTo.Date > today) return Error.Validation("Reporting period end must not be later than today");
        if (progress is < 0 or > 100) return Error.Validation("Overall progress must be between 0 and 100");
        return null;
    }

    private static Error? ValidateForPublish(ProjectStatusReport report, DateTime today)
    {
        var draftValidation = ValidateDraft(today, report.PeriodFrom, report.PeriodTo, report.OverallProgressPercent);
        if (draftValidation is not null) return draftValidation;
        var ragValues = new[] { report.OverallHealth, report.CostHealth, report.QualityHealth };
        if (ragValues.Any(value => string.IsNullOrWhiteSpace(value) || !HealthValues.Contains(value, StringComparer.OrdinalIgnoreCase)))
            return Error.Validation("Overall, cost, and quality health must be Green, Amber, or Red before publishing.");
        var utilizationValues = new[]
        {
            ProjectResourceUtilizationStatuses.Overallocated,
            ProjectResourceUtilizationStatuses.WithinCapacity,
            ProjectResourceUtilizationStatuses.NoAssignedWork,
            ProjectResourceUtilizationStatuses.Unavailable
        };
        if (string.IsNullOrWhiteSpace(report.ResourceHealth) || !utilizationValues.Contains(report.ResourceHealth, StringComparer.Ordinal))
            return Error.Validation("A resource utilization status is required before publishing the report.");
        if (string.IsNullOrWhiteSpace(report.ExecutiveSummary)) return Error.Validation("Executive summary is required before publishing the report.");
        return null;
    }

    private static ProjectStatusReportDto MapDetail(ProjectStatusReport x) => new()
    {
        Id = x.Id, ProjectId = x.ProjectId, ReportDate = x.ReportDate, PeriodFrom = x.PeriodFrom, PeriodTo = x.PeriodTo, StatusDate = x.StatusDate,
        ReportState = x.ReportState, OverallHealth = x.OverallHealth, OverallProgressPercent = x.OverallProgressPercent,
        PreparedByNameSnapshot = x.PreparedByNameSnapshot, PublishedAt = x.PublishedAt, ProjectNameSnapshot = x.ProjectNameSnapshot,
        ProjectCodeSnapshot = x.ProjectCodeSnapshot, ProjectManagerIdAtReport = x.ProjectManagerIdAtReport, ProjectManagerNameSnapshot = x.ProjectManagerNameSnapshot,
        ProjectLifecycleStatusAtReport = x.ProjectLifecycleStatusAtReport, ScheduleHealth = x.ScheduleHealth, CostHealth = x.CostHealth,
        ScopeHealth = x.ScopeHealth, QualityHealth = x.QualityHealth, ResourceHealth = x.ResourceHealth, ExecutiveSummary = x.ExecutiveSummary,
        KeyAchievements = x.KeyAchievements, ChallengesConcerns = x.ChallengesConcerns, NextPeriodPriorities = x.NextPeriodPriorities,
        ActionsNeeded = x.ActionsNeeded, SupportNeeded = x.SupportNeeded,
        PreparedByUserId = x.PreparedByUserId, PublishedByUserId = x.PublishedByUserId, PublishedByNameSnapshot = x.PublishedByNameSnapshot,
        SourceDataCapturedAt = x.SourceDataCapturedAt, ReportingConfigurationVersionSnapshot = x.ReportingConfigurationVersionSnapshot,
        IncludedSectionsSnapshot = DeserializePerformanceSnapshot(x.PerformanceSnapshotJson)?.IncludedReportSections ?? [],
        PerformanceSnapshot = DeserializePerformanceSnapshot(x.PerformanceSnapshotJson), CurrencyCodeSnapshot = x.CurrencyCodeSnapshot, BudgetAmountSnapshot = x.BudgetAmountSnapshot,
        ActualCostAmountSnapshot = x.ActualCostAmountSnapshot, CommittedCostAmountSnapshot = x.CommittedCostAmountSnapshot,
        ForecastCostAmountSnapshot = x.ForecastCostAmountSnapshot, CostVarianceAmountSnapshot = x.CostVarianceAmountSnapshot,
        ActualPaidSnapshot = x.ActualPaidSnapshot, OutstandingPlannedPaymentsSnapshot = x.OutstandingPlannedPaymentsSnapshot,
        PaymentsDueWithinPeriodSnapshot = x.PaymentsDueWithinPeriodSnapshot, RemainingUnpaidAtPeriodEndSnapshot = x.RemainingUnpaidAtPeriodEndSnapshot,
        ScheduleVarianceDaysSnapshot = x.ScheduleVarianceDaysSnapshot, ScheduleVariancePercentSnapshot = x.ScheduleVariancePercentSnapshot,
        BaselineIdAtReport = x.BaselineIdAtReport, BaselineNameSnapshot = x.BaselineNameSnapshot, BaselineVersionSnapshot = x.BaselineVersionSnapshot,
        Risks = x.RiskSnapshots.OrderBy(s => s.Rank).Select(s => new ProjectStatusReportRiskSnapshotDto
        {
            SourceRiskId = s.SourceRiskId, Rank = s.Rank, Title = s.Title, Description = s.Description, Impact = s.Impact, Likelihood = s.Likelihood,
            RiskLevel = s.RiskLevel, RiskStatus = s.RiskStatus, PotentialCost = s.PotentialCost, PotentialDelay = s.PotentialDelay,
            RiskResponse = s.RiskResponse, OwnerId = s.OwnerId, OwnerNameSnapshot = s.OwnerNameSnapshot
        }).ToList(),
        Issues = x.IssueSnapshots.OrderBy(s => s.Rank).Select(s => new ProjectStatusReportIssueSnapshotDto
        {
            SourceIssueId = s.SourceIssueId, Rank = s.Rank, Title = s.Title, Description = s.Description, Severity = s.Severity,
            Priority = s.Priority, IssueStatus = s.IssueStatus, AssigneeId = s.AssigneeId, AssigneeNameSnapshot = s.AssigneeNameSnapshot, Resolution = s.Resolution
        }).ToList(),
        ChangeRequests = x.ChangeRequestSnapshots.OrderBy(s => s.Rank).Select(s => new ProjectStatusReportChangeRequestSnapshotDto
        {
            SourceChangeRequestId = s.SourceChangeRequestId, Rank = s.Rank, Number = s.Number, ChangeType = s.ChangeType, Reason = s.Reason,
            Justification = s.Justification, Impact = s.Impact, ChangeRequestStatus = s.ChangeRequestStatus, RequestedBy = s.RequestedBy,
            RequestedByNameSnapshot = s.RequestedByNameSnapshot, RequestedAt = s.RequestedAt, ReviewedAt = s.ReviewedAt, DecisionNotes = s.DecisionNotes
        }).ToList(),
        RowVersion = x.RowVersion
    };

    private static ProjectPerformanceDto? DeserializePerformanceSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ProjectPerformanceDto>(json); }
        catch (JsonException) { return null; }
    }

    private static Guid? ParseUserId(string userId) => Guid.TryParse(userId, out var id) ? id : null;
    private static int RiskLevelRank(RiskLevel value) => value switch { RiskLevel.Critical => 4, RiskLevel.High => 3, RiskLevel.Medium => 2, _ => 1 };
    private static int RiskStatusRank(RiskStatus value) => value switch { RiskStatus.ActionRequired => 5, RiskStatus.Identified => 4, RiskStatus.FollowUp => 3, RiskStatus.Mitigated => 2, _ => 1 };
    private static int IssueSeverityRank(string value) => Rank(value, "Critical", "High", "Major", "Medium", "Minor");
    private static int IssuePriorityRank(string value) => Rank(value, "Critical", "High", "Medium", "Low");
    private static int IssueStatusRank(string value) => Rank(value, "Open", "In Progress", "InProgress", "Blocked", "Resolved", "Closed");
    private static int ChangeRequestStatusRank(ProjectChangeRequest.Statuses value) => value switch { ProjectChangeRequest.Statuses.Pending => 4, ProjectChangeRequest.Statuses.Approved => 3, ProjectChangeRequest.Statuses.Rejected => 2, _ => 1 };
    private static int Rank(string value, params string[] ordered) => Array.FindIndex(ordered, x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)) is var index && index >= 0 ? ordered.Length - index : 0;
}
