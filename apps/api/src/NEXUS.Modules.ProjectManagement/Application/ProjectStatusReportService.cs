using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.IssueManagement.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public sealed class ProjectStatusReportService : IProjectStatusReportService
{
    private const int MaxSnapshotItems = 5;
    private static readonly string[] HealthValues = ["Green", "Amber", "Red"];

    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IProjectPlanProgressLookup _projectPlanProgressLookup;

    public ProjectStatusReportService(DbContext context, IAuditService auditService, IHttpContextAccessor httpContextAccessor, IProjectPlanProgressLookup projectPlanProgressLookup)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _projectPlanProgressLookup = projectPlanProgressLookup;
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

    public async Task<Result<ProjectStatusReportCurrentProgressDto>> GetCurrentProgressAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!await ProjectExistsAsync(projectId, cancellationToken)) return Result<ProjectStatusReportCurrentProgressDto>.Failure(Error.NotFound("Project not found"));
        return Result<ProjectStatusReportCurrentProgressDto>.Success(new ProjectStatusReportCurrentProgressDto
        {
            ProjectPlanProgressPercent = await _projectPlanProgressLookup.GetAsync(projectId, cancellationToken)
        });
    }

    public async Task<Result<ProjectStatusReportDto>> GetByIdAsync(Guid projectId, Guid reportId, CancellationToken cancellationToken = default)
    {
        var report = await _context.Set<ProjectStatusReport>().AsNoTracking()
            .Include(x => x.RiskSnapshots).Include(x => x.IssueSnapshots).Include(x => x.ChangeRequestSnapshots)
            .FirstOrDefaultAsync(x => x.Id == reportId && x.ProjectId == projectId, cancellationToken);
        return report is null
            ? Result<ProjectStatusReportDto>.Failure(Error.NotFound("Status report not found"))
            : Result<ProjectStatusReportDto>.Success(MapDetail(report));
    }

    public async Task<Result<ProjectStatusReportDto>> CreateAsync(Guid projectId, CreateProjectStatusReportDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var project = await _context.Set<Project>().FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (project is null) return Result<ProjectStatusReportDto>.Failure(Error.NotFound("Project not found"));
        var projectPlanProgress = await _projectPlanProgressLookup.GetAsync(projectId, cancellationToken);
        var progress = projectPlanProgress ?? dto.OverallProgressPercent ?? project.Progress;
        var validation = ValidateDraft(dto.ReportDate, dto.PeriodFrom, dto.PeriodTo, progress);
        if (validation is not null) return Result<ProjectStatusReportDto>.Failure(validation);
        var latestProjectStatus = await _context.Set<ProjectStatusReport>().AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.ReportDate).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => x.ProjectLifecycleStatusAtReport)
            .FirstOrDefaultAsync(cancellationToken);
        var preparedByUserId = ParseUserId(userId);
        var preparedByName = await ResolveUserNameAsync(preparedByUserId, userId, cancellationToken);

        var report = new ProjectStatusReport
        {
            ProjectId = projectId,
            ReportDate = dto.ReportDate,
            PeriodFrom = dto.PeriodFrom,
            PeriodTo = dto.PeriodTo,
            ReportState = ProjectStatusReportState.Draft,
            ProjectLifecycleStatusAtReport = dto.ProjectLifecycleStatusAtReport ?? latestProjectStatus ?? project.Status,
            OverallHealth = dto.OverallHealth ?? project.Health,
            OverallProgressPercent = progress,
            ScheduleHealth = dto.ScheduleHealth,
            CostHealth = dto.CostHealth,
            ScopeHealth = dto.ScopeHealth,
            QualityHealth = dto.QualityHealth,
            ResourceHealth = dto.ResourceHealth,
            ExecutiveSummary = dto.ExecutiveSummary,
            KeyAchievements = dto.KeyAchievements,
            ChallengesConcerns = dto.ChallengesConcerns,
            NextPeriodPriorities = dto.NextPeriodPriorities,
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
        var report = await _context.Set<ProjectStatusReport>().FirstOrDefaultAsync(x => x.Id == reportId && x.ProjectId == projectId, cancellationToken);
        if (report is null) return Result<ProjectStatusReportDto>.Failure(Error.NotFound("Status report not found"));
        if (report.ReportState != ProjectStatusReportState.Draft) return Result<ProjectStatusReportDto>.Failure(Error.Conflict("Published status reports are immutable"));
        if (dto.RowVersion is { Length: > 0 }) _context.Entry(report).Property(x => x.RowVersion).OriginalValue = dto.RowVersion;

        var projectPlanProgress = await _projectPlanProgressLookup.GetAsync(projectId, cancellationToken);
        var progress = projectPlanProgress ?? dto.OverallProgressPercent;
        var validation = ValidateDraft(dto.ReportDate, dto.PeriodFrom, dto.PeriodTo, progress);
        if (validation is not null) return Result<ProjectStatusReportDto>.Failure(validation);

        report.ReportDate = dto.ReportDate;
        report.PeriodFrom = dto.PeriodFrom;
        report.PeriodTo = dto.PeriodTo;
        if (!string.IsNullOrWhiteSpace(dto.ProjectLifecycleStatusAtReport)) report.ProjectLifecycleStatusAtReport = dto.ProjectLifecycleStatusAtReport;
        report.OverallHealth = dto.OverallHealth;
        report.OverallProgressPercent = progress;
        report.ScheduleHealth = dto.ScheduleHealth;
        report.CostHealth = dto.CostHealth;
        report.ScopeHealth = dto.ScopeHealth;
        report.QualityHealth = dto.QualityHealth;
        report.ResourceHealth = dto.ResourceHealth;
        report.ExecutiveSummary = dto.ExecutiveSummary;
        report.KeyAchievements = dto.KeyAchievements;
        report.ChallengesConcerns = dto.ChallengesConcerns;
        report.NextPeriodPriorities = dto.NextPeriodPriorities;
        report.UpdatedAt = DateTime.UtcNow;
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

    public async Task<Result<ProjectStatusReportDto>> PublishAsync(Guid projectId, Guid reportId, string userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var report = await _context.Set<ProjectStatusReport>()
            .Include(x => x.RiskSnapshots).Include(x => x.IssueSnapshots).Include(x => x.ChangeRequestSnapshots)
            .FirstOrDefaultAsync(x => x.Id == reportId && x.ProjectId == projectId, cancellationToken);
        if (report is null) return Result<ProjectStatusReportDto>.Failure(Error.NotFound("Status report not found"));
        if (report.ReportState != ProjectStatusReportState.Draft) return Result<ProjectStatusReportDto>.Failure(Error.Conflict("Only draft status reports can be published"));

        var validation = ValidateForPublish(report);
        if (validation is not null) return Result<ProjectStatusReportDto>.Failure(validation);
        var project = await _context.Set<Project>().FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (project is null) return Result<ProjectStatusReportDto>.Failure(Error.NotFound("Project not found"));

        var now = DateTime.UtcNow;
        var preparedBy = report.PreparedByUserId ?? ParseUserId(userId);
        var preparedByName = report.PreparedByNameSnapshot ?? await ResolveUserNameAsync(preparedBy, userId, cancellationToken);
        var publisherName = await ResolveUserNameAsync(ParseUserId(userId), userId, cancellationToken);

        var managerName = await ResolveUserNameAsync(project.ProjectManagerId, "System", cancellationToken);
        CaptureProjectSnapshot(report, project, managerName, preparedBy, preparedByName);
        await CaptureFinancialSnapshotAsync(report, project, cancellationToken);
        await CaptureScheduleSnapshotAsync(report, projectId, cancellationToken);
        await CaptureBaselineSnapshotAsync(report, projectId, cancellationToken);
        await CaptureRelatedSnapshotsAsync(report, projectId, cancellationToken);

        report.ReportState = ProjectStatusReportState.Published;
        report.PublishedAt = now;
        report.PublishedByUserId = ParseUserId(userId);
        report.PublishedByNameSnapshot = publisherName;
        report.SourceDataCapturedAt = now;
        report.PreparedByUserId ??= preparedBy;
        report.PreparedByNameSnapshot ??= preparedByName;
        report.UpdatedAt = now;
        report.UpdatedBy = userId;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogAsync(new AuditEntry
            {
                EntityType = "ProjectStatusReport", EntityId = report.Id, Action = "Publish", UserId = ParseUserId(userId),
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { report.ReportState, report.SourceDataCapturedAt }),
                Timestamp = now, CreatedBy = userId
            }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ProjectStatusReportDto>.Failure(Error.Conflict("Status report was changed by another user"));
        }

        return Result<ProjectStatusReportDto>.Success(MapDetail(report));
    }

    public async Task<Result> DeleteAsync(Guid projectId, Guid reportId, string userId, CancellationToken cancellationToken = default)
    {
        var report = await _context.Set<ProjectStatusReport>().FirstOrDefaultAsync(x => x.Id == reportId && x.ProjectId == projectId, cancellationToken);
        if (report is null) return Result.Failure(Error.NotFound("Status report not found"));
        if (report.ReportState != ProjectStatusReportState.Draft) return Result.Failure(Error.Conflict("Published status reports are immutable"));
        report.IsDeleted = true;
        report.UpdatedAt = DateTime.UtcNow;
        report.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        await LogAsync(report, "Delete", userId, cancellationToken);
        return Result.Success();
    }

    private async Task CaptureRelatedSnapshotsAsync(ProjectStatusReport report, Guid projectId, CancellationToken cancellationToken)
    {
        _context.Set<ProjectStatusReportRiskSnapshot>().RemoveRange(report.RiskSnapshots);
        _context.Set<ProjectStatusReportIssueSnapshot>().RemoveRange(report.IssueSnapshots);
        _context.Set<ProjectStatusReportChangeRequestSnapshot>().RemoveRange(report.ChangeRequestSnapshots);

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
        report.ProjectLifecycleStatusAtReport ??= project.Status;
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

    private async Task<bool> ProjectExistsAsync(Guid projectId, CancellationToken cancellationToken) => await _context.Set<Project>().AnyAsync(x => x.Id == projectId, cancellationToken);

    private async Task LogAsync(ProjectStatusReport report, string action, string userId, CancellationToken cancellationToken)
    {
        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ProjectStatusReport", EntityId = report.Id, Action = action, UserId = ParseUserId(userId),
            Timestamp = DateTime.UtcNow, CreatedBy = userId
        }, cancellationToken);
    }

    private static Error? ValidateDraft(DateTime reportDate, DateTime periodFrom, DateTime periodTo, decimal? progress)
    {
        if (reportDate == default || periodFrom == default || periodTo == default) return Error.Validation("Report date and reporting period are required");
        if (periodFrom.Date > periodTo.Date) return Error.Validation("Reporting period start must be before its end");
        if (progress is < 0 or > 100) return Error.Validation("Overall progress must be between 0 and 100");
        return null;
    }

    private static Error? ValidateForPublish(ProjectStatusReport report)
    {
        var draftValidation = ValidateDraft(report.ReportDate, report.PeriodFrom, report.PeriodTo, report.OverallProgressPercent);
        if (draftValidation is not null) return draftValidation;
        var healthValues = new[] { report.OverallHealth, report.ScheduleHealth, report.CostHealth, report.ScopeHealth, report.QualityHealth, report.ResourceHealth };
        if (healthValues.Any(x => string.IsNullOrWhiteSpace(x) || !HealthValues.Contains(x, StringComparer.OrdinalIgnoreCase))) return Error.Validation("All health dimensions must be Green, Amber, or Red before publishing");
        if (string.IsNullOrWhiteSpace(report.ExecutiveSummary)) return Error.Validation("Executive summary is required before publishing");
        return null;
    }

    private static ProjectStatusReportDto MapDetail(ProjectStatusReport x) => new()
    {
        Id = x.Id, ProjectId = x.ProjectId, ReportDate = x.ReportDate, PeriodFrom = x.PeriodFrom, PeriodTo = x.PeriodTo,
        ReportState = x.ReportState, OverallHealth = x.OverallHealth, OverallProgressPercent = x.OverallProgressPercent,
        PreparedByNameSnapshot = x.PreparedByNameSnapshot, PublishedAt = x.PublishedAt, ProjectNameSnapshot = x.ProjectNameSnapshot,
        ProjectCodeSnapshot = x.ProjectCodeSnapshot, ProjectManagerIdAtReport = x.ProjectManagerIdAtReport, ProjectManagerNameSnapshot = x.ProjectManagerNameSnapshot,
        ProjectLifecycleStatusAtReport = x.ProjectLifecycleStatusAtReport, ScheduleHealth = x.ScheduleHealth, CostHealth = x.CostHealth,
        ScopeHealth = x.ScopeHealth, QualityHealth = x.QualityHealth, ResourceHealth = x.ResourceHealth, ExecutiveSummary = x.ExecutiveSummary,
        KeyAchievements = x.KeyAchievements, ChallengesConcerns = x.ChallengesConcerns, NextPeriodPriorities = x.NextPeriodPriorities,
        PreparedByUserId = x.PreparedByUserId, PublishedByUserId = x.PublishedByUserId, PublishedByNameSnapshot = x.PublishedByNameSnapshot,
        SourceDataCapturedAt = x.SourceDataCapturedAt, CurrencyCodeSnapshot = x.CurrencyCodeSnapshot, BudgetAmountSnapshot = x.BudgetAmountSnapshot,
        ActualCostAmountSnapshot = x.ActualCostAmountSnapshot, CommittedCostAmountSnapshot = x.CommittedCostAmountSnapshot,
        ForecastCostAmountSnapshot = x.ForecastCostAmountSnapshot, CostVarianceAmountSnapshot = x.CostVarianceAmountSnapshot,
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

    private static Guid? ParseUserId(string userId) => Guid.TryParse(userId, out var id) ? id : null;
    private static int RiskLevelRank(RiskLevel value) => value switch { RiskLevel.Critical => 4, RiskLevel.High => 3, RiskLevel.Medium => 2, _ => 1 };
    private static int RiskStatusRank(RiskStatus value) => value switch { RiskStatus.ActionRequired => 5, RiskStatus.Identified => 4, RiskStatus.FollowUp => 3, RiskStatus.Mitigated => 2, _ => 1 };
    private static int IssueSeverityRank(string value) => Rank(value, "Critical", "High", "Major", "Medium", "Minor");
    private static int IssuePriorityRank(string value) => Rank(value, "Critical", "High", "Medium", "Low");
    private static int IssueStatusRank(string value) => Rank(value, "Open", "In Progress", "InProgress", "Blocked", "Resolved", "Closed");
    private static int ChangeRequestStatusRank(ProjectChangeRequest.Statuses value) => value switch { ProjectChangeRequest.Statuses.Pending => 4, ProjectChangeRequest.Statuses.Approved => 3, ProjectChangeRequest.Statuses.Rejected => 2, _ => 1 };
    private static int Rank(string value, params string[] ordered) => Array.FindIndex(ordered, x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)) is var index && index >= 0 ? ordered.Length - index : 0;
}
