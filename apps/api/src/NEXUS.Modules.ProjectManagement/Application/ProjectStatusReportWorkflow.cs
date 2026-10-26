using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Application;

public static class ProjectStatusReportWorkflow
{
    public const string EntityType = "ProjectStatusReport";
    public const string DefinitionCode = "PROJECT_STATUS_REPORT_WEEKLY";
    public const string Generate = "GENERATE";
    public const string Submit = "SUBMIT";
    public const string Approve = "APPROVE";
    public const string Return = "RETURN";
    public const string Reopen = "REOPEN";
    public const string Publish = "PUBLISH";
    private static readonly (string Code, string From, string To)[] RequiredTransitions =
    [
        (Generate, "Draft", "Generated"),
        (Submit, "Generated", "Submitted"),
        (Approve, "Submitted", "Approved"),
        (Return, "Submitted", "Returned"),
        (Reopen, "Returned", "Draft"),
        (Publish, "Approved", "Published")
    ];

    public static bool IsExpectedTransition(string code, string from, string to) => RequiredTransitions.Any(item =>
        item.Code.Equals(code, StringComparison.OrdinalIgnoreCase)
        && item.From.Equals(from, StringComparison.OrdinalIgnoreCase)
        && item.To.Equals(to, StringComparison.OrdinalIgnoreCase));

    public static bool HasConfiguredSections(string? performanceSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(performanceSnapshotJson)) return false;
        try
        {
            using var document = JsonDocument.Parse(performanceSnapshotJson);
            var root = document.RootElement;
            if (!root.TryGetProperty(nameof(ProjectPerformanceDto.IncludedReportSections), out var sections)
                && !root.TryGetProperty("includedReportSections", out sections))
                return true; // A legacy snapshot predates section configuration and remains publishable.
            if (sections.ValueKind != JsonValueKind.Array) return false;
            var included = sections.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return included.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static Result ValidateWorkflowVersion(WorkflowDefinitionVersion version)
    {
        var requiredStates = new[] { "Draft", "Generated", "Submitted", "Approved", "Returned", "Published" };
        var activeStates = version.States.Where(item => item.IsActive && !item.IsDeleted).ToList();
        if (activeStates.Any(item => !requiredStates.Contains(item.Code, StringComparer.OrdinalIgnoreCase))
            || requiredStates.Any(expected => activeStates.Count(item => item.Code.Equals(expected, StringComparison.OrdinalIgnoreCase)) != 1))
            return Result.Failure(Error.Validation("The status-report workflow must contain each supported state exactly once."));
        if (activeStates.Count(item => item.IsInitial) != 1
            || activeStates.Count(item => item.IsInitial && item.Code.Equals("Draft", StringComparison.OrdinalIgnoreCase)) != 1)
            return Result.Failure(Error.Validation("The status-report workflow must have exactly one initial Draft state."));
        if (activeStates.Count(item => item.IsFinal) != 1
            || activeStates.Count(item => item.IsFinal && item.Code.Equals("Published", StringComparison.OrdinalIgnoreCase)) != 1)
            return Result.Failure(Error.Validation("Published must be the only final status-report workflow state."));

        var activeTransitions = version.Transitions.Where(item => item.IsActive && !item.IsDeleted).ToList();
        if (activeTransitions.Count != RequiredTransitions.Length || RequiredTransitions.Any(expected =>
            activeTransitions.Count(item => item.Code.Equals(expected.Code, StringComparison.OrdinalIgnoreCase)
                && item.FromState.Code.Equals(expected.From, StringComparison.OrdinalIgnoreCase)
                && item.ToState.Code.Equals(expected.To, StringComparison.OrdinalIgnoreCase)) != 1))
            return Result.Failure(Error.Validation("The status-report workflow must contain only the six required lifecycle transitions."));
        return Result.Success();
    }
}

public sealed record ProjectStatusReportWorkflowDefinition(Guid DefinitionId, Guid VersionId);

public interface IProjectStatusReportWorkflowProvisioningService
{
    Task<Result<ProjectStatusReportWorkflowDefinition>> EnsureAsync(Guid tenantId, string actor, CancellationToken cancellationToken = default);
}

public sealed class ProjectStatusReportWorkflowProvisioningService(DbContext context) : IProjectStatusReportWorkflowProvisioningService
{
    private static readonly (string Code, string NameEn, string NameAr, bool IsFinal)[] States =
    [
        ("Draft", "Draft", "مسودة", false),
        ("Generated", "Generated", "تم الإنشاء", false),
        ("Submitted", "Submitted", "مُرسل للمراجعة", false),
        ("Approved", "Approved", "معتمد", false),
        ("Returned", "Returned", "مُعاد للتعديل", false),
        ("Published", "Published", "منشور", true)
    ];

