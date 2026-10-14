using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.IssueManagement.Domain;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using ProjectEntity = NEXUS.Modules.ProjectManagement.Domain.Project;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Api.ProgramManagement;

public sealed class ProgramManagementService : IProgramManagementService
{
    private readonly DbContext _db;
    private readonly IManagementContextAccessor _managementContext;
    private readonly IManagementScopeResolver _scopeResolver;

    public ProgramManagementService(DbContext db, IManagementContextAccessor managementContext, IManagementScopeResolver scopeResolver)
    {
        _db = db;
        _managementContext = managementContext;
        _scopeResolver = scopeResolver;
    }

    public async Task<PagedResult<ProgramManagementListItemDto>> GetProgramsAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var context = _managementContext.Current;
        var canReadTenantData = context?.CanReadAllTenantPortfolioProgramData == true;
        if (!canReadTenantData && (context is null || context.Scope.ProgramIds.Count == 0))
            return new PagedResult<ProgramManagementListItemDto>([], page, pageSize, 0);

        var programIds = canReadTenantData ? Array.Empty<Guid>() : context!.Scope.ProgramIds.ToArray();
        if (!canReadTenantData && context!.ViewMode == ManagementViewMode.Native && context.SelectedPerspective == ManagementPerspective.ProgramManager)
        {
            var nativeSnapshot = await _scopeResolver.GetPerspectiveSnapshotAsync(context.UserId, context.TenantId, null, cancellationToken);
            programIds = nativeSnapshot.Scope.ProgramIds.ToArray();
        }
        var query = _db.Set<ProgramEntity>().AsNoTracking();
        if (canReadTenantData)
        {
            query = query.Where(program => program.TenantId == context!.TenantId);
        }
        else
        {
            query = query.Where(program => programIds.Contains(program.Id));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(program => program.Name.Contains(term) || (program.Code != null && program.Code.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var programs = await query.OrderBy(program => program.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        if (programs.Count == 0)
            return new PagedResult<ProgramManagementListItemDto>([], page, pageSize, total);

        var selectedIds = programs.Select(program => program.Id).ToArray();
        var projects = await ProjectRowsAsync(selectedIds, cancellationToken);
        var schedules = await ScheduleRowsAsync(projects.Select(project => project.Id), cancellationToken);
        var managers = await UserNamesAsync(programs.Select(program => program.ProgramManagerId), cancellationToken);
        var portfolios = await PortfolioNamesAsync(programs.Select(program => program.PortfolioId), cancellationToken);
        var schedulesByProject = schedules.GroupBy(schedule => schedule.ProjectId).ToDictionary(group => group.Key, group => group.OrderByDescending(schedule => schedule.UpdatedAt).First());

        var items = programs.Select(program =>
        {
            var children = projects.Where(project => project.ProgramId == program.Id).ToList();
            var childSchedules = children.Where(project => schedulesByProject.ContainsKey(project.Id)).Select(project => schedulesByProject[project.Id]).ToList();
            return new ProgramManagementListItemDto
            {
                Id = program.Id,
                Name = program.Name,
                Code = program.Code,
                Health = program.Health,
                Status = program.Status,
                ProgramManagerName = managers.GetValueOrDefault(program.ProgramManagerId ?? Guid.Empty),
                PortfolioName = portfolios.GetValueOrDefault(program.PortfolioId),
                StartDate = program.StartDate,
                EndDate = program.EndDate,
                Currency = program.Currency,
                ProjectCount = children.Count,
                ProjectsRequiringAttention = children.Count(project => ProjectNeedsAttention(project) || (schedulesByProject.TryGetValue(project.Id, out var schedule) && IsDelayed(schedule))),
                ScheduleIndicator = ScheduleIndicator(children, childSchedules),
                FinancialIndicator = children.Any(project => project.Budget.HasValue || project.ActualCost.HasValue) ? "Available" : "Unavailable"
            };
        }).ToList();

        return new PagedResult<ProgramManagementListItemDto>(items, page, pageSize, total);
    }

    public async Task<Result<ProgramManagementOverviewDto>> GetOverviewAsync(Guid programId, int projectsPage, int projectsPageSize, CancellationToken cancellationToken = default)
    {
        projectsPage = Math.Max(projectsPage, 1);
        projectsPageSize = Math.Clamp(projectsPageSize, 1, 100);
        var context = _managementContext.Current;
        var canReadTenantData = context?.CanReadAllTenantPortfolioProgramData == true;
        if (!canReadTenantData && (context is null || !context.Scope.ProgramIds.Contains(programId)))
            return Result<ProgramManagementOverviewDto>.Failure(Error.NotFound("Program not found"));

        var programQuery = _db.Set<ProgramEntity>().AsNoTracking().Where(item => item.Id == programId);
        if (canReadTenantData) programQuery = programQuery.Where(item => item.TenantId == context!.TenantId);
        var program = await programQuery.FirstOrDefaultAsync(cancellationToken);
        if (program is null)
            return Result<ProgramManagementOverviewDto>.Failure(Error.NotFound("Program not found"));

        var projects = await ProjectRowsAsync([programId], cancellationToken);
        var projectIds = projects.Select(project => project.Id).ToArray();
        var managers = await UserNamesAsync(projects.Select(project => project.ProjectManagerId).Append(program.ProgramManagerId), cancellationToken);
        var portfolioNames = await PortfolioNamesAsync([program.PortfolioId], cancellationToken);
        var schedules = await ScheduleRowsAsync(projectIds, cancellationToken);
        var milestones = await MilestoneRowsAsync(projectIds, cancellationToken);
        var risks = await RiskRowsAsync(projectIds, cancellationToken);
        var issues = await IssueRowsAsync(projectIds, cancellationToken);
        var changeRequests = await _db.Set<ProjectChangeRequest>().AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId) && item.Status == ProjectChangeRequest.Statuses.Pending)
            .Select(item => new ChangeRequestRow(item.Id, item.ProjectId, item.Impact))
            .ToListAsync(cancellationToken);
        var allocations = await AllocationRowsAsync(projectIds, cancellationToken);
        var resources = await UserResourceNamesAsync(allocations.Select(item => item.ResourceId), cancellationToken);
        var publishedReports = await PublishedStatusReportsAsync(projectIds, cancellationToken);
        var financials = await BuildFinancialSummaryAsync(program, projects, cancellationToken);
        var now = DateTime.UtcNow;

        var latestReports = publishedReports.GroupBy(report => report.ProjectId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(report => report.ReportDate).First());
        var openRisks = risks.Where(IsOpenRisk).ToList();
        var openIssues = issues.Where(IsOpenIssue).ToList();
        var overdueIssues = openIssues.Where(issue => issue.TargetResolutionDate.HasValue && issue.TargetResolutionDate.Value < now).ToList();
        var openRiskCounts = openRisks.GroupBy(risk => risk.ProjectId).ToDictionary(group => group.Key, group => group.Count());
        var openIssueCounts = openIssues.GroupBy(issue => issue.ProjectId!.Value).ToDictionary(group => group.Key, group => group.Count());
        var overdueIssueCounts = overdueIssues.GroupBy(issue => issue.ProjectId!.Value).ToDictionary(group => group.Key, group => group.Count());

        var projectViews = projects.Select(project =>
        {
            var schedule = schedules.Where(item => item.ProjectId == project.Id).OrderByDescending(item => item.UpdatedAt).FirstOrDefault();
            var report = latestReports.GetValueOrDefault(project.Id);
            var scheduleCondition = ScheduleCondition(schedule);
            var projectNeedsAttention = ProjectNeedsAttention(project) || scheduleCondition == "Delayed" || openRiskCounts.ContainsKey(project.Id) || overdueIssueCounts.ContainsKey(project.Id);
            return new ProgramProjectManagementDto
            {
                Id = project.Id,
                Name = project.Name,
                Code = project.Code,
                Status = project.Status,
                Health = project.Health,
                ProjectManagerName = managers.GetValueOrDefault(project.ProjectManagerId ?? Guid.Empty),
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Progress = project.Progress,
                ScheduleCondition = scheduleCondition,
                ForecastEndDate = schedule?.ForecastEndDate,
                Currency = project.Currency,
                Budget = project.Budget,
                ActualCost = project.ActualCost,
                RequiresAttention = projectNeedsAttention,
                OpenRiskCount = openRiskCounts.GetValueOrDefault(project.Id),
                OpenIssueCount = openIssueCounts.GetValueOrDefault(project.Id),
                StatusReportState = report is null ? "Missing" : ReportFreshness(report.ReportDate, now),
                LatestStatusReportDate = report?.ReportDate
            };
        }).OrderByDescending(project => project.RequiresAttention).ThenBy(project => project.Name).ToList();

        var summary = new ProgramManagementSummaryDto
        {
            TotalProjects = projects.Count,
            ActiveProjects = projects.Count(project => IsActiveStatus(project.Status)),
            CompletedProjects = projects.Count(project => IsCompletedStatus(project.Status)),
            ProjectsAtRisk = projectViews.Count(project => project.RequiresAttention),
            CriticalProjects = projects.Count(project => IsCriticalProject(project)),
            AverageProjectProgress = projects.Count == 0 ? null : Math.Round(projects.Average(project => project.Progress), 2),
            ProjectHealthDistribution = projects.GroupBy(project => string.IsNullOrWhiteSpace(project.Health) ? "Unknown" : project.Health)
                .OrderBy(group => group.Key).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase)
        };

        var delivery = new ProgramDeliverySummaryDto
        {
            ProjectsBehindSchedule = projectViews.Count(project => project.ScheduleCondition == "Delayed"),
            ProjectsWithScheduleData = projects.Count(project => schedules.Any(schedule => schedule.ProjectId == project.Id)),
            TotalMilestones = milestones.Count,
            OverdueMilestones = milestones.Count(item => MilestoneState(item, now) == "Overdue"),
            DueSoonMilestones = milestones.Count(item => MilestoneState(item, now) == "DueSoon"),
            OnTrackMilestones = milestones.Count(item => MilestoneState(item, now) == "OnTrack"),
            CompletedMilestones = milestones.Count(item => MilestoneState(item, now) == "Completed")
        };

        var reportViews = projects.Select(project => latestReports.GetValueOrDefault(project.Id) is { } report
                ? new ProgramStatusReportDto
                {
                    ProjectId = project.Id,
                    ProjectName = project.Name,
                    ReportDate = report.ReportDate,
                    Health = report.Health,
                    Progress = report.Progress,
                    Freshness = ReportFreshness(report.ReportDate, now)
                }
                : null)
            .Where(item => item is not null).Cast<ProgramStatusReportDto>()
            .OrderByDescending(item => item.ReportDate).ToList();

        var attention = BuildAttentionItems(projectViews, milestones, openRisks, overdueIssues, changeRequests, latestReports, projects, now);
        var projectSkip = (projectsPage - 1) * projectsPageSize;
        var pagedProjects = projectViews.Skip(projectSkip).Take(projectsPageSize).ToList();

        var overview = new ProgramManagementOverviewDto
        {
            Program = new ProgramManagementIdentityDto
            {
                Id = program.Id,
                Name = program.Name,
                Code = program.Code,
                Status = program.Status,
                Health = program.Health,
                ProgramManagerName = managers.GetValueOrDefault(program.ProgramManagerId ?? Guid.Empty),
                PortfolioId = program.PortfolioId,
                PortfolioName = portfolioNames.GetValueOrDefault(program.PortfolioId),
                Currency = program.Currency,
                StartDate = program.StartDate,
                EndDate = program.EndDate,
                Budget = program.Budget
            },
            Summary = summary,
            Delivery = delivery,
            Financials = financials,
            Risks = new ProgramRiskSummaryDto
            {
                OpenCount = openRisks.Count,
                OverdueCount = 0,
                BySeverity = openRisks.GroupBy(risk => risk.RiskLevel.ToString()).ToDictionary(group => group.Key, group => group.Count())
            },
            Issues = new ProgramIssueSummaryDto
            {
                OpenCount = openIssues.Count,
                OverdueCount = overdueIssues.Count,
                BySeverity = openIssues.GroupBy(issue => string.IsNullOrWhiteSpace(issue.Severity) ? "Unknown" : issue.Severity).ToDictionary(group => group.Key, group => group.Count())
            },
            Resources = BuildResourceSummary(allocations, resources, projects),
            StatusReports = new ProgramStatusReportSummaryDto
            {
                PublishedProjectCount = reportViews.Count,
                CurrentCount = reportViews.Count(item => item.Freshness == "Current"),
                StaleCount = reportViews.Count(item => item.Freshness == "Stale"),
                MissingCount = projects.Count - reportViews.Count,
                FreshnessDays = ManagementReportingRules.StatusReportFreshnessDays,
                LatestPublished = reportViews.Take(10).ToList()
            },
            Projects = pagedProjects,
            ProjectsPage = projectsPage,
            ProjectsPageSize = projectsPageSize,
            ProjectsTotalCount = projectViews.Count,
            Milestones = milestones.OrderBy(item => MilestoneSort(item, now)).ThenBy(item => item.DueDate).Take(12).Select(item => ToMilestoneDto(item, projects)).ToList(),
            TopRisks = openRisks.OrderByDescending(item => RiskRank(item.RiskLevel)).ThenByDescending(item => item.IdentifiedDate).Take(8).Select(item => ToRiskDto(item, projects)).ToList(),
            TopIssues = openIssues.OrderByDescending(item => SeverityRank(item.Severity)).ThenBy(item => item.TargetResolutionDate).Take(8).Select(item => ToIssueDto(item, projects)).ToList(),
            AttentionItems = attention,
            UpcomingMeetings = await UpcomingMeetingsAsync(projectIds, projects, now, cancellationToken)
        };

        return Result<ProgramManagementOverviewDto>.Success(overview);
    }

