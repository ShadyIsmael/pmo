using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Application;

public sealed class ProjectReportingService : IProjectReportingService
{
    private const int MaxIncludedPlans = 25;
    private static readonly string[] AllowedSections = [
        "ExecutiveSummary", "PerformanceKPIs", "PlanPerformance", "Milestones", "RisksIssues", "DecisionsRequired"];
    private readonly DbContext _context;
    private readonly IAuditService _audit;
    private readonly IProjectEvmPlanLookup _planLookup;
    private readonly IProjectEvmLaborCostLookup _laborCostLookup;
    private readonly IProjectEvmDirectCostLookup _directCostLookup;
    private readonly IManagementContextAccessor? _managementContextAccessor;
    private readonly ITenantFiscalYearService? _tenantFiscalYear;
    private readonly IFiscalYearCalendarService? _fiscalYearCalendar;

    public ProjectReportingService(
        DbContext context,
        IAuditService audit,
        IProjectEvmPlanLookup planLookup,
        IProjectEvmLaborCostLookup laborCostLookup,
        IProjectEvmDirectCostLookup directCostLookup,
        IManagementContextAccessor? managementContextAccessor = null,
        ITenantFiscalYearService? tenantFiscalYear = null,
        IFiscalYearCalendarService? fiscalYearCalendar = null)
    {
        _context = context;
        _audit = audit;
        _planLookup = planLookup;
        _laborCostLookup = laborCostLookup;
        _directCostLookup = directCostLookup;
        _managementContextAccessor = managementContextAccessor;
        _tenantFiscalYear = tenantFiscalYear;
        _fiscalYearCalendar = fiscalYearCalendar;
    }

