using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.RiskManagement.Application;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.WorkItems.Application;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public sealed class ProjectOverviewService : IProjectOverviewService
{
    private readonly IProjectService _projectService;
    private readonly IProgramService _programService;
    private readonly IProjectStatusReportService _statusReportService;
    private readonly IRiskService _riskService;
    private readonly IWorkItemService _workItemService;
    private readonly IMilestoneService _milestoneService;
    private readonly IBaselineService _baselineService;
    private readonly IProjectChangeRequestService _changeRequestService;
    private readonly IProjectHandoverRequestService _handoverService;
    private readonly IAllocationService _allocationService;
    private readonly IMeetingService _meetingService;
    private readonly IProjectPlanProgressLookup _planProgressLookup;
    private readonly IProjectFinancialLookup _financialLookup;

    public ProjectOverviewService(
        IProjectService projectService,
        IProgramService programService,
        IProjectStatusReportService statusReportService,
        IRiskService riskService,
        IWorkItemService workItemService,
        IMilestoneService milestoneService,
        IBaselineService baselineService,
        IProjectChangeRequestService changeRequestService,
        IProjectHandoverRequestService handoverService,
        IAllocationService allocationService,
        IMeetingService meetingService,
        IProjectPlanProgressLookup planProgressLookup,
        IProjectFinancialLookup financialLookup)
    {
        _projectService = projectService;
        _programService = programService;
        _statusReportService = statusReportService;
        _riskService = riskService;
        _workItemService = workItemService;
        _milestoneService = milestoneService;
        _baselineService = baselineService;
        _changeRequestService = changeRequestService;
        _handoverService = handoverService;
        _allocationService = allocationService;
        _meetingService = meetingService;
        _planProgressLookup = planProgressLookup;
        _financialLookup = financialLookup;
    }

    public async Task<Result<ProjectOverviewDto>> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var projectResult = await _projectService.GetShellAsync(projectId, cancellationToken);
        if (projectResult.IsFailure || projectResult.Value is null)
        {
            return Result<ProjectOverviewDto>.Failure(projectResult.Error ?? Error.NotFound("Project not found"));
        }

        var project = projectResult.Value;
        var statusReports = await _statusReportService.GetOverviewAsync(projectId, cancellationToken);
        if (statusReports.IsFailure || statusReports.Value is null) return Result<ProjectOverviewDto>.Failure(statusReports.Error!);

        var risks = await _riskService.GetProjectOverviewAsync(projectId, cancellationToken: cancellationToken);
        var issues = await _workItemService.GetProjectOverviewAsync(projectId, cancellationToken: cancellationToken);
        var milestones = await _milestoneService.GetByProjectAsync(projectId, 1, 50, cancellationToken);
        var baselines = await _baselineService.GetByProjectAsync(projectId, 1, 50, cancellationToken);
        var changes = await _changeRequestService.GetByProjectIdAsync(projectId, cancellationToken);
        var handovers = await _handoverService.GetForProjectAsync(projectId, cancellationToken);
        var allocations = await _allocationService.GetProjectOverviewAsync(projectId, cancellationToken);
        var meetings = await _meetingService.GetProjectOverviewAsync(projectId, cancellationToken);
        var planProgress = await _planProgressLookup.GetAsync(projectId, cancellationToken);
        var financialSummary = await _financialLookup.GetAsync(projectId, project.Currency, cancellationToken);

        var pv = project.PlannedValue;
        var ev = project.EarnedValue;

        // Cost Management is the sole authoritative source for AC.
        // No fallback to legacy Project.ActualCost — null means unavailable.
        var ac = financialSummary?.HasCostEntryData == true ? financialSummary.ActualCost : null;

        var sv = (ev.HasValue && pv.HasValue) ? ev - pv : null;
        var cv = (ev.HasValue && ac.HasValue) ? ev - ac : null;
        var spi = (ev.HasValue && pv.HasValue && pv.Value != 0m) ? ev / pv : null;
        var cpi = (ev.HasValue && ac.HasValue && ac.Value != 0m) ? ev / ac : null;

        // Project Management owns the ceiling; Cost Management supplies allocations and costs.
        var budget = project.Budget;

        var program = project.ProgramId.HasValue
            ? await _programService.GetByIdAsync(project.ProgramId.Value, cancellationToken)
            : null;

        var currentBaseline = baselines.Items
            .Where(baseline => baseline.IsCurrent)
            .OrderByDescending(baseline => baseline.Version)
            .ThenByDescending(baseline => baseline.BaselineDate)
            .Select(baseline => new ProjectOverviewBaselineDto
            {
                Id = baseline.Id,
                Name = baseline.Name,
                Version = baseline.Version,
                BaselineDate = baseline.BaselineDate,
                IsCurrent = baseline.IsCurrent
            })
            .FirstOrDefault();

        return Result<ProjectOverviewDto>.Success(new ProjectOverviewDto
        {
            Program = program?.IsSuccess == true && program.Value is not null
                ? new ProjectOverviewProgramDto { Id = program.Value.Id, Name = program.Value.Name, Code = program.Value.Code }
                : null,
            StatusReports = statusReports.Value,
            Risks = risks,
            Issues = issues,
            Milestones = milestones.Items
                .OrderBy(milestone => IsCompleted(milestone) ? 1 : 0)
                .ThenBy(milestone => milestone.DueDate.HasValue ? 0 : 1)
                .ThenBy(milestone => milestone.DueDate)
                .ThenBy(milestone => milestone.Name)
                .Take(8)
                .Select(milestone => new ProjectOverviewMilestoneDto
                {
                    Id = milestone.Id,
                    Name = milestone.Name,
                    DueDate = milestone.DueDate,
                    CompletedDate = milestone.CompletedDate,
                    Status = milestone.Status,
                    Progress = milestone.Progress
                })
                .ToList(),
            CurrentBaseline = currentBaseline,
            PendingChangeRequestCount = changes.IsSuccess && changes.Value is not null
                ? changes.Value.Count(request => request.Status == ProjectChangeRequest.Statuses.Pending)
                : 0,
            PendingHandoverCount = handovers.IsSuccess && handovers.Value is not null
                ? handovers.Value.Count(request => request.Status == ProjectHandoverRequest.Statuses.Pending)
                : 0,
            Allocations = allocations,
            Meetings = meetings,
            PlanProgress = planProgress,
            PlannedValue = pv,
            EarnedValue = ev,
            ActualCost = ac,
            Currency = project.Currency,
            ScheduleVariance = sv,
            CostVariance = cv,
            SchedulePerformanceIndex = spi,
            CostPerformanceIndex = cpi,
            ApprovedBudget = budget,
            CurrentApprovedProjectBudget = budget,
            AllocatedBudget = financialSummary?.AllocatedBudget ?? 0,
            UnallocatedBudget = budget - (financialSummary?.AllocatedBudget ?? 0),
            OriginalApprovedBudget = financialSummary?.OriginalApprovedBudget,
            RevisedBudget = financialSummary?.RevisedBudget,
            CommittedCost = financialSummary?.CommittedCost,
            ForecastAtCompletion = financialSummary?.ForecastAtCompletion,
            RemainingBudget = financialSummary?.RemainingBudget,
            BudgetUtilizationPercentage = financialSummary?.BudgetUtilizationPercentage,
            ForecastVariance = financialSummary?.ForecastVariance,
            HasBudgetData = financialSummary?.HasBudgetData == true,
            HasCostEntryData = financialSummary?.HasCostEntryData == true,
            HasCommitmentData = financialSummary?.HasCommitmentData == true,
            HasCostManagementData = financialSummary?.HasFinancialData == true,
            HasPendingRevision = financialSummary?.HasPendingRevision == true
        });
    }

    private static bool IsCompleted(Milestone milestone)
    {
        if (milestone.CompletedDate.HasValue) return true;
        if (milestone.Progress.HasValue && milestone.Progress >= 100) return true;
        var normalized = (milestone.Status ?? "").Replace(" ", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
        return normalized is "complete" or "completed" or "closed" or "done";
    }
}
