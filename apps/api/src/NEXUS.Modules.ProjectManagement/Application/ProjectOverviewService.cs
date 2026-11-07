using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.RiskManagement.Application;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.WorkItems.Application;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public sealed class ProjectOverviewService : IProjectOverviewService
{
    private readonly DbContext? _context;
    private readonly IProjectService _projectService;
    private readonly IProgramService _programService;
    private readonly IProjectStatusReportService _statusReportService;
    private readonly IRiskService _riskService;
    private readonly IWorkItemService _workItemService;
    private readonly IProjectChangeRequestService _changeRequestService;
    private readonly IProjectHandoverRequestService _handoverService;
    private readonly IAllocationService _allocationService;
    private readonly ICapacityService _capacityService;
    private readonly IMeetingService _meetingService;
    private readonly IProjectPlanProgressLookup _planProgressLookup;
    private readonly IProjectPlanMilestoneLookup _planMilestoneLookup;
    private readonly IProjectFinancialLookup _financialLookup;
    private readonly IProjectEvmPlanLookup _evmPlanLookup;
    private readonly IProjectEvmLaborCostLookup _evmLaborCostLookup;
    private readonly IProjectEvmDirectCostLookup _evmDirectCostLookup;

    public ProjectOverviewService(
        IProjectService projectService,
        IProgramService programService,
        IProjectStatusReportService statusReportService,
        IRiskService riskService,
        IWorkItemService workItemService,
        IProjectChangeRequestService changeRequestService,
        IProjectHandoverRequestService handoverService,
        IAllocationService allocationService,
        IMeetingService meetingService,
        IProjectPlanProgressLookup planProgressLookup,
        IProjectPlanMilestoneLookup planMilestoneLookup,
        IProjectFinancialLookup financialLookup,
        IProjectEvmPlanLookup evmPlanLookup,
        IProjectEvmLaborCostLookup evmLaborCostLookup,
        IProjectEvmDirectCostLookup evmDirectCostLookup,
        ICapacityService capacityService,
        DbContext? context = null)
    {
        _context = context;
        _projectService = projectService;
        _programService = programService;
        _statusReportService = statusReportService;
        _riskService = riskService;
        _workItemService = workItemService;
        _changeRequestService = changeRequestService;
        _handoverService = handoverService;
        _allocationService = allocationService;
        _meetingService = meetingService;
        _planProgressLookup = planProgressLookup;
        _planMilestoneLookup = planMilestoneLookup;
        _financialLookup = financialLookup;
        _evmPlanLookup = evmPlanLookup;
        _evmLaborCostLookup = evmLaborCostLookup;
        _evmDirectCostLookup = evmDirectCostLookup;
        _capacityService = capacityService;
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
        var statusDate = DateTime.UtcNow.Date;
        var planMilestones = await _planMilestoneLookup.GetUpcomingAsync(projectId, statusDate, cancellationToken);
        var changes = await _changeRequestService.GetByProjectIdAsync(projectId, cancellationToken);
        var handoverCount = await _handoverService.GetPendingCountForProjectAsync(projectId, cancellationToken);
        var allocations = await _allocationService.GetProjectOverviewAsync(projectId, cancellationToken);
        var meetings = await _meetingService.GetProjectOverviewAsync(projectId, cancellationToken);
        var planProgress = await _planProgressLookup.GetAsync(projectId, cancellationToken);
        var financialSummary = await _financialLookup.GetAsync(projectId, project.Currency, cancellationToken);
        var evmPlan = await _evmPlanLookup.GetAsync(projectId, project.TenantId, cancellationToken);
        var evmLabor = await _evmLaborCostLookup.GetAsync(
            project.TenantId, projectId, project.Currency, statusDate, evmPlan.Assignments, cancellationToken);
        var evmDirectCosts = await _evmDirectCostLookup.GetAsync(
            project.TenantId, projectId, project.Currency, statusDate, cancellationToken);

        var pv = evmLabor.PlannedValue;
        var ev = evmLabor.EarnedValue;
        string? actualCostReason = null;
        decimal? ac = null;
        var laborCostIsIncomplete = evmLabor.HasActualLaborData && !evmLabor.ActualLaborCost.HasValue;
        if (evmDirectCosts.HasCurrencyMismatch)
        {
            actualCostReason = "CURRENCY_MISMATCH";
        }
        else if (laborCostIsIncomplete)
        {
            actualCostReason = evmLabor.ActualLaborCostUnavailableReason ?? "EVM_DATA_INCOMPLETE";
        }
        else if (evmLabor.HasActualLaborData || evmDirectCosts.HasData)
        {
            ac = (evmLabor.ActualLaborCost ?? 0m) + (evmDirectCosts.HasData ? evmDirectCosts.Amount : 0m);
        }
        else
        {
            actualCostReason = "NO_ACTUAL_COST_DATA";
        }

        var pvReason = evmPlan.BaselineId.HasValue ? evmLabor.PlannedValueUnavailableReason : "NO_APPROVED_BASELINE";
        var evReason = evmPlan.BaselineId.HasValue ? evmLabor.EarnedValueUnavailableReason : "NO_APPROVED_BASELINE";

        var costComparisonReason = evmDirectCosts.HasData && ac.HasValue
            ? "NON_LABOR_COSTS_NOT_BASELINED"
            : actualCostReason;
        var metrics = ProjectEvmCalculator.Calculate(pv, ev, ac, pvReason, evReason, actualCostReason, costComparisonReason);

        // Project Management owns the ceiling; Cost Management supplies allocations and costs.
        var budget = project.Budget;

        var program = project.ProgramId.HasValue
            ? await _programService.GetByIdAsync(project.ProgramId.Value, cancellationToken)
            : null;

        var currentBaseline = evmPlan.BaselineId.HasValue
            ? new ProjectOverviewBaselineDto
            {
                Id = evmPlan.BaselineId.Value,
                Name = evmPlan.BaselineName ?? string.Empty,
                Version = evmPlan.BaselineVersion ?? 0,
                BaselineDate = evmPlan.BaselineDate,
                IsCurrent = true
            }
            : null;

        return Result<ProjectOverviewDto>.Success(new ProjectOverviewDto
        {
            Program = program?.IsSuccess == true && program.Value is not null
                ? new ProjectOverviewProgramDto { Id = program.Value.Id, Name = program.Value.Name, Code = program.Value.Code }
                : null,
            StatusReports = statusReports.Value,
            Risks = risks,
            Issues = issues,
            Milestones = planMilestones
                .Select(milestone => new ProjectOverviewMilestoneDto
                {
                    Id = milestone.Id,
                    Name = milestone.Name,
                    DueDate = milestone.DueDate,
                    CompletedDate = milestone.CompletedDate,
                    Status = null,
                    Progress = milestone.Progress
                })
                .ToList(),
            CurrentBaseline = currentBaseline,
            PendingChangeRequestCount = changes.IsSuccess && changes.Value is not null
                ? changes.Value.Count(request => request.Status == ProjectChangeRequest.Statuses.Pending)
                : null,
            PendingHandoverCount = handoverCount.IsSuccess
                ? handoverCount.Value
                : null,
            Allocations = allocations,
            Meetings = meetings,
            PlanProgress = planProgress,
            StatusDate = statusDate,
            BudgetAtCompletion = evmLabor.BudgetAtCompletion,
            PlannedValue = pv,
            EarnedValue = ev,
            ActualCost = ac,
            Currency = project.Currency,
            ScheduleVariance = metrics.ScheduleVariance,
            CostVariance = metrics.CostVariance,
            SchedulePerformanceIndex = metrics.SchedulePerformanceIndex,
            CostPerformanceIndex = metrics.CostPerformanceIndex,
            PlannedValueUnavailableReason = pvReason,
            EarnedValueUnavailableReason = evReason,
            ActualCostUnavailableReason = actualCostReason,
            SchedulePerformanceIndexUnavailableReason = metrics.SchedulePerformanceIndexUnavailableReason,
            CostPerformanceIndexUnavailableReason = metrics.CostPerformanceIndexUnavailableReason,
            CostVarianceUnavailableReason = metrics.CostVarianceUnavailableReason,
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

    public async Task<Result<ProjectTeamCapacityOverviewDto>> GetTeamCapacityAsync(
        Guid projectId,
        DateTime startDate,
        DateTime endDate,
        string periodType,
        CancellationToken cancellationToken = default)
    {
        var projectResult = await _projectService.GetShellAsync(projectId, cancellationToken);
        if (projectResult.IsFailure || projectResult.Value is null)
            return Result<ProjectTeamCapacityOverviewDto>.Failure(projectResult.Error ?? Error.NotFound("Project not found"));

        var teamResult = await _projectService.GetTeamAsync(projectId, cancellationToken);
        if (teamResult.IsFailure || teamResult.Value is null)
            return Result<ProjectTeamCapacityOverviewDto>.Failure(teamResult.Error ?? Error.NotFound("Project team not found"));

        var periodStart = startDate.Date;
        var periodEnd = endDate.Date;
        var candidates = teamResult.Value
            .Where(member => member.ResourceId.HasValue
                && string.Equals(member.Status, "Active", StringComparison.OrdinalIgnoreCase)
                && member.StartDate.Date < periodEnd && member.EndDate.Date >= periodStart)
            .Select(member => new ProjectTeamCapacityMemberInputDto
            {
                ResourceId = member.ResourceId!.Value,
                ProjectId = projectId,
                StartDate = member.StartDate.Date,
                EndDate = member.EndDate.Date,
                AllocationPercentage = member.AllocationPercentage,
                HoursPerWeek = null,
                UseProjectCalendar = string.Equals(member.CalendarPolicy, CalendarPolicyConstants.FollowProjectCalendar, StringComparison.Ordinal),
                ProjectCalendarId = projectResult.Value.WorkingCalendarId
            })
            .ToList();

        var groupAssignments = teamResult.Value
            .Where(member => member.TeamId.HasValue
                && string.Equals(member.Status, "Active", StringComparison.OrdinalIgnoreCase)
                && member.StartDate.Date < periodEnd && member.EndDate.Date >= periodStart)
            .ToList();
        if (_context is not null && groupAssignments.Count > 0)
        {
            var teamIds = groupAssignments.Select(member => member.TeamId!.Value).Distinct().ToArray();
            var teamRows = await _context.Set<Team>().AsNoTracking()
                .Where(team => teamIds.Contains(team.Id) && team.IsActive && !team.IsDeleted)
                .Select(team => team.Id)
                .ToArrayAsync(cancellationToken);
            var activeTeamIds = teamRows.ToHashSet();
            var teamMemberships = await _context.Set<TeamMember>().AsNoTracking()
                .Where(member => activeTeamIds.Contains(member.TeamId) && member.IsActive && !member.IsDeleted
                    && member.EffectiveFrom.Date < periodEnd
                    && (!member.EffectiveTo.HasValue || member.EffectiveTo.Value.Date >= periodStart))
                .Join(_context.Set<NEXUS.Modules.ResourceManagement.Domain.Resource>().AsNoTracking()
                        .Where(resource => resource.IsActive && !resource.IsDeleted),
                    member => member.ResourceId, resource => resource.Id,
                    (member, resource) => new { member.TeamId, member.ResourceId, member.EffectiveFrom, member.EffectiveTo })
                .ToListAsync(cancellationToken);

            foreach (var assignment in groupAssignments)
            foreach (var membership in teamMemberships.Where(member => member.TeamId == assignment.TeamId))
            {
                var memberStart = membership.EffectiveFrom.Date > assignment.StartDate.Date
                    ? membership.EffectiveFrom.Date : assignment.StartDate.Date;
                var memberEnd = membership.EffectiveTo?.Date ?? assignment.EndDate.Date;
                if (memberEnd > assignment.EndDate.Date) memberEnd = assignment.EndDate.Date;
                if (memberStart >= periodEnd || memberEnd < periodStart || memberStart > memberEnd) continue;
                candidates.Add(new ProjectTeamCapacityMemberInputDto
                {
                    ResourceId = membership.ResourceId,
                    ProjectId = projectId,
                    StartDate = memberStart,
                    EndDate = memberEnd,
                    AllocationPercentage = assignment.AllocationPercentage,
                    HoursPerWeek = null,
                    UseProjectCalendar = string.Equals(assignment.CalendarPolicy, CalendarPolicyConstants.FollowProjectCalendar, StringComparison.Ordinal),
                    ProjectCalendarId = projectResult.Value.WorkingCalendarId
                });
            }
        }

        // A direct assignment and one or more Team memberships can identify the same
        // Resource. Count that Resource once and use the highest dedication as the
        // effective project ceiling for the selected reporting period.
        var members = candidates
            .GroupBy(member => member.ResourceId)
            .Select(group =>
            {
                var selected = group.OrderByDescending(member => member.AllocationPercentage ?? 0m)
                    .ThenBy(member => member.StartDate).First();
                return new ProjectTeamCapacityMemberInputDto
                {
                    ResourceId = group.Key,
                    ProjectId = projectId,
                    StartDate = group.Min(member => member.StartDate),
                    EndDate = group.Max(member => member.EndDate),
                    AllocationPercentage = group.Max(member => member.AllocationPercentage ?? 0m),
                    HoursPerWeek = null,
                    UseProjectCalendar = selected.UseProjectCalendar,
                    ProjectCalendarId = projectResult.Value.WorkingCalendarId
                };
            })
            .ToArray();

        return await _capacityService.GetProjectTeamOverviewAsync(members, new CapacityFilterDto
        {
            PeriodType = periodType,
            StartDate = periodStart,
            EndDate = periodEnd,
            IncludeProposed = true
        }, cancellationToken);
    }
}
