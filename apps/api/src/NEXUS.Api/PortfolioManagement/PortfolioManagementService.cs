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
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;
using ProjectEntity = NEXUS.Modules.ProjectManagement.Domain.Project;

namespace NEXUS.Api.PortfolioManagement;

public sealed class PortfolioManagementService : IPortfolioManagementService
{
    private const int TopItems = 12;
    private readonly DbContext _db;
    private readonly IManagementContextAccessor _managementContext;

    public PortfolioManagementService(DbContext db, IManagementContextAccessor managementContext)
    {
        _db = db;
        _managementContext = managementContext;
    }

    public async Task<PagedResult<PortfolioManagementListItemDto>> GetPortfoliosAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var context = _managementContext.Current;
        var canReadTenantData = context?.CanReadAllTenantPortfolioProgramData == true;
        if (!canReadTenantData && context is not null && (context.SelectedPerspective != ManagementPerspective.PortfolioManager || context.Scope.PortfolioIds.Count == 0))
            return new PagedResult<PortfolioManagementListItemDto>([], page, pageSize, 0);

        // The controller requires Portfolio.View. Tenant administrators receive
        // tenant-wide read scope here; other users retain the selected manager
        // scope. The authenticated tenant filter remains active in both cases.
        var query = _db.Set<PortfolioEntity>().AsNoTracking();
        if (canReadTenantData)
        {
            query = query.Where(item => item.TenantId == context!.TenantId);
        }
        else if (context is not null)
        {
            var scopedPortfolioIds = context.Scope.PortfolioIds.ToArray();
            query = query.Where(item => scopedPortfolioIds.Contains(item.Id));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Name.Contains(term) || (item.Code != null && item.Code.Contains(term)) || (item.Description != null && item.Description.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var portfolios = await query.OrderBy(item => item.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        if (portfolios.Count == 0) return new PagedResult<PortfolioManagementListItemDto>([], page, pageSize, total);

        var portfolioIds = portfolios.Select(item => item.Id).ToArray();
        var readScope = canReadTenantData ? null : context;
        var scopedProgramIds = await ProgramIdsForPortfoliosAsync(portfolioIds, readScope, cancellationToken);
        var scopedProjectIds = await ProjectIdsForPortfoliosAsync(portfolioIds, scopedProgramIds, readScope, cancellationToken);
        var programs = await _db.Set<ProgramEntity>().AsNoTracking()
            .Where(item => portfolioIds.Contains(item.PortfolioId) && scopedProgramIds.Contains(item.Id))
            .Select(item => new ProgramRow(item.Id, item.PortfolioId, item.Name, item.Code, item.ProgramManagerId, item.Status, item.Health, item.Currency, item.StartDate, item.EndDate))
            .ToListAsync(cancellationToken);
        var programById = programs.ToDictionary(item => item.Id);
        var projects = await ProjectRowsAsync(portfolioIds, scopedProjectIds, programById.ToDictionary(item => item.Key, item => item.Value.PortfolioId), cancellationToken);
        var projectIds = projects.Select(item => item.Id).ToArray();
        var schedules = await ScheduleRowsAsync(projectIds, cancellationToken);
        var projectById = projects.ToDictionary(item => item.Id);
        var risks = await RiskRowsAsync(projectIds, projectById, programById, cancellationToken);
        var reports = await PublishedReportRowsAsync(projectIds, cancellationToken);
        var budgetProjectIds = await _db.Set<Budget>().AsNoTracking().Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value)).Select(item => item.ProjectId!.Value).Distinct().ToListAsync(cancellationToken);
        var managers = await UserNamesAsync(portfolios.Select(item => item.OwnerId).Concat(programs.Select(item => item.ManagerId)), cancellationToken);
        var now = DateTime.UtcNow;

        var items = portfolios.Select(portfolio =>
        {
            var children = projects.Where(project => project.PortfolioId == portfolio.Id).ToList();
            var childIds = children.Select(project => project.Id).ToHashSet();
            var childPrograms = programs.Where(program => program.PortfolioId == portfolio.Id).ToList();
            var childRisks = risks.Where(risk => childIds.Contains(risk.ProjectId) && IsOpenRisk(risk)).ToList();
            var childReports = reports.Where(report => childIds.Contains(report.ProjectId)).GroupBy(report => report.ProjectId).ToDictionary(group => group.Key, group => group.OrderByDescending(report => report.ReportDate).First());
            var missingReports = children.Count(project => !childReports.ContainsKey(project.Id));
            var staleReports = childReports.Values.Count(report => ManagementReportingRules.Freshness(report.ReportDate, now) == "Stale");
            var projectsAtRisk = children.Count(project => IsProjectAttention(project, schedules, risks, [], childReports, now));
            var attentionPrograms = childPrograms.Count(program => IsProgramAttention(program, children, schedules, risks));
            return new PortfolioManagementListItemDto
            {
                Id = portfolio.Id,
                Name = portfolio.Name,
                Code = portfolio.Code,
                OwnerName = managers.GetValueOrDefault(portfolio.OwnerId ?? Guid.Empty),
                Status = portfolio.Status,
                Health = string.IsNullOrWhiteSpace(portfolio.Health) ? "Unknown" : portfolio.Health,
                Currency = NormalizeCurrency(portfolio.Currency, "USD"),
                ProgramCount = childPrograms.Count,
                DirectProjectCount = children.Count(project => project.IsDirect),
                TotalProjectCount = children.Count,
                ProjectsRequiringAttention = projectsAtRisk,
                ProgramsRequiringAttention = attentionPrograms,
                FinancialIndicator = children.Any(project => project.Budget.HasValue || project.ActualCost.HasValue) || children.Any(project => budgetProjectIds.Contains(project.Id)) ? "Available" : "Unavailable",
                RiskIndicator = childRisks.Any(risk => risk.RiskLevel == RiskLevel.Critical) ? "Critical" : childRisks.Count > 0 ? "NeedsAttention" : "Clear",
                ReportingIndicator = children.Count == 0 || missingReports > 0 ? "Missing" : staleReports > 0 ? "Stale" : "Current"
            };
        }).ToList();

        return new PagedResult<PortfolioManagementListItemDto>(items, page, pageSize, total);
    }