    private static readonly (string Code, string From, string To, string NameEn, string NameAr)[] Transitions =
    [
        (ProjectStatusReportWorkflow.Generate, "Draft", "Generated", "Generate snapshot", "إنشاء اللقطة"),
        (ProjectStatusReportWorkflow.Submit, "Generated", "Submitted", "Submit for review", "إرسال للمراجعة"),
        (ProjectStatusReportWorkflow.Approve, "Submitted", "Approved", "Approve report", "اعتماد التقرير"),
        (ProjectStatusReportWorkflow.Return, "Submitted", "Returned", "Return for revision", "إعادة للتعديل"),
        (ProjectStatusReportWorkflow.Reopen, "Returned", "Draft", "Reopen draft", "إعادة فتح المسودة"),
        (ProjectStatusReportWorkflow.Publish, "Approved", "Published", "Publish report", "نشر التقرير")
    ];

    public async Task<Result<ProjectStatusReportWorkflowDefinition>> EnsureAsync(Guid tenantId, string actor, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return Result<ProjectStatusReportWorkflowDefinition>.Failure(Error.Validation("A valid tenant is required to provision the status-report workflow."));
        var definition = await context.Set<WorkflowDefinition>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.EntityType == ProjectStatusReportWorkflow.EntityType
                && item.Code == ProjectStatusReportWorkflow.DefinitionCode && !item.IsDeleted, cancellationToken);
        if (definition is { IsActive: false })
            return Result<ProjectStatusReportWorkflowDefinition>.Failure(Error.Conflict("The tenant's project status-report workflow is disabled."));

        if (definition?.ActivePublishedVersionId is Guid activeVersionId)
        {
            var activeVersion = await context.Set<WorkflowDefinitionVersion>()
                .Include(item => item.States).Include(item => item.Transitions).ThenInclude(item => item.FromState)
                .Include(item => item.Transitions).ThenInclude(item => item.ToState)
                .FirstOrDefaultAsync(item => item.Id == activeVersionId && item.WorkflowDefinitionId == definition.Id
                    && item.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Published && !item.IsDeleted, cancellationToken);
            if (activeVersion is not null)
            {
                var validation = ValidateVersion(activeVersion);
                return validation.IsSuccess
                    ? Result<ProjectStatusReportWorkflowDefinition>.Success(new(definition.Id, activeVersion.Id))
                    : Result<ProjectStatusReportWorkflowDefinition>.Failure(validation.Error!);
            }
        }

        List<int> existingVersionNumbers = definition is null ? new() : await context.Set<WorkflowDefinitionVersion>()
            .Where(item => item.WorkflowDefinitionId == definition.Id && !item.IsDeleted)
            .Select(item => item.VersionNumber).ToListAsync(cancellationToken);
        var version = new WorkflowDefinitionVersion
        {
            TenantId = tenantId,
            VersionNumber = existingVersionNumbers.DefaultIfEmpty(0).Max() + 1,
            LifecycleStatus = WorkflowDefinitionVersionLifecycleStatus.Published,
            ChangeLog = "Provisioned canonical weekly project status-report workflow.",
            PublishedAt = DateTime.UtcNow,
            PublishedBy = actor,
            CreatedBy = actor
        };
        var stateByCode = new Dictionary<string, WorkflowState>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < States.Length; index++)
        {
            var item = States[index];
            var state = new WorkflowState
            {
                TenantId = tenantId, WorkflowDefinitionVersionId = version.Id, Code = item.Code,
                NameEn = item.NameEn, NameAr = item.NameAr, DisplayOrder = index + 1,
                IsInitial = item.Code == "Draft", IsFinal = item.IsFinal, IsActive = true, CreatedBy = actor
            };
            version.States.Add(state);
            stateByCode.Add(state.Code, state);
        }
        foreach (var item in Transitions)
        {
            var transition = new WorkflowTransition
            {
                TenantId = tenantId, WorkflowDefinitionVersionId = version.Id, Code = item.Code,
                NameEn = item.NameEn, NameAr = item.NameAr,
                FromStateId = stateByCode[item.From].Id, ToStateId = stateByCode[item.To].Id,
                DisplayOrder = Array.IndexOf(Transitions, item) + 1, IsActive = true, CreatedBy = actor
            };
            transition.Actions.Add(new WorkflowTransitionAction
            {
                TenantId = tenantId, WorkflowTransitionId = transition.Id, ActionType = "DomainProjection",
                ConfigurationJson = "{}", ExecutionOrder = 10, IsActive = true, CreatedBy = actor
            });
            version.Transitions.Add(transition);
        }