    public async Task<Result<ProjectReportingOptionsDto>> GetOptionsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await GetProjectAsync(projectId, cancellationToken);
        if (project is null) return Result<ProjectReportingOptionsDto>.Failure(Error.NotFound("Project not found"));
        var options = await _planLookup.GetReportingOptionsAsync(projectId, project.TenantId, cancellationToken);
        return Result<ProjectReportingOptionsDto>.Success(new ProjectReportingOptionsDto
        {
            ProjectId = projectId,
            Plans = options.Select(MapOption).ToList()
        });
    }

    public async Task<Result<ProjectReportingConfigurationDto?>> GetConfigurationAsync(
        Guid projectId,
        DateTime? effectiveAt = null,
        CancellationToken cancellationToken = default)
    {
        var project = await GetProjectAsync(projectId, cancellationToken);
        if (project is null) return Result<ProjectReportingConfigurationDto?>.Failure(Error.NotFound("Project not found"));
        var date = (effectiveAt ?? DateTime.UtcNow).Date;
        var config = await _context.Set<ProjectReportingConfiguration>().AsNoTracking()
            .Include(item => item.Plans)
            .Where(item => item.TenantId == project.TenantId && item.ProjectId == projectId
                && !item.IsDeleted && item.Status == "Approved"
                && item.EffectiveFrom.Date <= date
                && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value.Date > date))
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (config is null) return Result<ProjectReportingConfigurationDto?>.Success(null);
        var options = await _planLookup.GetReportingOptionsAsync(projectId, project.TenantId, cancellationToken);
        return Result<ProjectReportingConfigurationDto?>.Success(MapConfiguration(config, options));
    }

    public async Task<Result<ProjectReportingConfigurationDto>> SaveConfigurationAsync(
        Guid projectId,
        SaveProjectReportingConfigurationDto dto,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var project = await GetProjectAsync(projectId, cancellationToken);
        if (project is null) return Result<ProjectReportingConfigurationDto>.Failure(Error.NotFound("Project not found"));
        if (dto is null) return InvalidConfiguration("A reporting configuration is required.");
        var requestedPlans = dto.Plans ?? [];
        var requestedSections = dto.IncludedSections ?? [];

        var effectiveFrom = dto.EffectiveFrom.Date;
        if (effectiveFrom == DateTime.MinValue)
            return InvalidConfiguration("An effective date is required.");
        if (!string.Equals(dto.ReportingFrequency, "Weekly", StringComparison.OrdinalIgnoreCase))
            return InvalidConfiguration("Only weekly reporting is currently supported.");
        if (string.IsNullOrWhiteSpace(dto.TimeZoneId) || !IsKnownTimeZone(dto.TimeZoneId))
            return InvalidConfiguration("Select a valid reporting time zone.");
        var currency = (dto.CurrencyCode ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length is < 3 or > 10 || !string.Equals(currency, project.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
            return InvalidConfiguration("Reporting currency must match the project's currency; currency conversion is not configured.");
        if (requestedPlans.Count is < 1 or > MaxIncludedPlans || requestedPlans.All(item => !item.IncludeInConsolidatedReporting))
            return InvalidConfiguration($"Select between 1 and {MaxIncludedPlans} execution plans.");
        var sectionSet = requestedSections.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (sectionSet.Count == 0)
            return InvalidConfiguration("Select at least one report section.");
        if (sectionSet.Any(item => !AllowedSections.Contains(item, StringComparer.Ordinal)))
            return InvalidConfiguration("The reporting configuration contains an unsupported report section.");
        if (requestedPlans.Select(item => item.PlanId).Distinct().Count() != requestedPlans.Count)
            return InvalidConfiguration("A plan can only be selected once in a reporting configuration.");

        var optionRows = await _planLookup.GetReportingOptionsAsync(projectId, project.TenantId, cancellationToken);
        var optionById = optionRows.ToDictionary(item => item.Id);
        foreach (var selection in requestedPlans)
        {
            if (!optionById.TryGetValue(selection.PlanId, out var plan)
                || !string.Equals(plan.PlanType, PlanType.Execution.ToString(), StringComparison.Ordinal))
                return InvalidConfiguration("Only execution plans can be selected for official consolidated reporting.");
        }

        var selections = requestedPlans.Select(item => new ProjectEvmPlanSelection(item.PlanId, item.WorkingVersionId, item.BaselineVersionId)).ToList();
        var resolved = await _planLookup.GetReportingPlansAsync(projectId, project.TenantId, selections, cancellationToken);
        if (resolved.Count != selections.Count)
            return InvalidConfiguration("Every selected plan must belong to this project and have one active working version and one approved baseline.");

        var scopeOwner = new Dictionary<Guid, string>();
        var includedPlanIds = requestedPlans.Where(item => item.IncludeInConsolidatedReporting).Select(item => item.PlanId).ToHashSet();
        foreach (var plan in resolved.Where(item => includedPlanIds.Contains(item.PlanId)))
        {
            foreach (var activityKey in plan.ActivityScopeKeys.Where(key => key != Guid.Empty))
            {
                if (scopeOwner.TryGetValue(activityKey, out var owner))
                    return InvalidConfiguration($"Reporting scope overlaps between '{owner}' and '{plan.PlanName}'. Remove the duplicate activity from one plan before including both.");
                scopeOwner[activityKey] = plan.PlanName;
            }
        }

        var current = await _context.Set<ProjectReportingConfiguration>()
            .Include(item => item.Plans)
            .Where(item => item.TenantId == project.TenantId && item.ProjectId == projectId && !item.IsDeleted && item.IsCurrent)
            .FirstOrDefaultAsync(cancellationToken);
        if (current is not null && effectiveFrom <= current.EffectiveFrom.Date)
            return InvalidConfiguration("A new reporting configuration must take effect after the current configuration's effective date.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var latestVersion = await _context.Set<ProjectReportingConfiguration>().AsNoTracking()
            .Where(item => item.TenantId == project.TenantId && item.ProjectId == projectId && !item.IsDeleted)
            .Select(item => (int?)item.Version).MaxAsync(cancellationToken) ?? 0;
        if (current is not null)
        {
            current.IsCurrent = false;
            current.EffectiveTo = effectiveFrom;
            current.UpdatedAt = DateTime.UtcNow;
            current.UpdatedBy = userId;
        }

        var configuration = new ProjectReportingConfiguration
        {
            TenantId = project.TenantId,
            ProjectId = projectId,
            Version = latestVersion + 1,
            EffectiveFrom = effectiveFrom,
            ReportingFrequency = "Weekly",
            TimeZoneId = dto.TimeZoneId.Trim(),
            CurrencyCode = currency,
            Status = "Approved",
            IsCurrent = true,
            IncludedSectionsJson = JsonSerializer.Serialize(sectionSet),
            ConfiguredBy = userId,
            ConfiguredAt = DateTime.UtcNow,
            CreatedBy = userId,
            Plans = resolved.Select(plan =>
            {
                var input = requestedPlans.Single(item => item.PlanId == plan.PlanId);
                return new ProjectReportingPlanSelection
                {
                    TenantId = project.TenantId,
                    PlanId = plan.PlanId,
                    WorkingVersionId = plan.WorkingVersionId,
                    BaselineVersionId = plan.BaselineVersionId,
                    IncludeInConsolidatedReporting = input.IncludeInConsolidatedReporting,
                    PlanNameSnapshot = plan.PlanName,
                    PlanTypeSnapshot = plan.PlanType,
                    CreatedBy = userId
                };
            }).ToList()
        };
        _context.Set<ProjectReportingConfiguration>().Add(configuration);
        await _context.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(new AuditEntry
        {
            TenantId = project.TenantId,
            EntityType = "ProjectReportingConfiguration",
            EntityId = configuration.Id,
            Action = "Configure",
            UserId = ParseUserId(userId),
            OriginalValues = current is null ? null : JsonSerializer.Serialize(new { current.Id, current.Version, current.EffectiveFrom, current.EffectiveTo }),
            NewValues = JsonSerializer.Serialize(new
            {
                configuration.Version,
                configuration.EffectiveFrom,
                configuration.ReportingFrequency,
                configuration.TimeZoneId,
                configuration.CurrencyCode,
                PlanCount = configuration.Plans.Count,
                PlanIds = configuration.Plans.Select(item => item.PlanId),
                Sections = sectionSet
            }),
            Timestamp = configuration.ConfiguredAt,
            CreatedBy = userId
        }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var freshOptions = await _planLookup.GetReportingOptionsAsync(projectId, project.TenantId, cancellationToken);
        return Result<ProjectReportingConfigurationDto>.Success(MapConfiguration(configuration, freshOptions));
    }

    public async Task<Result<ProjectPerformanceDto>> GetPerformanceAsync(
        Guid projectId,
        DateTime? statusDate = null,
        string perspective = "Consolidated",
        Guid? planId = null,
        Guid? comparePlanId = null,
        CancellationToken cancellationToken = default)
    {
        var project = await GetProjectAsync(projectId, cancellationToken);
        if (project is null) return Result<ProjectPerformanceDto>.Failure(Error.NotFound("Project not found"));
        var cutoff = (statusDate ?? DateTime.UtcNow).Date;
        var normalizedPerspective = perspective.Trim();
        if (normalizedPerspective is not ("Consolidated" or "Individual" or "Compare"))
            return Result<ProjectPerformanceDto>.Failure(Error.Validation("Perspective must be Consolidated, Individual, or Compare."));

        var options = await _planLookup.GetReportingOptionsAsync(projectId, project.TenantId, cancellationToken);
        var config = await _context.Set<ProjectReportingConfiguration>().AsNoTracking()
            .Include(item => item.Plans)
            .Where(item => item.TenantId == project.TenantId && item.ProjectId == projectId && !item.IsDeleted
                && item.Status == "Approved" && item.EffectiveFrom.Date <= cutoff
                && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value.Date > cutoff))
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (config is null)
            return Result<ProjectPerformanceDto>.Success(Unavailable(projectId, cutoff, normalizedPerspective, project.Currency, options,
                "NO_REPORTING_CONFIGURATION"));

        var official = config.Plans.Where(item => item.IncludeInConsolidatedReporting).ToList();
        var selected = normalizedPerspective switch
        {
            "Individual" => official.Where(item => item.PlanId == planId).ToList(),
            "Compare" => official.Where(item => item.PlanId == planId || item.PlanId == comparePlanId).ToList(),
            _ => official
        };
        if (selected.Count == 0 || normalizedPerspective == "Individual" && selected.Count != 1
            || normalizedPerspective == "Compare" && (planId == comparePlanId || selected.Count != 2))
            return Result<ProjectPerformanceDto>.Success(Unavailable(projectId, cutoff, normalizedPerspective, config.CurrencyCode, options,
                "NO_ELIGIBLE_EXECUTION_PLANS", config.Version, DeserializeSections(config.IncludedSectionsJson)));

        if (!string.Equals(config.CurrencyCode, project.Currency, StringComparison.OrdinalIgnoreCase))
            return Result<ProjectPerformanceDto>.Success(Unavailable(projectId, cutoff, normalizedPerspective, config.CurrencyCode, options,
                "INCOMPATIBLE_CURRENCY", config.Version, DeserializeSections(config.IncludedSectionsJson)));

        var planSelections = selected.Select(item => new ProjectEvmPlanSelection(item.PlanId, item.WorkingVersionId, item.BaselineVersionId)).ToList();
        var plans = await _planLookup.GetReportingPlansAsync(projectId, project.TenantId, planSelections, cancellationToken);
        if (plans.Count != selected.Count)
            return Result<ProjectPerformanceDto>.Success(Unavailable(projectId, cutoff, normalizedPerspective, config.CurrencyCode, options,
                "REPORTING_CONFIGURATION_INVALID", config.Version, DeserializeSections(config.IncludedSectionsJson)));

        var duplicateScope = plans.SelectMany(plan => plan.ActivityScopeKeys.Where(key => key != Guid.Empty)
                .Select(key => (Key: key, plan.PlanName)))
            .GroupBy(item => item.Key).FirstOrDefault(group => group.Select(item => item.PlanName).Distinct(StringComparer.Ordinal).Count() > 1);
        if (duplicateScope is not null)
            return Result<ProjectPerformanceDto>.Success(Unavailable(projectId, cutoff, normalizedPerspective, config.CurrencyCode, options,
                "OVERLAPPING_SCOPE", config.Version, DeserializeSections(config.IncludedSectionsJson)));

        var isHistorical = cutoff < DateTime.UtcNow.Date;
        var planResults = new List<(ProjectEvmReportingPlanData Plan, ProjectEvmLaborCostSummary Labor)>();
        foreach (var plan in plans)
        {
            var labor = await _laborCostLookup.GetAsync(project.TenantId, projectId, config.CurrencyCode, cutoff,
                plan.Assignments, cancellationToken, plan.CurrentActivityIds);
            planResults.Add((plan, labor));
        }

        var aggregateAssignments = plans.SelectMany(plan => plan.Assignments).ToList();
        var includedActivityIds = plans.SelectMany(plan => plan.CurrentActivityIds).ToHashSet();
        var includeProjectLevelActuals = normalizedPerspective == "Consolidated";
        var projectLabor = await _laborCostLookup.GetAsync(project.TenantId, projectId, config.CurrencyCode, cutoff,
            aggregateAssignments, cancellationToken, includedActivityIds,
            includeUnassignedProjectActuals: includeProjectLevelActuals);
        var directCosts = await _directCostLookup.GetAsync(project.TenantId, projectId, config.CurrencyCode, cutoff, cancellationToken);

        var planDtos = new List<ProjectPlanPerformanceDto>();
        var dataReasons = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (plan, labor) in planResults)
        {
            var evValue = isHistorical ? null : labor.EarnedValue;
            var evReason = isHistorical ? "PROGRESS_HISTORY_UNAVAILABLE" : labor.EarnedValueUnavailableReason;
            var planActualCost = labor.HasActualLaborData ? labor.ActualLaborCost : null;
            var metrics = ProjectEvmCalculator.Calculate(labor.PlannedValue, evValue, planActualCost,
                labor.PlannedValueUnavailableReason, evReason,
                labor.ActualLaborCostUnavailableReason,
                directCosts.HasData ? "INCOMPLETE_COST_ATTRIBUTION" : null);
            var reasons = new[] { labor.PlannedValueUnavailableReason, evReason,
                    planActualCost.HasValue ? null : "NO_ACTUAL_COST", metrics.CostPerformanceIndexUnavailableReason }
                .Where(reason => reason is not null).Cast<string>().Distinct(StringComparer.Ordinal).ToList();
            foreach (var reason in reasons) dataReasons.Add(reason);
            planDtos.Add(new ProjectPlanPerformanceDto
            {
                PlanId = plan.PlanId,
                PlanName = plan.PlanName,
                PlanType = plan.PlanType,
                Classification = "Execution",
                PlanStatus = plan.PlanStatus,
                IncludedInOfficialReporting = true,
                WorkingVersionId = plan.WorkingVersionId,
                WorkingVersionNumber = plan.WorkingVersionNumber,
                BaselineVersionId = plan.BaselineVersionId,
                BaselineName = plan.BaselineName,
                BaselineVersionNumber = plan.BaselineVersionNumber,
                BudgetAtCompletion = labor.BudgetAtCompletion,
                PlannedValue = labor.PlannedValue,
                EarnedValue = evValue,
                ActualCost = planActualCost,
                PlannedProgressPercent = Percent(labor.PlannedValue, labor.BudgetAtCompletion),
                ActualProgressPercent = Percent(evValue, labor.BudgetAtCompletion),
                ScheduleVariance = metrics.ScheduleVariance,
                CostVariance = metrics.CostVariance,
                SchedulePerformanceIndex = metrics.SchedulePerformanceIndex,
                CostPerformanceIndex = metrics.CostPerformanceIndex,
                CostPerformanceIndexUnavailableReason = metrics.CostPerformanceIndexUnavailableReason,
                DataQualityReasons = reasons
            });
        }

        var bac = SumIfComplete(planResults.Select(item => item.Labor.BudgetAtCompletion));
        var pv = SumIfComplete(planResults.Select(item => item.Labor.PlannedValue));
        var ev = isHistorical ? null : SumIfComplete(planResults.Select(item => item.Labor.EarnedValue));
        var allLaborCostValid = !projectLabor.HasActualLaborData || projectLabor.ActualLaborCost.HasValue;
        decimal? actualCost = includeProjectLevelActuals && directCosts.HasCurrencyMismatch || !allLaborCostValid ? null
            : projectLabor.HasActualLaborData || includeProjectLevelActuals && directCosts.HasData
                ? (projectLabor.ActualLaborCost ?? 0m) + (includeProjectLevelActuals && directCosts.HasData ? directCosts.Amount : 0m)
                : null;
        var planAttributed = planResults.All(item => !item.Labor.HasActualLaborData || item.Labor.ActualLaborCost.HasValue)
            ? planResults.Sum(item => item.Labor.ActualLaborCost ?? 0m) : (decimal?)null;
        decimal? unallocated = includeProjectLevelActuals && actualCost.HasValue && planAttributed.HasValue
            ? Math.Max(0m, actualCost.GetValueOrDefault() - planAttributed.GetValueOrDefault()) : null;
        var pvReason = FirstReason(planResults.Select(item => item.Labor.PlannedValueUnavailableReason), "NO_PLANNED_VALUE");
        var evUnavailable = isHistorical ? "PROGRESS_HISTORY_UNAVAILABLE"
            : FirstReason(planResults.Select(item => item.Labor.EarnedValueUnavailableReason), "NO_EARNED_VALUE");
        var actualReason = includeProjectLevelActuals && directCosts.HasCurrencyMismatch ? "CURRENCY_MISMATCH"
            : !allLaborCostValid ? projectLabor.ActualLaborCostUnavailableReason ?? "EVM_DATA_INCOMPLETE"
            : !actualCost.HasValue
                ? !includeProjectLevelActuals && directCosts.HasData ? "INCOMPLETE_COST_ATTRIBUTION"
                    : directCosts.Amount == 0m && !projectLabor.HasActualLaborData ? "NO_ACTUAL_COST" : null
            : null;
        var hasUnallocated = unallocated.GetValueOrDefault() > 0m;
        var costComparisonReason = actualCost.HasValue
            ? directCosts.HasData || hasUnallocated ? "INCOMPLETE_COST_ATTRIBUTION" : null
            : actualReason;
        var aggregateMetrics = ProjectEvmCalculator.Calculate(pv, ev, actualCost, pvReason, evUnavailable, actualReason, costComparisonReason);
        if (isHistorical) dataReasons.Add("PROGRESS_HISTORY_UNAVAILABLE");
        if (actualReason is not null) dataReasons.Add(actualReason);
        if (hasUnallocated || directCosts.HasData) dataReasons.Add("INCOMPLETE_COST_ATTRIBUTION");
        if (pvReason is not null) dataReasons.Add(pvReason);
        if (evUnavailable is not null) dataReasons.Add(evUnavailable);
        if (aggregateMetrics.CostPerformanceIndexUnavailableReason is not null) dataReasons.Add(aggregateMetrics.CostPerformanceIndexUnavailableReason);

        var milestones = planResults.SelectMany(item => item.Plan.Milestones.Select(milestone => MapMilestone(item.Plan, milestone)))
            .OrderBy(item => item.PlannedDate ?? item.BaselineDate).ThenBy(item => item.PlanName, StringComparer.Ordinal).ThenBy(item => item.Name, StringComparer.Ordinal).ToList();
        var requiredMetricsComplete = planResults.All(item => item.Labor.BudgetAtCompletion.HasValue && item.Labor.PlannedValue.HasValue)
            && (!isHistorical && planResults.All(item => item.Labor.EarnedValue.HasValue));
        var quality = selected.Count == 0 ? "Unavailable"
            : requiredMetricsComplete && actualCost.HasValue && !hasUnallocated && !directCosts.HasData ? "Complete"
            : planResults.Any(item => item.Labor.BudgetAtCompletion.HasValue || item.Labor.PlannedValue.HasValue || item.Labor.EarnedValue.HasValue) ? "Partial"
            : "Unavailable";

        return Result<ProjectPerformanceDto>.Success(new ProjectPerformanceDto
        {
            ProjectId = projectId,
            StatusDate = cutoff,
            Perspective = normalizedPerspective,
            DataQuality = quality,
            ReportingConfigurationVersion = config.Version,
            IncludedReportSections = DeserializeSections(config.IncludedSectionsJson),
            Currency = config.CurrencyCode,
            BudgetAtCompletion = bac,
            PlannedValue = pv,
            EarnedValue = ev,
            ActualCost = actualCost,
            PlanAttributedActualCost = planAttributed,
            UnallocatedProjectActualCost = unallocated,
            PlannedProgressPercent = Percent(pv, bac),
            OverallProgressPercent = Percent(ev, bac),
            ScheduleVariance = aggregateMetrics.ScheduleVariance,
            CostVariance = aggregateMetrics.CostVariance,
            SchedulePerformanceIndex = aggregateMetrics.SchedulePerformanceIndex,
            CostPerformanceIndex = aggregateMetrics.CostPerformanceIndex,
            PlannedValueUnavailableReason = pv.HasValue ? null : pvReason,
            EarnedValueUnavailableReason = ev.HasValue ? null : evUnavailable,
            ActualCostUnavailableReason = actualReason,
            SchedulePerformanceIndexUnavailableReason = aggregateMetrics.SchedulePerformanceIndexUnavailableReason,
            CostPerformanceIndexUnavailableReason = aggregateMetrics.CostPerformanceIndexUnavailableReason,
            CostVarianceUnavailableReason = aggregateMetrics.CostVarianceUnavailableReason,
            DataQualityReasons = dataReasons.OrderBy(item => item, StringComparer.Ordinal).ToList(),
            Plans = planDtos,
            AvailablePlans = options.Select(MapOption).ToList(),
            Milestones = milestones,
            CriticalPathMode = "ByPlan",
            CriticalPathUnavailableReason = "UNIFIED_CROSS_PLAN_NETWORK_UNAVAILABLE"
        });
    }

    public async Task<Result<ProjectFinancialSummaryDto>> GetFinancialSummaryAsync(
        Guid projectId,
        DateTime periodFrom,
        DateTime periodTo,
        CancellationToken cancellationToken = default)
    {
        var management = _managementContextAccessor?.Current;
        if (management is null || management.TenantId == Guid.Empty)
            return Result<ProjectFinancialSummaryDto>.Failure(Error.Forbidden("A valid tenant management context is required."));
        if (!management.Scope.ProjectIds.Contains(projectId))
            return Result<ProjectFinancialSummaryDto>.Failure(Error.NotFound("Project not found"));

        var from = periodFrom.Date;
        var to = periodTo.Date;
        if (from == DateTime.MinValue.Date || to == DateTime.MinValue.Date || from > to)
            return Result<ProjectFinancialSummaryDto>.Failure(Error.Validation("The reporting period must include valid dates in chronological order."));

        var project = await GetProjectAsync(projectId, cancellationToken);
        if (project is null || project.TenantId != management.TenantId)
            return Result<ProjectFinancialSummaryDto>.Failure(Error.NotFound("Project not found"));

        var currency = NormalizeCurrency(project.Currency);
        var validCurrency = IsValidCurrency(currency);
        var fromDate = DateOnly.FromDateTime(from);
        var toDate = DateOnly.FromDateTime(to);
        var hasDataQualityGap = !validCurrency;

        var annualBudgets = await _context.Set<ProjectAnnualBudget>().AsNoTracking()
            .Where(item => item.ProjectId == projectId
                && item.FiscalYearStartDate <= toDate
                && item.FiscalYearEndDate >= fromDate)
            .ToListAsync(cancellationToken);

        var fiscalStartMonth = _tenantFiscalYear is null
            ? (int?)null
            : await _tenantFiscalYear.GetStartMonthAsync(project.TenantId, cancellationToken);
        var matchingApprovedBudgets = annualBudgets.Where(item =>
                item.Status == ProjectAnnualBudgetStatuses.Approved
                && NormalizeCurrency(item.Currency) == currency
                && item.FiscalYearStartDate <= item.FiscalYearEndDate
                && IsFiscalBudgetBoundaryValid(item, fiscalStartMonth))
            .ToArray();
        var annualBudgetCountByYear = annualBudgets
            .Where(item => item.Status == ProjectAnnualBudgetStatuses.Approved
                && NormalizeCurrency(item.Currency) == currency)
            .GroupBy(item => item.FiscalYear)
            .ToDictionary(group => group.Key, group => group.Count());
        var hasDuplicateAnnualBudget = annualBudgetCountByYear.Values.Any(count => count > 1);
        var hasForeignCurrencyBudget = annualBudgets.Any(item => item.Status == ProjectAnnualBudgetStatuses.Approved
            && NormalizeCurrency(item.Currency) != currency);
        var hasInvalidBudgetPeriod = annualBudgets.Any(item => item.Status == ProjectAnnualBudgetStatuses.Approved
            && (item.FiscalYearStartDate > item.FiscalYearEndDate || !IsFiscalBudgetBoundaryValid(item, fiscalStartMonth)));
        hasDataQualityGap |= hasDuplicateAnnualBudget || hasForeignCurrencyBudget || hasInvalidBudgetPeriod;

        var budgetCoverageComplete = !hasDuplicateAnnualBudget
            && HasFullAnnualBudgetCoverage(fromDate, toDate, matchingApprovedBudgets);
        var approvedBudget = matchingApprovedBudgets.Length == 0 || hasDuplicateAnnualBudget
            ? (decimal?)null
            : matchingApprovedBudgets.Sum(item => item.TotalAnnualBudget);
        var budgetStatus = hasDuplicateAnnualBudget
            ? "unavailable"
            : budgetCoverageComplete ? "approved"
            : approvedBudget.HasValue ? "partial" : annualBudgets.Count > 0 ? "notApproved" : "unavailable";

        var directCosts = validCurrency
            ? await _directCostLookup.GetAsync(management.TenantId, projectId, currency, to, cancellationToken, from)
            : new ProjectEvmDirectCostSummary(0m, false, false);
        var laborCosts = validCurrency
            ? await _laborCostLookup.GetAsync(management.TenantId, projectId, currency, to, [], cancellationToken,
                includeUnassignedProjectActuals: true, periodFrom: from)
            : new ProjectEvmLaborCostSummary(null, null, null, null, null, null, "INVALID_PROJECT_CURRENCY", false);
        var hasExpenditureData = directCosts.HasData || laborCosts.HasActualLaborData;
        var hasExpenditureGap = directCosts.HasCurrencyMismatch
            || (laborCosts.HasActualLaborData && laborCosts.ActualLaborCost is null);
        hasDataQualityGap |= hasExpenditureGap;
        decimal? expenditure = !validCurrency || hasExpenditureGap
            ? null
            : directCosts.Amount + (laborCosts.HasActualLaborData ? laborCosts.ActualLaborCost.GetValueOrDefault() : 0m);

        var purchaseCommitments = await _context.Set<PurchaseCommitment>().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.CommitmentDate.Date <= to)
            .ToListAsync(cancellationToken);
        var activeCommitments = purchaseCommitments.Where(item =>
            !string.Equals(item.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(item.Status, "Closed", StringComparison.OrdinalIgnoreCase)).ToArray();
        var hasCommitmentCurrencyMismatch = activeCommitments.Any(item => NormalizeCurrency(item.Currency) != currency);
        hasDataQualityGap |= hasCommitmentCurrencyMismatch;
        var committedCost = !validCurrency || hasCommitmentCurrencyMismatch
            ? (decimal?)null
            : activeCommitments.Where(item => NormalizeCurrency(item.Currency) == currency).Sum(item => item.Amount);

        var obligations = await _context.Set<PaymentObligation>().AsNoTracking()
            .Where(item => item.ProjectId == projectId && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        var matchingObligations = obligations.Where(item => NormalizeCurrency(item.Currency) == currency).ToArray();
        var hasObligationCurrencyMismatch = obligations.Any(item => NormalizeCurrency(item.Currency) != currency);
        hasDataQualityGap |= hasObligationCurrencyMismatch;

        var obligationIds = matchingObligations.Select(item => item.Id).ToArray();

        var settlements = await _context.Set<PaymentSettlement>().AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.PaymentObligationId.HasValue
                && obligationIds.Contains(item.PaymentObligationId.Value)
                && item.SettlementDate.Date <= to)
            .ToListAsync(cancellationToken);

        var unknownCurrencySettlement = settlements.Any(item => item.Currency is null
            || NormalizeCurrency(item.Currency) != currency);
        hasDataQualityGap |= unknownCurrencySettlement;

        var knownCurrencySettlements = settlements.Where(item => NormalizeCurrency(item.Currency) == currency).ToArray();
        var settledByObligation = knownCurrencySettlements
            .GroupBy(item => item.PaymentObligationId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

        decimal settled(PaymentObligation o) => settledByObligation.TryGetValue(o.Id, out var amount) ? amount : 0m;

        var hasFinancialData = annualBudgets.Count > 0 || hasExpenditureData || purchaseCommitments.Count > 0
            || obligations.Count > 0 || settlements.Count > 0;
        var actualPaid = unknownCurrencySettlement
            ? (decimal?)null
            : knownCurrencySettlements.Where(item => item.SettlementDate.Date >= from && item.SettlementDate.Date <= to)
                .Sum(item => item.Amount);

        var outstandingPlannedPayments = matchingObligations
            .Select(item => Math.Max(0m, item.Amount - settled(item)))
            .Where(amount => amount > 0m)
            .Sum();

        var paymentsDueWithinPeriod = matchingObligations
            .Where(item => {
                var outstanding = Math.Max(0m, item.Amount - settled(item));
                return outstanding > 0m && item.DueDate.Date >= from && item.DueDate.Date <= to;
            })
            .Sum(item => Math.Max(0m, item.Amount - settled(item)));

        var remainingUnpaidAtPeriodEnd = matchingObligations
            .Where(item => {
                var outstanding = Math.Max(0m, item.Amount - settled(item));
                return outstanding > 0m && item.DueDate.Date <= to;
            })
            .Sum(item => Math.Max(0m, item.Amount - settled(item)));

        var hasMoneyData = matchingApprovedBudgets.Length > 0 || hasExpenditureData && !hasExpenditureGap
            || activeCommitments.Any(item => NormalizeCurrency(item.Currency) == currency)
            || matchingObligations.Length > 0 || knownCurrencySettlements.Length > 0;
        var dataStatus = !hasFinancialData ? "empty"
            : !validCurrency || (hasDataQualityGap && !hasMoneyData) ? "unavailable"
            : hasDataQualityGap || !budgetCoverageComplete ? "partial"
            : "available";

        return Result<ProjectFinancialSummaryDto>.Success(new ProjectFinancialSummaryDto
        {
            ProjectId = projectId,
            PeriodFrom = from,
            PeriodTo = to,
            CurrencyCode = validCurrency ? currency : null,
            ApprovedBudget = approvedBudget,
            BudgetStatus = budgetStatus,
            Expenditure = expenditure ?? (validCurrency && hasFinancialData && !hasExpenditureData ? 0m : null),
            CommittedCost = committedCost,
            ActualPaid = actualPaid,
            OutstandingPlannedPayments = unknownCurrencySettlement || !validCurrency ? null : outstandingPlannedPayments,
            PaymentsDueWithinPeriod = unknownCurrencySettlement || !validCurrency ? null : paymentsDueWithinPeriod,
            RemainingUnpaidAtPeriodEnd = unknownCurrencySettlement || !validCurrency ? null : remainingUnpaidAtPeriodEnd,
            HasFinancialData = hasFinancialData,
            DataStatus = dataStatus,
        });
    }

    private bool IsFiscalBudgetBoundaryValid(ProjectAnnualBudget budget, int? fiscalYearStartMonth)
    {
        if (!fiscalYearStartMonth.HasValue || _fiscalYearCalendar is null) return true;
        try
        {
            var period = _fiscalYearCalendar.ForFiscalYear(budget.FiscalYear, fiscalYearStartMonth.Value);
            return period.StartDate == budget.FiscalYearStartDate && period.EndDate == budget.FiscalYearEndDate;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool HasFullAnnualBudgetCoverage(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<ProjectAnnualBudget> budgets)
    {
        if (budgets.Count == 0) return false;
        var cursor = from.DayNumber;
        var endExclusive = to.DayNumber + 1;
        foreach (var budget in budgets.OrderBy(item => item.FiscalYearStartDate).ThenBy(item => item.FiscalYearEndDate))
        {
            if (budget.FiscalYearEndDate.DayNumber < cursor) continue;
            if (budget.FiscalYearStartDate.DayNumber > cursor) return false;
            cursor = Math.Max(cursor, budget.FiscalYearEndDate.DayNumber + 1);
            if (cursor >= endExclusive) return true;
        }
        return false;
    }

    private static string NormalizeCurrency(string? currency) => currency?.Trim().ToUpperInvariant() ?? string.Empty;

    private static bool IsValidCurrency(string currency) => currency.Length == 3
        && currency.All(character => character is >= 'A' and <= 'Z');

    private Task<Project?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var management = _managementContextAccessor?.Current;
        if (management is null || management.TenantId == Guid.Empty)
            return Task.FromResult<Project?>(null);

        var projectIds = management.Scope.ProjectIds.ToArray();
        return _context.Set<Project>().AsNoTracking().FirstOrDefaultAsync(item => item.Id == projectId
            && item.TenantId == management.TenantId && projectIds.Contains(item.Id) && !item.IsDeleted, cancellationToken);
    }

    private static ProjectReportingPlanOptionDto MapOption(ProjectEvmPlanOption plan) => new()
    {
        PlanId = plan.Id,
        Name = plan.Name,
        PlanType = plan.PlanType,
        Classification = plan.PlanType == PlanType.Scenario.ToString() ? "WhatIf" : plan.PlanType == PlanType.Execution.ToString() ? "Execution" : "NonExecution",
        Status = plan.Status,
        CurrentWorkingVersionId = plan.CurrentWorkingVersionId,
        CurrentWorkingVersionNumber = plan.CurrentWorkingVersionNumber,
        Versions = plan.Versions.Select(version => new ProjectReportingVersionOptionDto
        {
            Id = version.Id,
            VersionNumber = version.VersionNumber,
            Name = version.Name,
            Type = version.Type,
            Status = version.Status,
            CreatedAt = version.CreatedAt,
            EligibleForReporting = version.Type == PlanVersionType.Working.ToString() && version.Status == PlanVersionStatus.Active.ToString()
                || version.Type == PlanVersionType.Baseline.ToString() && version.Status == PlanVersionStatus.Approved.ToString()
        }).ToList()
    };

    private static ProjectReportingConfigurationDto MapConfiguration(ProjectReportingConfiguration configuration, IReadOnlyList<ProjectEvmPlanOption> options)
    {
        var planById = options.ToDictionary(item => item.Id);
        return new ProjectReportingConfigurationDto
        {
            Id = configuration.Id,
            ProjectId = configuration.ProjectId,
            Version = configuration.Version,
            EffectiveFrom = configuration.EffectiveFrom,
            EffectiveTo = configuration.EffectiveTo,
            ReportingFrequency = configuration.ReportingFrequency,
            TimeZoneId = configuration.TimeZoneId,
            CurrencyCode = configuration.CurrencyCode,
            Status = configuration.Status,
            IsCurrent = configuration.IsCurrent,
            IncludedSections = DeserializeSections(configuration.IncludedSectionsJson),
            ConfiguredBy = configuration.ConfiguredBy,
            ConfiguredAt = configuration.ConfiguredAt,
            Plans = configuration.Plans.OrderBy(item => item.PlanNameSnapshot, StringComparer.Ordinal).Select(item =>
            {
                var plan = planById.GetValueOrDefault(item.PlanId);
                var working = plan?.Versions.FirstOrDefault(version => version.Id == item.WorkingVersionId);
                var baseline = plan?.Versions.FirstOrDefault(version => version.Id == item.BaselineVersionId);
                return new ProjectReportingPlanSelectionDto
                {
                    PlanId = item.PlanId,
                    WorkingVersionId = item.WorkingVersionId,
                    WorkingVersionNumber = working?.VersionNumber ?? 0,
                    BaselineVersionId = item.BaselineVersionId,
                    BaselineVersionNumber = baseline?.VersionNumber ?? 0,
                    BaselineName = baseline?.Name ?? string.Empty,
                    PlanName = item.PlanNameSnapshot,
                    PlanType = item.PlanTypeSnapshot,
                    PlanStatus = plan?.Status ?? "Unavailable",
                    IncludeInConsolidatedReporting = item.IncludeInConsolidatedReporting
                };
            }).ToList()
        };
    }

    private static ProjectPerformanceDto Unavailable(Guid projectId, DateTime statusDate, string perspective, string currency,
        IReadOnlyList<ProjectEvmPlanOption> options, string reason, int? configurationVersion = null,
        IReadOnlyList<string>? includedReportSections = null) => new()
    {
        ProjectId = projectId,
        StatusDate = statusDate,
        Perspective = perspective,
        DataQuality = "Unavailable",
        ReportingConfigurationVersion = configurationVersion,
        IncludedReportSections = includedReportSections ?? [],
        Currency = currency,
        DataQualityReasons = [reason],
        PlannedValueUnavailableReason = reason,
        EarnedValueUnavailableReason = reason,
        ActualCostUnavailableReason = reason,
        SchedulePerformanceIndexUnavailableReason = reason,
        CostPerformanceIndexUnavailableReason = reason,
        CostVarianceUnavailableReason = reason,
        AvailablePlans = options.Select(MapOption).ToList(),
        CriticalPathMode = "ByPlan",
        CriticalPathUnavailableReason = "UNIFIED_CROSS_PLAN_NETWORK_UNAVAILABLE"
    };

    private static ProjectPerformanceMilestoneDto MapMilestone(ProjectEvmReportingPlanData plan, ProjectEvmMilestoneInput milestone)
    {
        var variance = milestone.ActualCompletionDate.HasValue && milestone.BaselineDate.HasValue
            ? (decimal?)(milestone.ActualCompletionDate.Value.Date - milestone.BaselineDate.Value.Date).TotalDays
            : milestone.PlannedDate.HasValue && milestone.BaselineDate.HasValue
                ? (decimal?)(milestone.PlannedDate.Value.Date - milestone.BaselineDate.Value.Date).TotalDays
                : null;
        var status = milestone.ActualCompletionDate.HasValue ? "Completed"
            : milestone.Progress >= 100 ? "Completed"
            : milestone.PlannedDate.HasValue && milestone.PlannedDate.Value.Date < DateTime.UtcNow.Date ? "Delayed"
            : "Upcoming";
        return new ProjectPerformanceMilestoneDto
        {
            PlanId = plan.PlanId,
            PlanName = plan.PlanName,
            Name = milestone.Name,
            BaselineDate = milestone.BaselineDate,
            PlannedDate = milestone.PlannedDate,
            ActualCompletionDate = milestone.ActualCompletionDate,
            VarianceDays = variance,
            Status = status
        };
    }

    private static decimal? SumIfComplete(IEnumerable<decimal?> values)
    {
        var list = values.ToList();
        return list.Count > 0 && list.All(item => item.HasValue) ? list.Sum(item => item!.Value) : null;
    }

    private static decimal? Percent(decimal? numerator, decimal? denominator) =>
        numerator.HasValue && denominator.HasValue && denominator.Value > 0m ? numerator.Value / denominator.Value * 100m : null;

    private static string? FirstReason(IEnumerable<string?> reasons, string fallback) => reasons.FirstOrDefault(item => item is not null) ?? fallback;

    private static bool IsKnownTimeZone(string timeZoneId)
    {
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    private static IReadOnlyList<string> DeserializeSections(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static Result<ProjectReportingConfigurationDto> InvalidConfiguration(string message) =>
        Result<ProjectReportingConfigurationDto>.Failure(Error.Validation(message));

    private static Guid? ParseUserId(string userId) => Guid.TryParse(userId, out var parsed) ? parsed : null;
}