    public async Task<Result<PortfolioManagementOverviewDto>> GetOverviewAsync(Guid portfolioId, int programsPage, int projectsPage, int pageSize, CancellationToken cancellationToken = default)
    {
        programsPage = Math.Max(programsPage, 1);
        projectsPage = Math.Max(projectsPage, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var context = _managementContext.Current;
        var canReadTenantData = context?.CanReadAllTenantPortfolioProgramData == true;
        if (!canReadTenantData && context is not null && (context.SelectedPerspective != ManagementPerspective.PortfolioManager || !context.Scope.PortfolioIds.Contains(portfolioId)))
            return Result<PortfolioManagementOverviewDto>.Failure(Error.NotFound("Portfolio not found"));

        var portfolioQuery = _db.Set<PortfolioEntity>().AsNoTracking().Where(item => item.Id == portfolioId);
        if (canReadTenantData) portfolioQuery = portfolioQuery.Where(item => item.TenantId == context!.TenantId);
        var portfolio = await portfolioQuery.FirstOrDefaultAsync(cancellationToken);
        if (portfolio is null) return Result<PortfolioManagementOverviewDto>.Failure(Error.NotFound("Portfolio not found"));

        var portfolioIds = new[] { portfolioId };
        var readScope = canReadTenantData ? null : context;
        var scopedProgramIds = await ProgramIdsForPortfoliosAsync(portfolioIds, readScope, cancellationToken);
        var scopedProjectIds = await ProjectIdsForPortfoliosAsync(portfolioIds, scopedProgramIds, readScope, cancellationToken);
        var programs = await _db.Set<ProgramEntity>().AsNoTracking()
            .Where(item => item.PortfolioId == portfolioId && scopedProgramIds.Contains(item.Id))
            .Select(item => new ProgramRow(item.Id, item.PortfolioId, item.Name, item.Code, item.ProgramManagerId, item.Status, item.Health, item.Currency, item.StartDate, item.EndDate))
            .ToListAsync(cancellationToken);
        var programById = programs.ToDictionary(item => item.Id);
        var projects = await ProjectRowsAsync(portfolioIds, scopedProjectIds, programById.ToDictionary(item => item.Key, item => item.Value.PortfolioId), cancellationToken);
        var projectIds = projects.Select(item => item.Id).ToArray();
        var managers = await UserNamesAsync(new[] { portfolio.OwnerId }.Concat(programs.Select(item => item.ManagerId)).Concat(projects.Select(item => item.ManagerId)), cancellationToken);
        var schedules = await ScheduleRowsAsync(projectIds, cancellationToken);
        var milestones = await MilestoneRowsAsync(projectIds, cancellationToken);
        var projectById = projects.ToDictionary(item => item.Id);
        var risks = await RiskRowsAsync(projectIds, projectById, programById, cancellationToken);
        var issues = await IssueRowsAsync(projectIds, projectById, programById, cancellationToken);
        var changeRequests = await _db.Set<ProjectChangeRequest>().AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId) && item.Status == ProjectChangeRequest.Statuses.Pending)
            .Select(item => new ChangeRequestRow(item.Id, item.ProjectId, item.Impact, item.BudgetChangeAmount))
            .ToListAsync(cancellationToken);
        var allocations = await AllocationRowsAsync(projectIds, cancellationToken);
        var resources = await ResourceNamesAsync(allocations.Select(item => item.ResourceId), cancellationToken);
        var reports = await PublishedReportRowsAsync(projectIds, cancellationToken);
        var financials = await BuildFinancialSummaryAsync(portfolio, projects, programById, cancellationToken);
        var now = DateTime.UtcNow;