        definition ??= new WorkflowDefinition
        {
            TenantId = tenantId, Code = ProjectStatusReportWorkflow.DefinitionCode,
            Name = "Weekly Project Status Report", Description = "Canonical report generation, review, approval, and publication lifecycle.",
            Domain = WorkflowDomains.Reporting, EntityType = ProjectStatusReportWorkflow.EntityType,
            IsActive = true, CreatedBy = actor
        };
        definition.ActivePublishedVersionId = null;
        definition.ActivePublishedVersion = null;
        definition.Versions.Add(version);
        if (context.Entry(definition).State == EntityState.Detached) context.Set<WorkflowDefinition>().Add(definition);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            definition.ActivePublishedVersionId = version.Id;
            definition.ActivePublishedVersion = version;
            definition.UpdatedAt = DateTime.UtcNow;
            definition.UpdatedBy = actor;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        return Result<ProjectStatusReportWorkflowDefinition>.Success(new(definition.Id, version.Id));
    }

    private static Result ValidateVersion(WorkflowDefinitionVersion version)
        => ProjectStatusReportWorkflow.ValidateWorkflowVersion(version);
}

public sealed class ProjectStatusReportWorkflowAdapter(
    DbContext context,
    IHttpContextAccessor httpContextAccessor,
    IManagementContextAccessor managementContext) : IWorkflowEntityAdapter
{
    public string EntityType => ProjectStatusReportWorkflow.EntityType;

    public async Task<Result<WorkflowEntitySnapshot>> GetSnapshotAsync(Guid entityId, WorkflowExecutionContext execution, CancellationToken cancellationToken = default)
    {
        var report = await FindAccessibleAsync(entityId, execution, cancellationToken);
        if (report is null) return Result<WorkflowEntitySnapshot>.Failure(WorkflowErrors.EntityNotAccessible(EntityType, entityId));
        return Result<WorkflowEntitySnapshot>.Success(new WorkflowEntitySnapshot(EntityType, entityId, new Dictionary<string, object?>
        {
            ["StateCode"] = report.ReportState.ToString(),
            ["StateNameEn"] = report.ReportState.ToString(),
            ["StateNameAr"] = report.ReportState.ToString(),
            ["ProjectId"] = report.ProjectId,
            ["PreparedByUserId"] = report.PreparedByUserId,
            ["HasGeneratedSnapshot"] = !string.IsNullOrWhiteSpace(report.PerformanceSnapshotJson),
            ["HasConfiguredSections"] = ProjectStatusReportWorkflow.HasConfiguredSections(report.PerformanceSnapshotJson),
            ["StatusDate"] = report.StatusDate
        }));
    }

    public async Task<Result> AuthorizeAsync(WorkflowExecutionContext execution, Guid entityId, CancellationToken cancellationToken = default)
    {
        if (!HasPermission("StatusReport.View")) return Result.Failure(Error.Forbidden("Status report access is required."));
        return await FindAccessibleAsync(entityId, execution, cancellationToken) is null
            ? Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, entityId))
            : Result.Success();
    }

    public Task<Result> ValidateTransitionAsync(WorkflowTransitionContext transition, CancellationToken cancellationToken = default)
    {
        var target = transition.ToState.Code;
        if (!ProjectStatusReportWorkflow.IsExpectedTransition(transition.Transition.Code, transition.FromState.Code, target))
            return Task.FromResult(Result.Failure(Error.Forbidden("This transition is not part of the approved status-report lifecycle.")));
        var permission = target.ToUpperInvariant() switch
        {
            "GENERATED" => "StatusReport.Generate",
            "SUBMITTED" => "StatusReport.Submit",
            "APPROVED" or "RETURNED" => "StatusReport.Approve",
            "PUBLISHED" => "StatusReport.Publish",
            "DRAFT" => "StatusReport.Edit",
            _ => null
        };
        if (permission is null || !HasPermission(permission))
            return Task.FromResult(Result.Failure(Error.Forbidden("You do not have permission for this status-report workflow transition.")));
        var requiresSnapshot = target.Equals("Generated", StringComparison.OrdinalIgnoreCase)
            || target.Equals("Submitted", StringComparison.OrdinalIgnoreCase)
            || target.Equals("Approved", StringComparison.OrdinalIgnoreCase)
            || target.Equals("Returned", StringComparison.OrdinalIgnoreCase)
            || target.Equals("Published", StringComparison.OrdinalIgnoreCase);
        if (requiresSnapshot && (!transition.Entity.Facts.TryGetValue("HasGeneratedSnapshot", out var hasSnapshot) || hasSnapshot is not true))
            return Task.FromResult(Result.Failure(Error.Conflict("A report snapshot must be generated before continuing this workflow.")));
        if (target.Equals("Published", StringComparison.OrdinalIgnoreCase)
            && (!transition.Entity.Facts.TryGetValue("HasConfiguredSections", out var hasConfiguredSections) || hasConfiguredSections is not true))
            return Task.FromResult(Result.Failure(Error.Conflict("At least one configured report section is required before publication.")));
        if (target.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        {
            if (!transition.Entity.Facts.TryGetValue("PreparedByUserId", out var author)
                || author is not Guid authorId
                || !Guid.TryParse(transition.Execution.UserId, out var reviewerId))
                return Task.FromResult(Result.Failure(Error.Forbidden("The report author and reviewer must be identified before approval.")));
            if (authorId == reviewerId)
                return Task.FromResult(Result.Failure(Error.Forbidden("A report author cannot approve their own report.")));
        }
        return Task.FromResult(Result.Success());
    }

    public async Task<Result> ApplyTransitionAsync(WorkflowTransitionContext transition, CancellationToken cancellationToken = default)
    {
        var report = await FindAccessibleAsync(transition.EntityId, transition.Execution, cancellationToken, tracked: true);
        if (report is null) return Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, transition.EntityId));
        if (!string.Equals(report.ReportState.ToString(), transition.FromState.Code, StringComparison.OrdinalIgnoreCase))
            return Result.Failure(WorkflowErrors.WorkflowDomainStateMismatch(transition.FromState.Code, report.ReportState.ToString()));
        if (!Enum.TryParse<ProjectStatusReportState>(transition.ToState.Code, true, out var newState))
            return Result.Failure(Error.Validation("The target state is not supported by the status-report domain."));

        if (newState == ProjectStatusReportState.Published && string.IsNullOrWhiteSpace(report.PerformanceSnapshotJson))
            return Result.Failure(Error.Conflict("Generate and freeze the report snapshot before publishing."));
        if (newState == ProjectStatusReportState.Published && !ProjectStatusReportWorkflow.HasConfiguredSections(report.PerformanceSnapshotJson))
            return Result.Failure(Error.Conflict("At least one configured report section is required before publication."));
        if (newState == ProjectStatusReportState.Draft && report.ReportState == ProjectStatusReportState.Returned)
            await ClearGeneratedSnapshotAsync(report, cancellationToken);
        report.ReportState = newState;
        report.UpdatedAt = DateTime.UtcNow;
        report.UpdatedBy = transition.Execution.UserId;
        if (newState == ProjectStatusReportState.Published)
        {
            report.PublishedAt = DateTime.UtcNow;
            report.PublishedByUserId = Guid.TryParse(transition.Execution.UserId, out var publisherId) ? publisherId : null;
            report.PublishedByNameSnapshot = transition.Execution.UserName;
        }
        if (newState == ProjectStatusReportState.Draft)
        {
            report.PublishedAt = null;
            report.PublishedByUserId = null;
            report.PublishedByNameSnapshot = null;
        }
        return Result.Success();
    }

    private async Task ClearGeneratedSnapshotAsync(ProjectStatusReport report, CancellationToken cancellationToken)
    {
        report.PerformanceSnapshotJson = null;
        report.ReportingConfigurationVersionSnapshot = null;
        report.SourceDataCapturedAt = null;
        report.ProjectNameSnapshot = null;
        report.ProjectCodeSnapshot = null;
        report.CurrencyCodeSnapshot = null;
        report.BudgetAmountSnapshot = null;
        report.ActualCostAmountSnapshot = null;
        report.CommittedCostAmountSnapshot = null;
        report.ForecastCostAmountSnapshot = null;
        report.CostVarianceAmountSnapshot = null;
        report.ScheduleVarianceDaysSnapshot = null;
        report.ScheduleVariancePercentSnapshot = null;
        report.BaselineIdAtReport = null;
        report.BaselineNameSnapshot = null;
        report.BaselineVersionSnapshot = null;
        context.Set<ProjectStatusReportRiskSnapshot>().RemoveRange(await context.Set<ProjectStatusReportRiskSnapshot>().Where(item => item.ProjectStatusReportId == report.Id).ToListAsync(cancellationToken));
        context.Set<ProjectStatusReportIssueSnapshot>().RemoveRange(await context.Set<ProjectStatusReportIssueSnapshot>().Where(item => item.ProjectStatusReportId == report.Id).ToListAsync(cancellationToken));
        context.Set<ProjectStatusReportChangeRequestSnapshot>().RemoveRange(await context.Set<ProjectStatusReportChangeRequestSnapshot>().Where(item => item.ProjectStatusReportId == report.Id).ToListAsync(cancellationToken));
    }

    private async Task<ProjectStatusReport?> FindAccessibleAsync(Guid id, WorkflowExecutionContext execution, CancellationToken cancellationToken, bool tracked = false)
    {
        var query = context.Set<ProjectStatusReport>().AsQueryable();
        if (!tracked) query = query.AsNoTracking();
        var report = await query.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted
            && item.Project.TenantId == execution.TenantId && !item.Project.IsDeleted, cancellationToken);
        if (report is null || execution.TenantId == Guid.Empty || report.TenantId != execution.TenantId) return null;
        var management = managementContext.Current;
        if (management is null || management.TenantId != report.TenantId) return null;
        return management.Scope.ProjectIds.Contains(report.ProjectId) ? report : null;
    }

    private bool HasPermission(string permission)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true
            && principal.HasClaim("permission", permission);
    }
}

public sealed class ProjectStatusReportWorkflowMetadataProvider : IWorkflowEntityMetadataProvider
{
    public string EntityType => ProjectStatusReportWorkflow.EntityType;
    public WorkflowEntityTypeMetadata GetMetadata() => new(WorkflowDomains.Reporting, EntityType, "Project Status Reports", "تقارير حالة المشروع", new[]
    {
        new WorkflowFactMetadata("ProjectId", "Project", "المشروع", "string"),
        new WorkflowFactMetadata("PreparedByUserId", "Prepared by", "أعده", "string"),
        new WorkflowFactMetadata("HasGeneratedSnapshot", "Has generated snapshot", "توجد لقطة منشأة", "boolean"),
        new WorkflowFactMetadata("StatusDate", "Status date", "تاريخ الحالة", "date")
    });
}

public sealed class ProjectStatusReportWorkflowConsumerValidator : IWorkflowDefinitionConsumerValidator
{
    public string EntityType => ProjectStatusReportWorkflow.EntityType;
    public Task<Result> ValidateAsync(WorkflowDefinitionVersion version, CancellationToken cancellationToken = default)
        => Task.FromResult(ProjectStatusReportWorkflow.ValidateWorkflowVersion(version));
}