    private async Task<List<ProjectRow>> ProjectRowsAsync(IEnumerable<Guid> programIds, CancellationToken cancellationToken)
    {
        var ids = programIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        return await _db.Set<ProjectEntity>().AsNoTracking()
            .Where(project => project.ProgramId.HasValue && ids.Contains(project.ProgramId.Value))
            .Select(project => new ProjectRow(project.Id, project.ProgramId!.Value, project.Name, project.Code, project.Status, project.Health, project.Priority, project.ProjectManagerId, project.StartDate, project.EndDate, project.Progress, project.Currency, project.Budget, project.ActualCost))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<ScheduleRow>> ScheduleRowsAsync(IEnumerable<Guid> projectIds, CancellationToken cancellationToken)
    {
        var ids = projectIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        return await _db.Set<ProjectSchedule>().AsNoTracking()
            .Where(schedule => ids.Contains(schedule.ProjectId))
            .Select(schedule => new ScheduleRow(schedule.ProjectId, schedule.BaselineEndDate, schedule.ForecastEndDate, schedule.UpdatedAt ?? schedule.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<MilestoneRow>> MilestoneRowsAsync(Guid[] projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0) return [];
        return await _db.Set<Milestone>().AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId))
            .Select(item => new MilestoneRow(item.Id, item.ProjectId, item.Name, item.DueDate, item.CompletedDate, item.Status))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<RiskRow>> RiskRowsAsync(Guid[] projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0) return [];
        return await _db.Set<Risk>().AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId))
            .Select(item => new RiskRow(item.Id, item.ProjectId, item.Title, item.RiskLevel, item.Status, item.IdentifiedDate))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<IssueRow>> IssueRowsAsync(Guid[] projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0) return [];
        return await _db.Set<Issue>().AsNoTracking()
            .Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value))
            .Select(item => new IssueRow(item.Id, item.ProjectId, item.Title, item.Severity, item.Priority, item.Status, item.TargetResolutionDate))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<AllocationRow>> AllocationRowsAsync(Guid[] projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0) return [];
        var now = DateTime.UtcNow;
        return await _db.Set<ResourceAllocation>().AsNoTracking()
            .Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value) && item.Status != ResourceAllocation.StatusCancelled && item.StartDate <= now && item.EndDate >= now)
            .Select(item => new AllocationRow(item.ResourceId, item.ProjectId!.Value, item.AllocationPercentage, item.HoursPerWeek))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<PublishedReportRow>> PublishedStatusReportsAsync(Guid[] projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0) return [];
        return await _db.Set<ProjectStatusReport>().AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId) && item.ReportState == ProjectStatusReportState.Published)
            .Select(item => new PublishedReportRow(item.ProjectId, item.ReportDate, item.OverallHealth, item.OverallProgressPercent))
            .ToListAsync(cancellationToken);
    }

    private async Task<ProgramFinancialSummaryDto> BuildFinancialSummaryAsync(ProgramEntity program, IReadOnlyList<ProjectRow> projects, CancellationToken cancellationToken)
    {
        var projectIds = projects.Select(project => project.Id).ToArray();
        if (projectIds.Length == 0)
            return new ProgramFinancialSummaryDto { BaseCurrency = program.Currency };

        var budgets = await _db.Set<Budget>().AsNoTracking()
            .Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value))
            .Select(item => new MoneyRow(item.ProjectId!.Value, item.Currency, item.ApprovedAmount, item.ActualAmount, item.CommittedAmount, item.ForecastAmount))
            .ToListAsync(cancellationToken);
        var costs = await _db.Set<CostEntry>().AsNoTracking()
            .Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value))
            .GroupBy(item => new { ProjectId = item.ProjectId!.Value, item.Currency })
            .Select(group => new MoneyRow(group.Key.ProjectId, group.Key.Currency, 0m, group.Sum(item => item.Amount), 0m, 0m))
            .ToListAsync(cancellationToken);
        var commitments = await _db.Set<PurchaseCommitment>().AsNoTracking()
            .Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value))
            .GroupBy(item => new { ProjectId = item.ProjectId!.Value, item.Currency })
            .Select(group => new MoneyRow(group.Key.ProjectId, group.Key.Currency, 0m, 0m, group.Sum(item => item.Amount), 0m))
            .ToListAsync(cancellationToken);

        var values = new Dictionary<(Guid ProjectId, string Currency), FinancialAccumulator>();
        foreach (var project in projects)
        {
            var currency = NormalizeCurrency(project.Currency, program.Currency);
            values[(project.Id, currency)] = new FinancialAccumulator(project.Id, project.Name, currency, project.Budget ?? 0m, project.ActualCost ?? 0m, 0m, project.Budget ?? 0m);
        }

        foreach (var group in budgets.GroupBy(item => (item.ProjectId, Currency: NormalizeCurrency(item.Currency, program.Currency))))
        {
            var projectName = projects.FirstOrDefault(project => project.Id == group.Key.ProjectId)?.Name ?? "";
            values[group.Key] = new FinancialAccumulator(group.Key.ProjectId, projectName, group.Key.Currency, group.Sum(item => item.Budget), group.Sum(item => item.Actual), group.Sum(item => item.Committed), group.Sum(item => item.Forecast));
        }
        foreach (var group in costs.GroupBy(item => (item.ProjectId, Currency: NormalizeCurrency(item.Currency, program.Currency))))
        {
            if (!values.TryGetValue(group.Key, out var value)) continue;
            if (!budgets.Any(item => item.ProjectId == group.Key.ProjectId && NormalizeCurrency(item.Currency, program.Currency) == group.Key.Currency)) value.Actual = group.Sum(item => item.Actual);
        }
        foreach (var group in commitments.GroupBy(item => (item.ProjectId, Currency: NormalizeCurrency(item.Currency, program.Currency))))
        {
            if (values.TryGetValue(group.Key, out var value) && !budgets.Any(item => item.ProjectId == group.Key.ProjectId && NormalizeCurrency(item.Currency, program.Currency) == group.Key.Currency)) value.Committed = group.Sum(item => item.Committed);
        }

        var groups = values.Values.GroupBy(value => value.Currency, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => string.Equals(group.Key, program.Currency, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(group => group.Key)
            .Select(group => new ProgramFinancialCurrencyGroupDto
            {
                Currency = group.Key,
                IsBaseCurrency = string.Equals(group.Key, program.Currency, StringComparison.OrdinalIgnoreCase),
                Budget = group.Sum(value => value.Budget),
                Actual = group.Sum(value => value.Actual),
                Committed = group.Sum(value => value.Committed),
                Forecast = group.Sum(value => value.Forecast),
                Remaining = group.Sum(value => value.Budget) - group.Sum(value => value.Actual) - group.Sum(value => value.Committed),
                Projects = group.OrderByDescending(value => value.Budget).Take(25).Select(value => new ProgramFinancialProjectDto
                {
                    ProjectId = value.ProjectId,
                    ProjectName = value.ProjectName,
                    Budget = value.Budget,
                    Actual = value.Actual,
                    Committed = value.Committed,
                    Forecast = value.Forecast
                }).ToList()
            }).ToList();

        return new ProgramFinancialSummaryDto
        {
            BaseCurrency = program.Currency,
            HasMixedCurrencies = groups.Select(group => group.Currency).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1,
            Groups = groups
        };
    }

    private async Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var values = ids.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (values.Length == 0) return new Dictionary<Guid, string>();
        return await _db.Set<User>().AsNoTracking().Where(user => values.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> PortfolioNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var values = ids.Distinct().ToArray();
        if (values.Length == 0) return new Dictionary<Guid, string>();
        return await _db.Set<Portfolio>().AsNoTracking().Where(portfolio => values.Contains(portfolio.Id)).ToDictionaryAsync(portfolio => portfolio.Id, portfolio => portfolio.Name, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> UserResourceNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var values = ids.Distinct().ToArray();
        if (values.Length == 0) return new Dictionary<Guid, string>();
        return await _db.Set<Resource>().AsNoTracking().Where(resource => values.Contains(resource.Id)).ToDictionaryAsync(resource => resource.Id, resource => resource.Name, cancellationToken);
    }

    private async Task<IReadOnlyList<ProgramMeetingDto>> UpcomingMeetingsAsync(Guid[] projectIds, IReadOnlyList<ProjectRow> projects, DateTime now, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0) return [];
        var names = projects.ToDictionary(project => project.Id, project => project.Name);
        var meetings = await _db.Set<Meeting>().AsNoTracking()
            .Where(meeting => meeting.ProjectId.HasValue && projectIds.Contains(meeting.ProjectId.Value) && meeting.EndTime >= now)
            .OrderBy(meeting => meeting.StartTime).Take(8)
            .Select(meeting => new ProgramMeetingDto { Id = meeting.Id, ProjectId = meeting.ProjectId, Title = meeting.Title, StartTime = meeting.StartTime, EndTime = meeting.EndTime })
            .ToListAsync(cancellationToken);
        foreach (var meeting in meetings) if (meeting.ProjectId.HasValue) meeting.ProjectName = names.GetValueOrDefault(meeting.ProjectId.Value);
        return meetings;
    }

    private static ProgramResourceSummaryDto BuildResourceSummary(IReadOnlyList<AllocationRow> allocations, IReadOnlyDictionary<Guid, string> resourceNames, IReadOnlyList<ProjectRow> projects)
    {
        var projectNames = projects.ToDictionary(project => project.Id, project => project.Name);
        var byResource = allocations.GroupBy(item => item.ResourceId).Select(group => new
        {
            ResourceId = group.Key,
            Percentage = group.Sum(item => item.AllocationPercentage),
            Projects = group.GroupBy(item => item.ProjectId).Select(projectGroup => new ProgramResourceProjectDto
            {
                ProjectId = projectGroup.Key,
                ProjectName = projectNames.GetValueOrDefault(projectGroup.Key, ""),
                AllocationPercentage = projectGroup.Sum(item => item.AllocationPercentage)
            }).OrderByDescending(item => item.AllocationPercentage).ToList()
        }).ToList();

        return new ProgramResourceSummaryDto
        {
            ActiveAllocationCount = allocations.Count,
            ActiveResourceCount = byResource.Count,
            SharedResourceCount = byResource.Count(item => item.Projects.Count > 1),
            OverAllocatedResourceCount = byResource.Count(item => item.Percentage > 100m),
            AllocatedHoursPerWeek = allocations.Sum(item => item.HoursPerWeek ?? 0m),
            Pressure = byResource.Where(item => item.Percentage >= 80m).OrderByDescending(item => item.Percentage).Take(10).Select(item => new ProgramResourcePressureDto
            {
                ResourceId = item.ResourceId,
                ResourceName = resourceNames.GetValueOrDefault(item.ResourceId, ""),
                AllocationPercentage = item.Percentage,
                ProjectCount = item.Projects.Count,
                Projects = item.Projects
            }).ToList()
        };
    }

    private static IReadOnlyList<ProgramAttentionItemDto> BuildAttentionItems(IReadOnlyList<ProgramProjectManagementDto> projects, IReadOnlyList<MilestoneRow> milestones, IReadOnlyList<RiskRow> risks, IReadOnlyList<IssueRow> overdueIssues, IReadOnlyList<ChangeRequestRow> changeRequests, IReadOnlyDictionary<Guid, PublishedReportRow> latestReports, IReadOnlyList<ProjectRow> allProjects, DateTime now)
    {
        var projectNames = allProjects.ToDictionary(project => project.Id, project => project.Name);
        var result = new List<ProgramAttentionItemDto>();
        foreach (var project in projects.Where(project => project.RequiresAttention).Take(8)) result.Add(new ProgramAttentionItemDto { Level = project.Health.Equals("Red", StringComparison.OrdinalIgnoreCase) ? "Critical" : "NeedsAttention", Kind = "Project", Message = project.Name, ProjectId = project.Id });
        foreach (var milestone in milestones.Where(item => MilestoneState(item, now) == "Overdue").Take(6)) result.Add(new ProgramAttentionItemDto { Level = "Critical", Kind = "Milestone", Message = milestone.Name, ItemId = milestone.Id, ProjectId = milestone.ProjectId });
        foreach (var risk in risks.Where(IsOpenRisk).OrderByDescending(item => RiskRank(item.RiskLevel)).Take(6)) result.Add(new ProgramAttentionItemDto { Level = risk.RiskLevel == RiskLevel.Critical ? "Critical" : "NeedsAttention", Kind = "Risk", Message = risk.Title, ItemId = risk.Id, ProjectId = risk.ProjectId });
        foreach (var issue in overdueIssues.Take(6)) result.Add(new ProgramAttentionItemDto { Level = "Critical", Kind = "Issue", Message = issue.Title, ItemId = issue.Id, ProjectId = issue.ProjectId });
        foreach (var request in changeRequests.Take(4)) result.Add(new ProgramAttentionItemDto { Level = "NeedsAttention", Kind = "ChangeRequest", Message = request.Impact ?? "Pending change request", ItemId = request.Id, ProjectId = request.ProjectId });
        foreach (var project in allProjects.Where(project => !latestReports.ContainsKey(project.Id)).Take(4)) result.Add(new ProgramAttentionItemDto { Level = "Informational", Kind = "StatusReport", Message = projectNames.GetValueOrDefault(project.Id, project.Name), ProjectId = project.Id });
        return result.Take(20).ToList();
    }

    private static ProgramMilestoneDto ToMilestoneDto(MilestoneRow milestone, IReadOnlyList<ProjectRow> projects) => new()
    {
        Id = milestone.Id,
        ProjectId = milestone.ProjectId,
        ProjectName = projects.FirstOrDefault(project => project.Id == milestone.ProjectId)?.Name ?? "",
        Name = milestone.Name,
        DueDate = milestone.DueDate,
        CompletedDate = milestone.CompletedDate,
        State = MilestoneState(milestone, DateTime.UtcNow),
        Status = milestone.Status
    };

    private static ProgramRiskDto ToRiskDto(RiskRow risk, IReadOnlyList<ProjectRow> projects) => new()
    {
        Id = risk.Id,
        ProjectId = risk.ProjectId,
        ProjectName = projects.FirstOrDefault(project => project.Id == risk.ProjectId)?.Name ?? "",
        Title = risk.Title,
        Severity = risk.RiskLevel.ToString(),
        Status = risk.Status.ToString(),
        IdentifiedDate = risk.IdentifiedDate
    };

    private static ProgramIssueDto ToIssueDto(IssueRow issue, IReadOnlyList<ProjectRow> projects) => new()
    {
        Id = issue.Id,
        ProjectId = issue.ProjectId ?? Guid.Empty,
        ProjectName = issue.ProjectId.HasValue ? projects.FirstOrDefault(project => project.Id == issue.ProjectId.Value)?.Name ?? "" : "",
        Title = issue.Title,
        Severity = issue.Severity,
        Priority = issue.Priority,
        Status = issue.Status,
        TargetResolutionDate = issue.TargetResolutionDate
    };

    private static bool ProjectNeedsAttention(ProjectRow project) => string.Equals(project.Health, "Red", StringComparison.OrdinalIgnoreCase) || project.Status.Contains("risk", StringComparison.OrdinalIgnoreCase) || project.Status.Contains("delay", StringComparison.OrdinalIgnoreCase) || string.Equals(project.Status, "OnHold", StringComparison.OrdinalIgnoreCase);
    private static bool IsCriticalProject(ProjectRow project) => string.Equals(project.Priority, "Critical", StringComparison.OrdinalIgnoreCase) || string.Equals(project.Health, "Red", StringComparison.OrdinalIgnoreCase);
    private static bool IsActiveStatus(string status) => !IsCompletedStatus(status) && !string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) && !string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase);
    private static bool IsCompletedStatus(string status) => string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Done", StringComparison.OrdinalIgnoreCase);
    private static bool IsOpenRisk(RiskRow risk) => risk.Status is not RiskStatus.Mitigated and not RiskStatus.Closed;
    private static bool IsOpenIssue(IssueRow issue) => !string.Equals(issue.Status, "Closed", StringComparison.OrdinalIgnoreCase) && !string.Equals(issue.Status, "Resolved", StringComparison.OrdinalIgnoreCase) && !string.Equals(issue.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);
    private static bool IsDelayed(ScheduleRow schedule) => schedule.BaselineEndDate.HasValue && schedule.ForecastEndDate.HasValue && schedule.ForecastEndDate > schedule.BaselineEndDate;
    private static string ScheduleIndicator(IReadOnlyList<ProjectRow> projects, IReadOnlyList<ScheduleRow> schedules)
    {
        var comparableSchedules = schedules.Where(schedule => schedule.BaselineEndDate.HasValue && schedule.ForecastEndDate.HasValue).ToList();
        return comparableSchedules.Count == 0 ? "Unknown" : comparableSchedules.Any(IsDelayed) ? "Delayed" : projects.Count > 0 ? "OnTrack" : "Unknown";
    }
    private static string ScheduleCondition(ScheduleRow? schedule) => schedule is null || !schedule.BaselineEndDate.HasValue || !schedule.ForecastEndDate.HasValue ? "Unknown" : IsDelayed(schedule) ? "Delayed" : "OnTrack";
    private static string ReportFreshness(DateTime reportDate, DateTime now) => ManagementReportingRules.Freshness(reportDate, now);
    private static string NormalizeCurrency(string? currency, string fallback) => string.IsNullOrWhiteSpace(currency) ? fallback.ToUpperInvariant() : currency.Trim().ToUpperInvariant();
    private static int RiskRank(RiskLevel level) => level switch { RiskLevel.Critical => 4, RiskLevel.High => 3, RiskLevel.Medium => 2, _ => 1 };
    private static int SeverityRank(string? severity) => severity?.ToLowerInvariant() switch { "critical" => 5, "high" => 4, "major" => 3, "medium" => 2, _ => 1 };
    private static string MilestoneState(MilestoneRow milestone, DateTime now)
    {
        if (milestone.CompletedDate.HasValue || string.Equals(milestone.Status, "Completed", StringComparison.OrdinalIgnoreCase) || string.Equals(milestone.Status, "Done", StringComparison.OrdinalIgnoreCase)) return "Completed";
        if (milestone.DueDate.HasValue && milestone.DueDate.Value < now) return "Overdue";
        if (milestone.DueDate.HasValue && milestone.DueDate.Value <= now.AddDays(30)) return "DueSoon";
        return "OnTrack";
    }
    private static int MilestoneSort(MilestoneRow milestone, DateTime now) => MilestoneState(milestone, now) switch { "Overdue" => 0, "DueSoon" => 1, "OnTrack" => 2, _ => 3 };

    private sealed record ProjectRow(Guid Id, Guid ProgramId, string Name, string Code, string Status, string Health, string Priority, Guid? ProjectManagerId, DateTime? StartDate, DateTime? EndDate, decimal Progress, string Currency, decimal? Budget, decimal? ActualCost);
    private sealed record ScheduleRow(Guid ProjectId, DateTime? BaselineEndDate, DateTime? ForecastEndDate, DateTime UpdatedAt);
    private sealed record MilestoneRow(Guid Id, Guid ProjectId, string Name, DateTime? DueDate, DateTime? CompletedDate, string? Status);
    private sealed record RiskRow(Guid Id, Guid ProjectId, string Title, RiskLevel RiskLevel, RiskStatus Status, DateTime? IdentifiedDate);
    private sealed record IssueRow(Guid Id, Guid? ProjectId, string Title, string Severity, string Priority, string Status, DateTime? TargetResolutionDate);
    private sealed record ChangeRequestRow(Guid Id, Guid ProjectId, string? Impact);
    private sealed record AllocationRow(Guid ResourceId, Guid ProjectId, decimal AllocationPercentage, decimal? HoursPerWeek);
    private sealed record PublishedReportRow(Guid ProjectId, DateTime ReportDate, string? Health, decimal? Progress);
    private sealed record MoneyRow(Guid ProjectId, string Currency, decimal Budget, decimal Actual, decimal Committed, decimal Forecast);

    private sealed class FinancialAccumulator(Guid projectId, string projectName, string currency, decimal budget, decimal actual, decimal committed, decimal forecast)
    {
        public Guid ProjectId { get; } = projectId;
        public string ProjectName { get; } = projectName;
        public string Currency { get; } = currency;
        public decimal Budget { get; set; } = budget;
        public decimal Actual { get; set; } = actual;
        public decimal Committed { get; set; } = committed;
        public decimal Forecast { get; set; } = forecast;
    }
}