        var latestReports = reports.GroupBy(item => item.ProjectId).ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.ReportDate).First());
        var openRisks = risks.Where(IsOpenRisk).ToList();
        var openIssues = issues.Where(IsOpenIssue).ToList();
        var overdueIssues = openIssues.Where(item => item.TargetResolutionDate.HasValue && item.TargetResolutionDate.Value < now).ToList();
        var openRiskCounts = openRisks.GroupBy(item => item.ProjectId).ToDictionary(group => group.Key, group => group.Count());
        var financialKeys = financials.Groups.SelectMany(group => group.Contributions.Select(item => (item.ProjectId, group.Currency))).ToHashSet();

        var projectViews = projects.Select(project =>
        {
            var schedule = schedules.Where(item => item.ProjectId == project.Id).OrderByDescending(item => item.UpdatedAt).FirstOrDefault();
            var report = latestReports.GetValueOrDefault(project.Id);
            var scheduleCondition = ScheduleCondition(schedule);
            var riskCondition = openRiskCounts.GetValueOrDefault(project.Id) > 0 ? openRisks.Any(item => item.ProjectId == project.Id && item.RiskLevel == RiskLevel.Critical) ? "Critical" : "NeedsAttention" : "Clear";
            var requiresAttention = IsProjectAttention(project, schedules, risks, issues, latestReports, now) || changeRequests.Any(item => item.ProjectId == project.Id);
            var currency = NormalizeCurrency(project.Currency, portfolio.Currency);
            return new PortfolioProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Code = project.Code,
                ProgramId = project.ProgramId,
                ProgramName = project.ProgramId.HasValue ? programById.GetValueOrDefault(project.ProgramId.Value)?.Name : null,
                IsDirectProject = project.IsDirect,
                ProjectManagerName = managers.GetValueOrDefault(project.ManagerId ?? Guid.Empty),
                Status = project.Status,
                Health = string.IsNullOrWhiteSpace(project.Health) ? "Unknown" : project.Health,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Progress = project.Progress,
                ScheduleCondition = scheduleCondition,
                Currency = currency,
                FinancialCondition = financialKeys.Contains((project.Id, currency)) ? project.Budget.HasValue || project.ActualCost.HasValue ? "Available" : "Reported" : "Unavailable",
                RiskCondition = riskCondition,
                StatusReportFreshness = report is null ? "Missing" : ManagementReportingRules.Freshness(report.ReportDate, now),
                LatestStatusReportDate = report?.ReportDate,
                RequiresAttention = requiresAttention
            };
        }).OrderByDescending(item => item.RequiresAttention).ThenBy(item => item.Name).ToList();

        var programViews = programs.Select(program =>
        {
            var childProjects = projects.Where(project => project.ProgramId == program.Id).ToList();
            var childViews = projectViews.Where(project => project.ProgramId == program.Id).ToList();
            return new PortfolioProgramDto
            {
                Id = program.Id,
                Name = program.Name,
                Code = program.Code,
                ProgramManagerName = managers.GetValueOrDefault(program.ManagerId ?? Guid.Empty),
                Status = program.Status,
                Health = string.IsNullOrWhiteSpace(program.Health) ? "Unknown" : program.Health,
                StartDate = program.StartDate,
                EndDate = program.EndDate,
                Currency = NormalizeCurrency(program.Currency, portfolio.Currency),
                ProjectCount = childProjects.Count,
                ProjectsAtRisk = childViews.Count(item => item.RequiresAttention),
                DeliveryIndicator = ProgramDeliveryIndicator(childProjects, schedules),
                FinancialIndicator = childProjects.Any(item => item.Budget.HasValue || item.ActualCost.HasValue) ? "Available" : "Unavailable",
                RiskIndicator = childViews.Any(item => item.RiskCondition == "Critical") ? "Critical" : childViews.Any(item => item.RiskCondition == "NeedsAttention") ? "NeedsAttention" : "Clear",
                ReportingIndicator = childViews.Count == 0 || childViews.Any(item => item.StatusReportFreshness == "Missing") ? "Missing" : childViews.Any(item => item.StatusReportFreshness == "Stale") ? "Stale" : "Current"
            };
        }).OrderByDescending(item => item.ProjectsAtRisk).ThenBy(item => item.Name).ToList();

        var pagedPrograms = programViews.Skip((programsPage - 1) * pageSize).Take(pageSize).ToList();
        var pagedProjects = projectViews.Skip((projectsPage - 1) * pageSize).Take(pageSize).ToList();
        var projectHealth = projectViews.GroupBy(item => string.IsNullOrWhiteSpace(item.Health) ? "Unknown" : item.Health).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var programHealth = programViews.GroupBy(item => string.IsNullOrWhiteSpace(item.Health) ? "Unknown" : item.Health).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var delivery = new PortfolioDeliverySummaryDto
        {
            ProgramDeliveryConditions = programViews.GroupBy(item => item.DeliveryIndicator).ToDictionary(group => group.Key, group => group.Count()),
            ProjectScheduleConditions = projectViews.GroupBy(item => item.ScheduleCondition).ToDictionary(group => group.Key, group => group.Count()),
            DelayedProjects = projectViews.Count(item => item.ScheduleCondition == "Delayed"),
            DelayedPrograms = programViews.Count(item => item.DeliveryIndicator == "Delayed"),
            ProjectsWithScheduleData = projectViews.Count(item => item.ScheduleCondition != "Unknown"),
            TotalMilestones = milestones.Count,
            OverdueMilestones = milestones.Count(item => MilestoneState(item, now) == "Overdue"),
            DueSoonMilestones = milestones.Count(item => MilestoneState(item, now) == "DueSoon"),
            OnTrackMilestones = milestones.Count(item => MilestoneState(item, now) == "OnTrack"),
            CompletedMilestones = milestones.Count(item => MilestoneState(item, now) == "Completed"),
            ProjectProgressDistribution = projectViews.GroupBy(item => ProgressBucket(item.Progress)).ToDictionary(group => group.Key, group => group.Count())
        };

        var reportViews = projectViews.Select(project => latestReports.GetValueOrDefault(project.Id) is { } report
            ? new PortfolioStatusReportDto { ProjectId = project.Id, ProjectName = project.Name, ProgramId = project.ProgramId, ProgramName = project.ProgramName, ReportDate = report.ReportDate, Health = report.Health, Progress = report.Progress, Freshness = ManagementReportingRules.Freshness(report.ReportDate, now) }
            : null).Where(item => item is not null).Cast<PortfolioStatusReportDto>().OrderByDescending(item => item.ReportDate).ToList();
        var statusGaps = projectViews.Where(item => item.StatusReportFreshness != "Current").ToList();
        var attention = BuildAttentionItems(projectViews, programViews, milestones, openRisks, overdueIssues, changeRequests, latestReports, programById, now);

        var overview = new PortfolioManagementOverviewDto
        {
            Portfolio = new PortfolioManagementIdentityDto
            {
                Id = portfolio.Id,
                Name = portfolio.Name,
                Code = portfolio.Code,
                Description = portfolio.Description,
                Status = portfolio.Status,
                Health = string.IsNullOrWhiteSpace(portfolio.Health) ? "Unknown" : portfolio.Health,
                OwnerName = managers.GetValueOrDefault(portfolio.OwnerId ?? Guid.Empty),
                Currency = NormalizeCurrency(portfolio.Currency, "USD"),
                StrategicObjective = portfolio.StrategicObjective,
                Priority = portfolio.Priority,
                ConfidenceLevel = portfolio.ConfidenceLevel,
                StartDate = portfolio.StartDate,
                EndDate = portfolio.EndDate
            },
            Summary = new PortfolioStructureSummaryDto
            {
                TotalPrograms = programs.Count,
                TotalProjects = projects.Count,
                ProgramContainedProjects = projects.Count(item => !item.IsDirect),
                DirectPortfolioProjects = projects.Count(item => item.IsDirect),
                ActiveProjects = projects.Count(item => IsActiveStatus(item.Status)),
                CompletedProjects = projects.Count(item => IsCompletedStatus(item.Status)),
                AtRiskProjects = projectViews.Count(item => item.RequiresAttention),
                CriticalProjects = projectViews.Count(item => IsCriticalProject(item)),
                AverageProjectProgress = projects.Count == 0 ? null : Math.Round(projects.Average(item => item.Progress), 2),
                ProgramHealthDistribution = programHealth,
                ProjectHealthDistribution = projectHealth
            },
            Delivery = delivery,
            Financials = financials,
            Risks = new PortfolioRiskSummaryDto
            {
                SourceScope = "Project",
                OpenCount = openRisks.Count,
                CriticalCount = openRisks.Count(item => item.RiskLevel == RiskLevel.Critical),
                HighCount = openRisks.Count(item => item.RiskLevel == RiskLevel.High),
                OverdueCount = 0,
                ByProgram = openRisks.GroupBy(item => item.ProgramId.HasValue ? item.ProgramName ?? "Unknown Program" : "Direct Projects").ToDictionary(group => group.Key, group => group.Count()),
                ByProject = openRisks.GroupBy(item => item.ProjectName).ToDictionary(group => group.Key, group => group.Count())
            },
            Issues = new PortfolioIssueSummaryDto
            {
                OpenCount = openIssues.Count,
                CriticalCount = openIssues.Count(item => SeverityRank(item.Severity) >= 5),
                OverdueCount = overdueIssues.Count,
                ByProgram = openIssues.GroupBy(item => item.ProgramId.HasValue ? item.ProgramName ?? "Unknown Program" : "Direct Projects").ToDictionary(group => group.Key, group => group.Count()),
                ByProject = openIssues.GroupBy(item => item.ProjectName).ToDictionary(group => group.Key, group => group.Count())
            },
            Resources = BuildResourceSummary(allocations, resources, projects, programById),
            StatusReports = new PortfolioStatusReportSummaryDto
            {
                PublishedProjectCount = reportViews.Count,
                CurrentCount = projectViews.Count(item => item.StatusReportFreshness == "Current"),
                StaleCount = projectViews.Count(item => item.StatusReportFreshness == "Stale"),
                MissingCount = projectViews.Count(item => item.StatusReportFreshness == "Missing"),
                FreshnessDays = ManagementReportingRules.StatusReportFreshnessDays,
                ProgramsWithReportingGaps = programViews.Where(item => item.ReportingIndicator != "Current").Take(20).Select(item => item.Name).ToList(),
                ProjectsWithReportingGaps = statusGaps.Take(20).Select(item => item.Name).ToList(),
                LatestPublished = reportViews.Take(20).ToList()
            },
            Programs = pagedPrograms,
            ProgramsPage = programsPage,
            ProgramsPageSize = pageSize,
            ProgramsTotalCount = programViews.Count,
            Projects = pagedProjects,
            ProjectsPage = projectsPage,
            ProjectsPageSize = pageSize,
            ProjectsTotalCount = projectViews.Count,
            Milestones = milestones.OrderBy(item => MilestoneSort(item, now)).ThenBy(item => item.DueDate).Take(20).Select(item => ToMilestoneDto(item, programById, projects)).ToList(),
            TopRisks = openRisks.OrderByDescending(item => RiskRank(item.RiskLevel)).ThenByDescending(item => item.IdentifiedDate).Take(TopItems).Select(item => ToRiskDto(item)).ToList(),
            TopIssues = openIssues.OrderByDescending(item => SeverityRank(item.Severity)).ThenBy(item => item.TargetResolutionDate).Take(TopItems).Select(item => ToIssueDto(item)).ToList(),
            AttentionItems = attention,
            UpcomingMeetings = await UpcomingMeetingsAsync(projectIds, projects, programById, now, cancellationToken)
        };
        return Result<PortfolioManagementOverviewDto>.Success(overview);
    }

    private async Task<List<ProjectRow>> ProjectRowsAsync(Guid[] portfolioIds, IReadOnlySet<Guid> scopedProjectIds, IReadOnlyDictionary<Guid, Guid> programPortfolios, CancellationToken cancellationToken)
    {
        var programs = await _db.Set<ProgramEntity>().AsNoTracking().Where(item => portfolioIds.Contains(item.PortfolioId)).Select(item => item.Id).ToListAsync(cancellationToken);
        var rows = await _db.Set<ProjectEntity>().AsNoTracking()
            .Where(project => scopedProjectIds.Contains(project.Id) && ((project.ProgramId.HasValue && programs.Contains(project.ProgramId.Value)) || (!project.ProgramId.HasValue && project.PortfolioId.HasValue && portfolioIds.Contains(project.PortfolioId.Value))))
            .Select(project => new ProjectRow(project.Id, project.ProgramId, project.PortfolioId, project.Name, project.Code, project.Status, project.Health, project.Priority, project.ProjectManagerId, project.StartDate, project.EndDate, project.Progress, project.Currency, project.Budget, project.ActualCost, !project.ProgramId.HasValue))
            .ToListAsync(cancellationToken);
        return rows.Select(project => project with { PortfolioId = project.ProgramId.HasValue ? programPortfolios.GetValueOrDefault(project.ProgramId.Value) : project.PortfolioId }).ToList();
    }

    private async Task<Guid[]> ProgramIdsForPortfoliosAsync(Guid[] portfolioIds, ManagementContext? context, CancellationToken cancellationToken)
    {
        if (context is not null) return context.Scope.ProgramIds.ToArray();

        return await _db.Set<ProgramEntity>().AsNoTracking()
            .Where(program => portfolioIds.Contains(program.PortfolioId))
            .Select(program => program.Id)
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlySet<Guid>> ProjectIdsForPortfoliosAsync(Guid[] portfolioIds, Guid[] programIds, ManagementContext? context, CancellationToken cancellationToken)
    {
        if (context is not null) return context.Scope.ProjectIds;

        return await _db.Set<ProjectEntity>().AsNoTracking()
            .Where(project =>
                (project.ProgramId.HasValue && programIds.Contains(project.ProgramId.Value)) ||
                (!project.ProgramId.HasValue && project.PortfolioId.HasValue && portfolioIds.Contains(project.PortfolioId.Value)))
            .Select(project => project.Id)
            .ToHashSetAsync(cancellationToken);
    }

    private async Task<List<ScheduleRow>> ScheduleRowsAsync(Guid[] projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0) return [];
        return await _db.Set<ProjectSchedule>().AsNoTracking().Where(item => projectIds.Contains(item.ProjectId)).Select(item => new ScheduleRow(item.ProjectId, item.BaselineEndDate, item.ForecastEndDate, item.UpdatedAt ?? item.CreatedAt)).ToListAsync(cancellationToken);
    }

    private async Task<List<MilestoneRow>> MilestoneRowsAsync(Guid[] projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0) return [];
        return await _db.Set<Milestone>().AsNoTracking().Where(item => projectIds.Contains(item.ProjectId)).Select(item => new MilestoneRow(item.Id, item.ProjectId, item.Name, item.DueDate, item.CompletedDate, item.Status)).ToListAsync(cancellationToken);
    }

    private async Task<List<RiskRow>> RiskRowsAsync(Guid[] projectIds, IReadOnlyDictionary<Guid, ProjectRow> projects, IReadOnlyDictionary<Guid, ProgramRow> programs, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0) return [];
        var rows = await _db.Set<Risk>().AsNoTracking().Where(item => projectIds.Contains(item.ProjectId)).Select(item => new RiskRow(item.Id, item.ProjectId, item.Title, item.RiskLevel, item.Status, item.IdentifiedDate)).ToListAsync(cancellationToken);
        return rows.Select(item => { var project = projects.GetValueOrDefault(item.ProjectId); var program = project?.ProgramId is Guid programId ? programs.GetValueOrDefault(programId) : null; return item with { ProjectName = project?.Name ?? "", ProgramId = project?.ProgramId, ProgramName = program?.Name }; }).ToList();
    }

    private async Task<List<IssueRow>> IssueRowsAsync(Guid[] projectIds, IReadOnlyDictionary<Guid, ProjectRow> projects, IReadOnlyDictionary<Guid, ProgramRow> programs, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0) return [];
        var rows = await _db.Set<Issue>().AsNoTracking().Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value)).Select(item => new IssueRow(item.Id, item.ProjectId!.Value, item.Title, item.Severity, item.Priority, item.Status, item.TargetResolutionDate)).ToListAsync(cancellationToken);
        return rows.Select(item => { var project = projects.GetValueOrDefault(item.ProjectId); var program = project?.ProgramId is Guid programId ? programs.GetValueOrDefault(programId) : null; return item with { ProjectName = project?.Name ?? "", ProgramId = project?.ProgramId, ProgramName = program?.Name }; }).ToList();
    }

    private async Task<List<AllocationRow>> AllocationRowsAsync(Guid[] projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0) return [];
        var now = DateTime.UtcNow;
        return await _db.Set<ResourceAllocation>().AsNoTracking().Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value) && item.Status != ResourceAllocation.StatusCancelled && item.StartDate <= now && item.EndDate >= now).Select(item => new AllocationRow(item.ResourceId, item.ProjectId!.Value, item.AllocationPercentage, item.HoursPerWeek)).ToListAsync(cancellationToken);
    }

    private async Task<List<PublishedReportRow>> PublishedReportRowsAsync(Guid[] projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0) return [];
        return await _db.Set<ProjectStatusReport>().AsNoTracking().Where(item => projectIds.Contains(item.ProjectId) && item.ReportState == ProjectStatusReportState.Published).Select(item => new PublishedReportRow(item.ProjectId, item.ReportDate, item.OverallHealth, item.OverallProgressPercent)).ToListAsync(cancellationToken);
    }

    private async Task<PortfolioFinancialSummaryDto> BuildFinancialSummaryAsync(PortfolioEntity portfolio, IReadOnlyList<ProjectRow> projects, IReadOnlyDictionary<Guid, ProgramRow> programs, CancellationToken cancellationToken)
    {
        var projectIds = projects.Select(item => item.Id).ToArray();
        if (projectIds.Length == 0) return new PortfolioFinancialSummaryDto { BaseCurrency = NormalizeCurrency(portfolio.Currency, "USD") };
        var baseCurrency = NormalizeCurrency(portfolio.Currency, "USD");
        var budgets = await _db.Set<Budget>().AsNoTracking().Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value)).Select(item => new MoneyRow(item.ProjectId!.Value, item.Currency, item.ApprovedAmount, item.ApprovedAmount, item.ActualAmount, item.CommittedAmount, item.ForecastAmount)).ToListAsync(cancellationToken);
        var costs = await _db.Set<CostEntry>().AsNoTracking().Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value)).GroupBy(item => new { ProjectId = item.ProjectId!.Value, item.Currency }).Select(group => new MoneyRow(group.Key.ProjectId, group.Key.Currency, 0m, 0m, group.Sum(item => item.Amount), 0m, 0m)).ToListAsync(cancellationToken);
        var commitments = await _db.Set<PurchaseCommitment>().AsNoTracking().Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value)).GroupBy(item => new { ProjectId = item.ProjectId!.Value, item.Currency }).Select(group => new MoneyRow(group.Key.ProjectId, group.Key.Currency, 0m, 0m, 0m, group.Sum(item => item.Amount), 0m)).ToListAsync(cancellationToken);
        var values = new Dictionary<(Guid ProjectId, string Currency), FinancialAccumulator>();
        foreach (var project in projects)
        {
            var currency = NormalizeCurrency(project.Currency, baseCurrency);
            values[(project.Id, currency)] = new FinancialAccumulator(project.Id, project.Name, project.ProgramId, programs.GetValueOrDefault(project.ProgramId ?? Guid.Empty)?.Name, project.IsDirect, currency, project.Budget ?? 0m, project.Budget ?? 0m, project.ActualCost ?? 0m, 0m, project.Budget ?? 0m);
        }
        foreach (var group in budgets.GroupBy(item => (item.ProjectId, Currency: NormalizeCurrency(item.Currency, baseCurrency))))
        {
            var project = projects.FirstOrDefault(item => item.Id == group.Key.ProjectId);
            if (project is null) continue;
            values[group.Key] = new FinancialAccumulator(project.Id, project.Name, project.ProgramId, programs.GetValueOrDefault(project.ProgramId ?? Guid.Empty)?.Name, project.IsDirect, group.Key.Currency, group.Sum(item => item.Budget), group.Sum(item => item.Allocated), group.Sum(item => item.Actual), group.Sum(item => item.Committed), group.Sum(item => item.Forecast));
        }
        foreach (var group in costs.GroupBy(item => (item.ProjectId, Currency: NormalizeCurrency(item.Currency, baseCurrency)))) if (!budgets.Any(item => item.ProjectId == group.Key.ProjectId && NormalizeCurrency(item.Currency, baseCurrency) == group.Key.Currency) && values.TryGetValue(group.Key, out var value)) value.Actual = group.Sum(item => item.Actual);
        foreach (var group in commitments.GroupBy(item => (item.ProjectId, Currency: NormalizeCurrency(item.Currency, baseCurrency)))) if (!budgets.Any(item => item.ProjectId == group.Key.ProjectId && NormalizeCurrency(item.Currency, baseCurrency) == group.Key.Currency) && values.TryGetValue(group.Key, out var value)) value.Committed = group.Sum(item => item.Committed);
        var groups = values.Values.GroupBy(item => item.Currency, StringComparer.OrdinalIgnoreCase).OrderBy(group => string.Equals(group.Key, baseCurrency, StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(group => group.Key).Select(group =>
        {
            var budget = group.Sum(item => item.Budget);
            var actual = group.Sum(item => item.Actual);
            var committed = group.Sum(item => item.Committed);
            var forecast = group.Sum(item => item.Forecast);
            return new PortfolioFinancialCurrencyGroupDto
            {
                Currency = group.Key,
                IsBaseCurrency = string.Equals(group.Key, baseCurrency, StringComparison.OrdinalIgnoreCase),
                ProjectCount = group.Count(),
                Budget = budget,
                Allocated = group.Sum(item => item.Allocated),
                Actual = actual,
                Committed = committed,
                Forecast = forecast,
                Remaining = budget - actual - committed,
                Variance = budget - actual,
                Contributions = group.OrderByDescending(item => item.Budget).Take(25).Select(item => new PortfolioFinancialContributionDto { ProjectId = item.ProjectId, ProjectName = item.ProjectName, ProgramId = item.ProgramId, ProgramName = item.ProgramName, IsDirectProject = item.IsDirect, Budget = item.Budget, Allocated = item.Allocated, Actual = item.Actual, Committed = item.Committed, Forecast = item.Forecast }).ToList()
            };
        }).ToList();
        return new PortfolioFinancialSummaryDto { BaseCurrency = baseCurrency, HasMixedCurrencies = groups.Select(item => item.Currency).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1, Groups = groups };
    }

    private async Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var values = ids.Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
        return values.Length == 0 ? new Dictionary<Guid, string>() : await _db.Set<User>().AsNoTracking().Where(item => values.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.FullName, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResourceNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var values = ids.Distinct().ToArray();
        return values.Length == 0 ? new Dictionary<Guid, string>() : await _db.Set<Resource>().AsNoTracking().Where(item => values.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
    }

    private async Task<IReadOnlyList<PortfolioMeetingDto>> UpcomingMeetingsAsync(Guid[] projectIds, IReadOnlyList<ProjectRow> projects, IReadOnlyDictionary<Guid, ProgramRow> programs, DateTime now, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0) return [];
        var projectById = projects.ToDictionary(item => item.Id);
        var meetings = await _db.Set<Meeting>().AsNoTracking().Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value) && item.EndTime >= now).OrderBy(item => item.StartTime).Take(20).Select(item => new PortfolioMeetingDto { Id = item.Id, Title = item.Title, StartTime = item.StartTime, EndTime = item.EndTime, ProjectId = item.ProjectId }).ToListAsync(cancellationToken);
        foreach (var meeting in meetings) if (meeting.ProjectId.HasValue && projectById.TryGetValue(meeting.ProjectId.Value, out var project)) { meeting.ProjectName = project.Name; meeting.ProgramId = project.ProgramId; meeting.ProgramName = project.ProgramId.HasValue ? programs.GetValueOrDefault(project.ProgramId.Value)?.Name : null; }
        return meetings;
    }

    private static PortfolioResourceSummaryDto BuildResourceSummary(IReadOnlyList<AllocationRow> allocations, IReadOnlyDictionary<Guid, string> resourceNames, IReadOnlyList<ProjectRow> projects, IReadOnlyDictionary<Guid, ProgramRow> programs)
    {
        var projectById = projects.ToDictionary(item => item.Id);
        var byResource = allocations.GroupBy(item => item.ResourceId).Select(group => new
        {
            ResourceId = group.Key,
            Percentage = group.Sum(item => item.AllocationPercentage),
            Projects = group.GroupBy(item => item.ProjectId).Select(projectGroup =>
            {
                var project = projectById.GetValueOrDefault(projectGroup.Key);
                return new PortfolioResourceProjectDto { ProjectId = projectGroup.Key, ProjectName = project?.Name ?? "", ProgramId = project?.ProgramId, ProgramName = project?.ProgramId is Guid programId ? programs.GetValueOrDefault(programId)?.Name : null, IsDirectProject = project?.IsDirect ?? false, AllocationPercentage = projectGroup.Sum(item => item.AllocationPercentage) };
            }).OrderByDescending(item => item.AllocationPercentage).ToList()
        }).ToList();
        return new PortfolioResourceSummaryDto
        {
            ActiveAllocationCount = allocations.Count,
            ActiveResourceCount = byResource.Count,
            SharedResourceCount = byResource.Count(item => item.Projects.Count > 1),
            CrossProgramResourceCount = byResource.Count(item => item.Projects.Select(project => project.ProgramId).Distinct().Count() > 1),
            OverAllocatedResourceCount = byResource.Count(item => item.Percentage > 100m),
            AllocatedHoursPerWeek = allocations.Sum(item => item.HoursPerWeek ?? 0m),
            Pressure = byResource.Where(item => item.Percentage >= 80m).OrderByDescending(item => item.Percentage).Take(20).Select(item => new PortfolioResourcePressureDto { ResourceId = item.ResourceId, ResourceName = resourceNames.GetValueOrDefault(item.ResourceId, ""), AllocationPercentage = item.Percentage, ProgramCount = item.Projects.Select(project => project.ProgramId).Where(id => id.HasValue).Distinct().Count(), ProjectCount = item.Projects.Count, Projects = item.Projects }).ToList()
        };
    }

    private static IReadOnlyList<PortfolioAttentionItemDto> BuildAttentionItems(IReadOnlyList<PortfolioProjectDto> projects, IReadOnlyList<PortfolioProgramDto> programs, IReadOnlyList<MilestoneRow> milestones, IReadOnlyList<RiskRow> risks, IReadOnlyList<IssueRow> issues, IReadOnlyList<ChangeRequestRow> changes, IReadOnlyDictionary<Guid, PublishedReportRow> reports, IReadOnlyDictionary<Guid, ProgramRow> programById, DateTime now)
    {
        var result = new List<PortfolioAttentionItemDto>();
        foreach (var program in programs.Where(item => item.ProjectsAtRisk > 0).Take(8)) result.Add(new PortfolioAttentionItemDto { Level = program.Health?.Equals("Red", StringComparison.OrdinalIgnoreCase) == true ? "Critical" : "NeedsAttention", Kind = "Program", EntityType = "Program", EntityId = program.Id, EntityName = program.Name, ProgramId = program.Id, Reason = "Program contains Projects requiring attention." });
        foreach (var project in projects.Where(item => item.RequiresAttention).Take(10)) result.Add(new PortfolioAttentionItemDto { Level = project.Health.Equals("Red", StringComparison.OrdinalIgnoreCase) || project.RiskCondition == "Critical" ? "Critical" : "NeedsAttention", Kind = "Project", EntityType = "Project", EntityId = project.Id, EntityName = project.Name, ProgramId = project.ProgramId, ProjectId = project.Id, Reason = BuildProjectReason(project) });
        foreach (var milestone in milestones.Where(item => MilestoneState(item, now) == "Overdue").Take(8)) result.Add(new PortfolioAttentionItemDto { Level = "Critical", Kind = "Milestone", EntityType = "Project", EntityId = milestone.ProjectId, EntityName = milestone.Name, ProgramId = GetProgramId(milestone.ProjectId, projects), ProjectId = milestone.ProjectId, Reason = "Milestone is overdue." });
        foreach (var risk in risks.Where(IsOpenRisk).OrderByDescending(item => RiskRank(item.RiskLevel)).Take(8)) result.Add(new PortfolioAttentionItemDto { Level = risk.RiskLevel == RiskLevel.Critical ? "Critical" : "NeedsAttention", Kind = "Risk", EntityType = "Project", EntityId = risk.ProjectId, EntityName = risk.Title, ProjectId = risk.ProjectId, ProgramId = GetProgramId(risk.ProjectId, projects), Reason = $"{risk.RiskLevel} open project risk." });
        foreach (var issue in issues.Where(IsOpenIssue).OrderByDescending(item => SeverityRank(item.Severity)).Take(8)) result.Add(new PortfolioAttentionItemDto { Level = SeverityRank(issue.Severity) >= 5 ? "Critical" : "NeedsAttention", Kind = "Issue", EntityType = "Project", EntityId = issue.ProjectId, EntityName = issue.Title, ProjectId = issue.ProjectId, ProgramId = GetProgramId(issue.ProjectId, projects), Reason = "Open project issue requires management attention." });
        foreach (var change in changes.Take(6)) result.Add(new PortfolioAttentionItemDto { Level = "NeedsAttention", Kind = "ChangeRequest", EntityType = "Project", EntityId = change.ProjectId, EntityName = change.Impact ?? "Pending change request", ProjectId = change.ProjectId, ProgramId = GetProgramId(change.ProjectId, projects), Reason = change.BudgetChangeAmount.HasValue ? "Pending change request includes budget impact." : "Pending change request." });
        foreach (var project in projects.Where(item => item.StatusReportFreshness != "Current").Take(6)) result.Add(new PortfolioAttentionItemDto { Level = "Informational", Kind = "StatusReport", EntityType = "Project", EntityId = project.Id, EntityName = project.Name, ProjectId = project.Id, ProgramId = project.ProgramId, Reason = $"Status report is {project.StatusReportFreshness.ToLowerInvariant()}." });
        return result.Take(20).ToList();
    }

    private static Guid? GetProgramId(Guid projectId, IReadOnlyList<PortfolioProjectDto> projects) => projects.FirstOrDefault(item => item.Id == projectId)?.ProgramId;
    private static string BuildProjectReason(PortfolioProjectDto project) => project.ScheduleCondition == "Delayed" ? "Project schedule is delayed." : project.RiskCondition == "Critical" ? "Project has critical risk exposure." : project.StatusReportFreshness != "Current" ? $"Project reporting is {project.StatusReportFreshness.ToLowerInvariant()}." : "Project requires attention.";
    private static PortfolioMilestoneDto ToMilestoneDto(MilestoneRow milestone, IReadOnlyDictionary<Guid, ProgramRow> programs, IReadOnlyList<ProjectRow> projects)
    {
        var project = projects.FirstOrDefault(item => item.Id == milestone.ProjectId);
        return new PortfolioMilestoneDto { Id = milestone.Id, ProjectId = milestone.ProjectId, ProjectName = project?.Name ?? "", ProgramId = project?.ProgramId, ProgramName = project?.ProgramId is Guid programId ? programs.GetValueOrDefault(programId)?.Name : null, Name = milestone.Name, DueDate = milestone.DueDate, CompletedDate = milestone.CompletedDate, State = MilestoneState(milestone, DateTime.UtcNow), Status = milestone.Status };
    }
    private static PortfolioRiskDto ToRiskDto(RiskRow risk) => new() { Id = risk.Id, ProjectId = risk.ProjectId, ProjectName = risk.ProjectName, ProgramId = risk.ProgramId, ProgramName = risk.ProgramName, Title = risk.Title, Severity = risk.RiskLevel.ToString(), Status = risk.Status.ToString(), IdentifiedDate = risk.IdentifiedDate };
    private static PortfolioIssueDto ToIssueDto(IssueRow issue) => new() { Id = issue.Id, ProjectId = issue.ProjectId, ProjectName = issue.ProjectName, ProgramId = issue.ProgramId, ProgramName = issue.ProgramName, Title = issue.Title, Severity = issue.Severity, Priority = issue.Priority, Status = issue.Status, TargetResolutionDate = issue.TargetResolutionDate };

    private static bool IsProjectAttention(ProjectRow project, IReadOnlyList<ScheduleRow> schedules, IReadOnlyList<RiskRow> risks, IReadOnlyList<IssueRow> issues, IReadOnlyDictionary<Guid, PublishedReportRow> reports, DateTime now) => IsCriticalProject(project) || string.Equals(project.Status, "OnHold", StringComparison.OrdinalIgnoreCase) || schedules.Where(item => item.ProjectId == project.Id).Any(IsDelayed) || risks.Any(item => item.ProjectId == project.Id && IsOpenRisk(item)) || issues.Any(item => item.ProjectId == project.Id && IsOpenIssue(item)) || !reports.TryGetValue(project.Id, out var report) || ManagementReportingRules.Freshness(report.ReportDate, now) == "Stale";
    private static bool IsProgramAttention(ProgramRow program, IReadOnlyList<ProjectRow> projects, IReadOnlyList<ScheduleRow> schedules, IReadOnlyList<RiskRow> risks) => string.Equals(program.Health, "Red", StringComparison.OrdinalIgnoreCase) || projects.Where(item => item.ProgramId == program.Id).Any(project => IsCriticalProject(project) || schedules.Where(item => item.ProjectId == project.Id).Any(IsDelayed) || risks.Any(risk => risk.ProjectId == project.Id && IsOpenRisk(risk)));
    private static string ProgramDeliveryIndicator(IReadOnlyList<ProjectRow> projects, IReadOnlyList<ScheduleRow> schedules)
    {
        var comparable = schedules.Where(item => projects.Any(project => project.Id == item.ProjectId) && item.BaselineEndDate.HasValue && item.ForecastEndDate.HasValue).ToList();
        return comparable.Count == 0 ? "Unknown" : comparable.Any(IsDelayed) ? "Delayed" : "OnTrack";
    }
    private static string ScheduleCondition(ScheduleRow? schedule) => schedule is null || !schedule.BaselineEndDate.HasValue || !schedule.ForecastEndDate.HasValue ? "Unknown" : IsDelayed(schedule) ? "Delayed" : "OnTrack";
    private static int RiskRank(RiskLevel level) => level switch { RiskLevel.Critical => 4, RiskLevel.High => 3, RiskLevel.Medium => 2, _ => 1 };
    private static int SeverityRank(string? severity) => severity?.ToLowerInvariant() switch { "critical" => 5, "high" => 4, "major" => 3, "medium" => 2, _ => 1 };
    private static bool IsOpenRisk(RiskRow risk) => risk.Status is not RiskStatus.Mitigated and not RiskStatus.Closed;
    private static bool IsOpenIssue(IssueRow issue) => !string.Equals(issue.Status, "Closed", StringComparison.OrdinalIgnoreCase) && !string.Equals(issue.Status, "Resolved", StringComparison.OrdinalIgnoreCase) && !string.Equals(issue.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);
    private static bool IsDelayed(ScheduleRow schedule) => schedule.BaselineEndDate.HasValue && schedule.ForecastEndDate.HasValue && schedule.ForecastEndDate > schedule.BaselineEndDate;
    private static bool IsCriticalProject(PortfolioProjectDto project) => string.Equals(project.Health, "Red", StringComparison.OrdinalIgnoreCase) || string.Equals(project.Status, "Critical", StringComparison.OrdinalIgnoreCase);
    private static bool IsCriticalProject(ProjectRow project) => string.Equals(project.Health, "Red", StringComparison.OrdinalIgnoreCase) || string.Equals(project.Priority, "Critical", StringComparison.OrdinalIgnoreCase);
    private static bool IsActiveStatus(string status) => !IsCompletedStatus(status) && !string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) && !string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase);
    private static bool IsCompletedStatus(string status) => string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Done", StringComparison.OrdinalIgnoreCase);
    private static string ProgressBucket(decimal progress) => progress < 25 ? "0-24" : progress < 50 ? "25-49" : progress < 75 ? "50-74" : progress < 100 ? "75-99" : "100";
    private static string NormalizeCurrency(string? currency, string fallback) => string.IsNullOrWhiteSpace(currency) ? fallback.ToUpperInvariant() : currency.Trim().ToUpperInvariant();
    private static string MilestoneState(MilestoneRow milestone, DateTime now) { if (milestone.CompletedDate.HasValue || string.Equals(milestone.Status, "Completed", StringComparison.OrdinalIgnoreCase) || string.Equals(milestone.Status, "Done", StringComparison.OrdinalIgnoreCase)) return "Completed"; if (!milestone.DueDate.HasValue) return "Unknown"; if (milestone.DueDate.Value < now) return "Overdue"; return milestone.DueDate.Value <= now.AddDays(30) ? "DueSoon" : "OnTrack"; }
    private static int MilestoneSort(MilestoneRow milestone, DateTime now) => MilestoneState(milestone, now) switch { "Overdue" => 0, "DueSoon" => 1, "OnTrack" => 2, "Completed" => 3, _ => 4 };

    private sealed record ProgramRow(Guid Id, Guid PortfolioId, string Name, string? Code, Guid? ManagerId, string? Status, string? Health, string Currency, DateTime? StartDate, DateTime? EndDate);
    private sealed record ProjectRow(Guid Id, Guid? ProgramId, Guid? PortfolioId, string Name, string Code, string Status, string Health, string Priority, Guid? ManagerId, DateTime? StartDate, DateTime? EndDate, decimal Progress, string Currency, decimal? Budget, decimal? ActualCost, bool IsDirect);
    private sealed record ScheduleRow(Guid ProjectId, DateTime? BaselineEndDate, DateTime? ForecastEndDate, DateTime UpdatedAt);
    private sealed record MilestoneRow(Guid Id, Guid ProjectId, string Name, DateTime? DueDate, DateTime? CompletedDate, string? Status);
    private sealed record RiskRow(Guid Id, Guid ProjectId, string Title, RiskLevel RiskLevel, RiskStatus Status, DateTime? IdentifiedDate, string ProjectName = "", Guid? ProgramId = null, string? ProgramName = null);
    private sealed record IssueRow(Guid Id, Guid ProjectId, string Title, string Severity, string Priority, string Status, DateTime? TargetResolutionDate, string ProjectName = "", Guid? ProgramId = null, string? ProgramName = null);
    private sealed record ChangeRequestRow(Guid Id, Guid ProjectId, string? Impact, decimal? BudgetChangeAmount);
    private sealed record AllocationRow(Guid ResourceId, Guid ProjectId, decimal AllocationPercentage, decimal? HoursPerWeek);
    private sealed record PublishedReportRow(Guid ProjectId, DateTime ReportDate, string? Health, decimal? Progress);
    private sealed record MoneyRow(Guid ProjectId, string Currency, decimal Budget, decimal Allocated, decimal Actual, decimal Committed, decimal Forecast);
    private sealed class FinancialAccumulator(Guid projectId, string projectName, Guid? programId, string? programName, bool isDirect, string currency, decimal budget, decimal allocated, decimal actual, decimal committed, decimal forecast)
    {
        public Guid ProjectId { get; } = projectId;
        public string ProjectName { get; } = projectName;
        public Guid? ProgramId { get; } = programId;
        public string? ProgramName { get; } = programName;
        public bool IsDirect { get; } = isDirect;
        public string Currency { get; } = currency;
        public decimal Budget { get; set; } = budget;
        public decimal Allocated { get; set; } = allocated;
        public decimal Actual { get; set; } = actual;
        public decimal Committed { get; set; } = committed;
        public decimal Forecast { get; set; } = forecast;
    }
}
