using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Uses a disposable SQLite database and canonical persisted planning and
/// actual-work entities to verify effective multi-plan reporting and report
/// snapshot behavior without changing Development business data.
/// </summary>
public sealed class ProjectReportingPersistenceTests : IDisposable
{
    private static readonly DateTime StatusDate = new(2037, 10, 20);
    private static readonly string[] Sections =
    [
        "ExecutiveSummary", "PerformanceKPIs", "PlanPerformance", "Milestones", "RisksIssues", "DecisionsRequired"
    ];

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IManagementContextAccessor> _managementContext = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly List<PlanFixture> _executionPlans = [];
    private PlanFixture _scenarioPlan = null!;
    private Guid _coreEarnedWorkingActivityId;

    public ProjectReportingPersistenceTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
        SetTenant();
        _audit.Setup(mock => mock.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    private void SetTenant()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenantId", _tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Name, "Reporting Test User"),
            new Claim("permission", "StatusReport.View"),
            new Claim("permission", "StatusReport.Edit"),
            new Claim("permission", "StatusReport.Publish")
        ], "reporting-test"));
        _http.Setup(item => item.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_http.Object);
        _managementContext.SetupGet(accessor => accessor.Current).Returns(new ManagementContext(
            _userId,
            _tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { _projectId })));
    }

    [Fact]
    public async Task Persisted_multi_plan_configuration_uses_weighted_EVM_and_freezes_generated_report_data()
    {
        SetTenant();
        await SeedFixtureAsync();
        var reporting = CreateReportingService();

        var options = await reporting.GetOptionsAsync(_projectId);
        Assert.True(options.IsSuccess);
        Assert.Equal(4, options.Value!.Plans.Count);
        Assert.Equal("WhatIf", options.Value.Plans.Single(item => item.PlanId == _scenarioPlan.PlanId).Classification);

        var scenarioSelection = Configuration([Selection(_scenarioPlan)]);
        var scenarioResult = await reporting.SaveConfigurationAsync(_projectId, scenarioSelection, _userId.ToString());
        Assert.True(scenarioResult.IsFailure);
        Assert.Empty(await _context.Set<ProjectReportingConfiguration>().ToListAsync());

        var supersededWorking = Configuration(
        [
            Selection(_executionPlans[0].PlanId, _executionPlans[0].SupersededWorkingVersionId, _executionPlans[0].BaselineVersionId)
        ]);
        Assert.True((await reporting.SaveConfigurationAsync(_projectId, supersededWorking, _userId.ToString())).IsFailure);
        var supersededBaseline = Configuration(
        [
            Selection(_executionPlans[0].PlanId, _executionPlans[0].WorkingVersionId, _executionPlans[0].SupersededBaselineVersionId)
        ]);
        Assert.True((await reporting.SaveConfigurationAsync(_projectId, supersededBaseline, _userId.ToString())).IsFailure);

        var firstConfigInput = Configuration(_executionPlans.Select(plan => Selection(plan.PlanId, plan.WorkingVersionId, plan.BaselineVersionId)).ToArray());
        var savedConfig = await reporting.SaveConfigurationAsync(_projectId, firstConfigInput, _userId.ToString());
        Assert.True(savedConfig.IsSuccess, savedConfig.Error?.Message);
        Assert.Equal(1, savedConfig.Value!.Version);
        Assert.Equal(3, savedConfig.Value.Plans.Count);
        Assert.DoesNotContain(savedConfig.Value.Plans, item => item.PlanId == _scenarioPlan.PlanId);

        var persistedConfig = await reporting.GetConfigurationAsync(_projectId, StatusDate);
        Assert.True(persistedConfig.IsSuccess);
        Assert.Equal(3, persistedConfig.Value!.Plans.Count);
        Assert.All(persistedConfig.Value.Plans, item =>
        {
            var plan = _executionPlans.Single(fixture => fixture.PlanId == item.PlanId);
            Assert.Equal(plan.WorkingVersionNumber, item.WorkingVersionNumber);
            Assert.Equal(plan.BaselineVersionNumber, item.BaselineVersionNumber);
            Assert.Equal(plan.BaselineName, item.BaselineName);
        });

        var consolidated = await reporting.GetPerformanceAsync(_projectId, StatusDate);
        Assert.True(consolidated.IsSuccess);
        AssertPerformance(consolidated.Value!, bac: 1_750_000m, pv: 1_000_000m, ev: 780_000m, ac: 900_000m);
        AssertClose(0.78m, consolidated.Value!.SchedulePerformanceIndex);
        AssertClose(780_000m / 900_000m, consolidated.Value.CostPerformanceIndex);
        AssertClose(-220_000m, consolidated.Value.ScheduleVariance);
        AssertClose(-120_000m, consolidated.Value.CostVariance);
        AssertClose(780_000m / 1_750_000m * 100m, consolidated.Value.OverallProgressPercent);
        Assert.Equal("Complete", consolidated.Value.DataQuality);
        Assert.Equal(3, consolidated.Value.Plans.Count);
        Assert.Equal(4, consolidated.Value.AvailablePlans.Count);
        Assert.DoesNotContain(consolidated.Value.Plans, item => item.PlanId == _scenarioPlan.PlanId);
        Assert.Equal(900_000m, consolidated.Value.PlanAttributedActualCost);
        Assert.Equal(0m, consolidated.Value.UnallocatedProjectActualCost);

        var individual = await reporting.GetPerformanceAsync(_projectId, StatusDate, "Individual", _executionPlans[0].PlanId);
        Assert.True(individual.IsSuccess);
        AssertPerformance(individual.Value!, bac: 1_000_000m, pv: 600_000m, ev: 500_000m, ac: 550_000m);
        Assert.Single(individual.Value!.Plans);

        var comparison = await reporting.GetPerformanceAsync(_projectId, StatusDate, "Compare",
            _executionPlans[0].PlanId, _executionPlans[1].PlanId);
        Assert.True(comparison.IsSuccess);
        AssertPerformance(comparison.Value!, bac: 1_500_000m, pv: 900_000m, ev: 700_000m, ac: 800_000m);
        Assert.Equal(2, comparison.Value!.Plans.Count);

        var statusReportService = CreateStatusReportService(reporting);
        var invalidStatusDraft = await statusReportService.CreateAsync(_projectId, new CreateProjectStatusReportDto
        {
            PeriodFrom = StatusDate.AddDays(-6),
            PeriodTo = StatusDate,
            ProjectLifecycleStatusAtReport = "not-configured",
            OverallHealth = "Green",
            OverallProgressPercent = 36.5m
        }, _userId.ToString());
        Assert.True(invalidStatusDraft.IsFailure);
        var created = await statusReportService.CreateAsync(_projectId, new CreateProjectStatusReportDto
        {
            ReportDate = StatusDate,
            PeriodFrom = StatusDate.AddDays(-6),
            PeriodTo = StatusDate,
            StatusDate = StatusDate,
            ProjectLifecycleStatusAtReport = "Planning",
            OverallHealth = "Green",
            OverallProgressPercent = 36.5m,
            ScheduleHealth = "Amber",
            CostHealth = "Green",
            ScopeHealth = "Green",
            QualityHealth = "Green",
            ResourceHealth = "Amber",
            ExecutiveSummary = "Persisted reporting snapshot fixture."
        }, _userId.ToString());
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Null(created.Value!.PerformanceSnapshot);
        Assert.Equal(ProjectStatusReportState.Draft, created.Value.ReportState);
        Assert.Equal("planning", created.Value.ProjectLifecycleStatusAtReport);
        Assert.Equal("Green", created.Value.OverallHealth);
        Assert.Equal(36.5m, created.Value.OverallProgressPercent);

        var currentStatusId = await _context.Set<Project>().Where(project => project.Id == _projectId)
            .Select(project => project.ProjectStatusId).SingleAsync() ?? Guid.Empty;
        var currentCatalogStatus = await _context.Set<ProjectStatusCatalogEntry>()
            .SingleAsync(item => item.Id == currentStatusId);
        currentCatalogStatus.IsActive = false;
        await _context.SaveChangesAsync();

        var inactiveReplacement = await statusReportService.UpdateAsync(_projectId, created.Value.Id, new UpdateProjectStatusReportDto
        {
            PeriodFrom = StatusDate.AddDays(-6),
            PeriodTo = StatusDate,
            ProjectLifecycleStatusAtReport = "retired",
            OverallHealth = "Amber",
            OverallProgressPercent = 41.25m
        }, _userId.ToString());
        Assert.True(inactiveReplacement.IsFailure);

        var editedDraft = await statusReportService.UpdateAsync(_projectId, created.Value.Id, new UpdateProjectStatusReportDto
        {
            ReportDate = StatusDate,
            PeriodFrom = StatusDate.AddDays(-6),
            PeriodTo = StatusDate,
            StatusDate = StatusDate,
            ProjectLifecycleStatusAtReport = "active",
            OverallHealth = "Amber",
            OverallProgressPercent = 41.25m,
            ScheduleHealth = "Amber",
            CostHealth = "Green",
            ScopeHealth = "Green",
            QualityHealth = "Green",
            ResourceHealth = "Amber",
            ExecutiveSummary = "User-edited reporting snapshot."
        }, _userId.ToString());
        Assert.True(editedDraft.IsSuccess, editedDraft.Error?.Message);
        Assert.Equal("active", editedDraft.Value!.ProjectLifecycleStatusAtReport);
        Assert.Equal("Amber", editedDraft.Value.OverallHealth);
        Assert.Equal(41.25m, editedDraft.Value.OverallProgressPercent);

        var finalDraftContent = new UpdateProjectStatusReportDto
        {
            ReportDate = StatusDate,
            PeriodFrom = StatusDate.AddDays(-6),
            PeriodTo = StatusDate,
            StatusDate = StatusDate,
            ProjectLifecycleStatusAtReport = null,
            OverallHealth = "Amber",
            OverallProgressPercent = 41.25m,
            ScheduleHealth = "Amber",
            CostHealth = "Green",
            ScopeHealth = "Green",
            QualityHealth = "Green",
            ResourceHealth = "Amber",
            ExecutiveSummary = "User-edited reporting snapshot.",
            RowVersion = editedDraft.Value!.RowVersion
        };

        var authenticatedUser = _http.Object.HttpContext!.User;
        _http.Object.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenantId", _tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Name, "Reporting Test User"),
            new Claim("permission", "StatusReport.View"),
            new Claim("permission", "StatusReport.Publish")
        ], "reporting-test"));
        var unauthorizedEditAtPublish = await statusReportService.PublishAsync(_projectId, created.Value.Id, new UpdateProjectStatusReportDto
        {
            ReportDate = finalDraftContent.ReportDate,
            PeriodFrom = finalDraftContent.PeriodFrom,
            PeriodTo = finalDraftContent.PeriodTo,
            StatusDate = finalDraftContent.StatusDate,
            ProjectLifecycleStatusAtReport = finalDraftContent.ProjectLifecycleStatusAtReport,
            OverallHealth = finalDraftContent.OverallHealth,
            OverallProgressPercent = finalDraftContent.OverallProgressPercent,
            ScheduleHealth = finalDraftContent.ScheduleHealth,
            CostHealth = finalDraftContent.CostHealth,
            ScopeHealth = finalDraftContent.ScopeHealth,
            QualityHealth = finalDraftContent.QualityHealth,
            ResourceHealth = finalDraftContent.ResourceHealth,
            ExecutiveSummary = "Unauthorized change",
            RowVersion = finalDraftContent.RowVersion
        }, _userId.ToString());
        Assert.True(unauthorizedEditAtPublish.IsFailure);
        Assert.Equal(403, unauthorizedEditAtPublish.Error!.StatusCode);
        var unchangedDraft = await _context.Set<ProjectStatusReport>().SingleAsync(item => item.Id == created.Value.Id);
        Assert.Equal(ProjectStatusReportState.Draft, unchangedDraft.ReportState);
        Assert.Null(unchangedDraft.PerformanceSnapshotJson);
        Assert.Null(unchangedDraft.SourceDataCapturedAt);
        _http.Object.HttpContext.User = authenticatedUser;

        var published = await statusReportService.PublishAsync(_projectId, created.Value.Id, finalDraftContent, _userId.ToString());
        Assert.True(published.IsSuccess, published.Error?.Message);
        Assert.Equal(ProjectStatusReportState.Published, published.Value!.ReportState);
        Assert.Equal(1, published.Value.ReportingConfigurationVersionSnapshot);
        Assert.Equal("User-edited reporting snapshot.", published.Value.ExecutiveSummary);
        Assert.Equal("active", published.Value.ProjectLifecycleStatusAtReport);
        Assert.Equal("Amber", published.Value.OverallHealth);
        Assert.Equal(41.25m, published.Value.OverallProgressPercent);
        Assert.Equal(Sections, published.Value.IncludedSectionsSnapshot);
        Assert.Equal(StatusDate, published.Value.StatusDate);
        Assert.NotNull(published.Value.SourceDataCapturedAt);
        Assert.Equal(published.Value.SourceDataCapturedAt, published.Value.PublishedAt);
        Assert.Equal(_userId, published.Value.PublishedByUserId);
        Assert.Equal("Reporting Test User", published.Value.PublishedByNameSnapshot);
        AssertPerformance(published.Value.PerformanceSnapshot!, bac: 1_750_000m, pv: 1_000_000m, ev: 780_000m, ac: 900_000m);

        var sourceActivity = await _context.Set<PlanActivity>().SingleAsync(item => item.Id == _coreEarnedWorkingActivityId);
        sourceActivity.Progress = 80m;
        await _context.SaveChangesAsync();

        var changedLivePerformance = await reporting.GetPerformanceAsync(_projectId, StatusDate);
        Assert.True(changedLivePerformance.IsSuccess);
        AssertClose(680_000m, changedLivePerformance.Value!.EarnedValue);

        var frozenReport = await statusReportService.GetByIdAsync(_projectId, created.Value.Id);
        Assert.True(frozenReport.IsSuccess);
        AssertPerformance(frozenReport.Value!.PerformanceSnapshot!, bac: 1_750_000m, pv: 1_000_000m, ev: 780_000m, ac: 900_000m);

        await AddUnassignedProjectActualAsync(50_000m);
        var consolidatedWithUnassigned = await reporting.GetPerformanceAsync(_projectId, StatusDate);
        Assert.True(consolidatedWithUnassigned.IsSuccess);
        AssertPerformance(consolidatedWithUnassigned.Value!, bac: 1_750_000m, pv: 1_000_000m, ev: 680_000m, ac: 950_000m);
        Assert.Equal(900_000m, consolidatedWithUnassigned.Value!.PlanAttributedActualCost);
        Assert.Equal(50_000m, consolidatedWithUnassigned.Value.UnallocatedProjectActualCost);
        Assert.Null(consolidatedWithUnassigned.Value.CostVariance);
        Assert.Null(consolidatedWithUnassigned.Value.CostPerformanceIndex);
        Assert.Equal("Partial", consolidatedWithUnassigned.Value.DataQuality);

        var comparisonWithUnassigned = await reporting.GetPerformanceAsync(_projectId, StatusDate, "Compare",
            _executionPlans[0].PlanId, _executionPlans[1].PlanId);
        Assert.True(comparisonWithUnassigned.IsSuccess);
        AssertPerformance(comparisonWithUnassigned.Value!, bac: 1_500_000m, pv: 900_000m, ev: 600_000m, ac: 800_000m);

        var nextConfig = Configuration(_executionPlans.Select((plan, index) =>
            Selection(plan.PlanId, plan.WorkingVersionId, plan.BaselineVersionId, include: index < 2)).ToArray(),
            effectiveFrom: StatusDate.AddDays(1));
        var savedNextConfig = await reporting.SaveConfigurationAsync(_projectId, nextConfig, _userId.ToString());
        Assert.True(savedNextConfig.IsSuccess, savedNextConfig.Error?.Message);
        Assert.Equal(2, savedNextConfig.Value!.Version);

        var historicalPerformance = await reporting.GetPerformanceAsync(_projectId, StatusDate);
        Assert.True(historicalPerformance.IsSuccess);
        AssertPerformance(historicalPerformance.Value!, bac: 1_750_000m, pv: 1_000_000m, ev: 680_000m, ac: 950_000m);
        Assert.Equal(50_000m, historicalPerformance.Value!.UnallocatedProjectActualCost);

        var nextPeriodPerformance = await reporting.GetPerformanceAsync(_projectId, StatusDate.AddDays(1));
        Assert.True(nextPeriodPerformance.IsSuccess);
        Assert.Equal(2, nextPeriodPerformance.Value!.ReportingConfigurationVersion);
        Assert.Equal(2, nextPeriodPerformance.Value.Plans.Count);
        // Legacy calendars spread their weekly capacity across all seven
        // dates, so one day of each included future activity enters PV here.
        AssertPerformance(nextPeriodPerformance.Value, bac: 1_500_000m, pv: 906_666.67m, ev: 600_000m, ac: 850_000m);
        Assert.Equal(50_000m, nextPeriodPerformance.Value.UnallocatedProjectActualCost);

        var publishedEntity = await _context.Set<ProjectStatusReport>().SingleAsync(item => item.Id == created.Value.Id);
        var update = await statusReportService.UpdateAsync(_projectId, publishedEntity.Id, new UpdateProjectStatusReportDto
        {
            ReportDate = StatusDate.AddDays(1), PeriodFrom = StatusDate, PeriodTo = StatusDate.AddDays(1),
            StatusDate = StatusDate.AddDays(1), OverallHealth = "Red", ExecutiveSummary = "Attempted mutation"
        }, _userId.ToString());
        Assert.True(update.IsFailure);
        Assert.Equal(409, update.Error!.StatusCode);
        var delete = await statusReportService.DeleteAsync(_projectId, publishedEntity.Id, _userId.ToString());
        Assert.True(delete.IsFailure);
        Assert.Equal(409, delete.Error!.StatusCode);
        var stillFrozen = await statusReportService.GetByIdAsync(_projectId, publishedEntity.Id);
        Assert.True(stillFrozen.IsSuccess);
        Assert.Equal("User-edited reporting snapshot.", stillFrozen.Value!.ExecutiveSummary);
        AssertPerformance(stillFrozen.Value.PerformanceSnapshot!, bac: 1_750_000m, pv: 1_000_000m, ev: 780_000m, ac: 900_000m);
    }

    [Fact]
    public async Task StatusReportOperationsRejectSameTenantProjectsOutsideManagementProjectScope()
    {
        SetTenant();
        var hiddenProjectId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        _context.Set<Project>().Add(new Project
        {
            Id = hiddenProjectId,
            TenantId = _tenantId,
            Name = "Out of scope reporting project",
            Code = "REPORT-HIDDEN",
            CreatedBy = _userId.ToString()
        });
        _context.Set<ProjectStatusReport>().Add(new ProjectStatusReport
        {
            Id = reportId,
            TenantId = _tenantId,
            ProjectId = hiddenProjectId,
            ReportDate = StatusDate,
            PeriodFrom = StatusDate.AddDays(-7),
            PeriodTo = StatusDate,
            StatusDate = StatusDate,
            ReportState = ProjectStatusReportState.Draft,
            ExecutiveSummary = "Hidden draft remains unchanged.",
            CreatedBy = _userId.ToString()
        });
        await _context.SaveChangesAsync();

        var service = CreateStatusReportService(CreateReportingService());
        var reportingService = CreateReportingService();
        var createDto = new CreateProjectStatusReportDto
        {
            PeriodFrom = StatusDate.AddDays(-7),
            PeriodTo = StatusDate,
            OverallHealth = "Green",
            OverallProgressPercent = 25m,
            ExecutiveSummary = "Must not be created."
        };
        var updateDto = new UpdateProjectStatusReportDto
        {
            PeriodFrom = StatusDate.AddDays(-7),
            PeriodTo = StatusDate,
            OverallHealth = "Red",
            ExecutiveSummary = "Must not be applied."
        };

        var page = await service.GetPagedAsync(hiddenProjectId, 1, 20);
        var overview = await service.GetOverviewAsync(hiddenProjectId);
        var progress = await service.GetCurrentProgressAsync(hiddenProjectId);
        var detail = await service.GetByIdAsync(hiddenProjectId, reportId);
        var comparison = await service.ComparePublishedReportsAsync(hiddenProjectId, reportId, Guid.NewGuid());
        var create = await service.CreateAsync(hiddenProjectId, createDto, _userId.ToString());
        var update = await service.UpdateAsync(hiddenProjectId, reportId, updateDto, _userId.ToString());
        var publish = await service.PublishAsync(hiddenProjectId, reportId, updateDto, _userId.ToString());
        var delete = await service.DeleteAsync(hiddenProjectId, reportId, _userId.ToString());
        var reportingOptions = await reportingService.GetOptionsAsync(hiddenProjectId);
        var reportingConfiguration = await reportingService.GetConfigurationAsync(hiddenProjectId);
        var reportingPerformance = await reportingService.GetPerformanceAsync(hiddenProjectId);
        var saveReportingConfiguration = await reportingService.SaveConfigurationAsync(hiddenProjectId,
            Configuration(Array.Empty<ProjectReportingPlanSelectionInputDto>()), _userId.ToString());
        var financialSummary = await reportingService.GetFinancialSummaryAsync(hiddenProjectId, StatusDate.AddDays(-7), StatusDate);

        Assert.Equal(404, page.Error?.StatusCode);
        Assert.Equal(404, overview.Error?.StatusCode);
        Assert.Equal(404, progress.Error?.StatusCode);
        Assert.Equal(404, detail.Error?.StatusCode);
        Assert.Equal(404, comparison.Error?.StatusCode);
        Assert.Equal(404, create.Error?.StatusCode);
        Assert.Equal(404, update.Error?.StatusCode);
        Assert.Equal(404, publish.Error?.StatusCode);
        Assert.Equal(404, delete.Error?.StatusCode);
        Assert.Equal(404, reportingOptions.Error?.StatusCode);
        Assert.Equal(404, reportingConfiguration.Error?.StatusCode);
        Assert.Equal(404, reportingPerformance.Error?.StatusCode);
        Assert.Equal(404, saveReportingConfiguration.Error?.StatusCode);
        Assert.Equal(404, financialSummary.Error?.StatusCode);

        var persistedReport = await _context.Set<ProjectStatusReport>().SingleAsync(item => item.Id == reportId);
        Assert.Equal(ProjectStatusReportState.Draft, persistedReport.ReportState);
        Assert.Equal("Hidden draft remains unchanged.", persistedReport.ExecutiveSummary);
        Assert.False(persistedReport.IsDeleted);
        Assert.Equal(1, await _context.Set<ProjectStatusReport>().CountAsync(item => item.ProjectId == hiddenProjectId));
    }

    private async Task SeedFixtureAsync()
    {
        var calendar = new WorkingCalendar
        {
            TenantId = _tenantId, CreatedBy = _userId.ToString(), Name = "Reporting test calendar",
            WorkingDaysPerWeek = 5, HoursPerDay = 8, IsDefault = true
        };
        var activeStatusId = Guid.Parse("3df7cd91-58e5-4c5e-aedc-812f34d8a343");
        var activeStatus = new ProjectStatusCatalogEntry
        {
            Id = activeStatusId, TenantId = _tenantId, Code = "active", NormalizedCode = "ACTIVE",
            NameEn = "Active", NameAr = "نشط", IsActive = true, IsDefault = true,
            ReportingCategory = ProjectStatusReportingCategories.InProgress,
            AttentionLevel = ProjectStatusAttentionLevels.None, CreatedBy = _userId.ToString()
        };
        var planningStatus = new ProjectStatusCatalogEntry
        {
            TenantId = _tenantId, Code = "planning", NormalizedCode = "PLANNING",
            NameEn = "Planning", NameAr = "تخطيط", IsActive = true, DisplayOrder = 1,
            ReportingCategory = ProjectStatusReportingCategories.InProgress,
            AttentionLevel = ProjectStatusAttentionLevels.None, CreatedBy = _userId.ToString()
        };
        var retiredStatus = new ProjectStatusCatalogEntry
        {
            TenantId = _tenantId, Code = "retired", NormalizedCode = "RETIRED",
            NameEn = "Retired", NameAr = "متوقف", IsActive = false, DisplayOrder = 2,
            ReportingCategory = ProjectStatusReportingCategories.Excluded,
            AttentionLevel = ProjectStatusAttentionLevels.None, CreatedBy = _userId.ToString()
        };
        var project = new Project
        {
            Id = _projectId, PublicId = Guid.NewGuid(), TenantId = _tenantId, CreatedBy = _userId.ToString(),
            Name = "Multi-Plan Reporting Validation", Code = "MPR-TEST", Priority = "Medium", Status = "active", Currency = "USD",
            ProjectStatusId = activeStatusId
        };
        _context.AddRange(calendar, activeStatus, planningStatus, retiredStatus, project);
        await _context.SaveChangesAsync();

        _executionPlans.Add(await AddExecutionPlanAsync(calendar, "Core Implementation", "Core", 1_000m, 3, 2,
        [
            new PlannedLine("earned", 500m, StatusDate.AddDays(-90), StatusDate.AddDays(-10), 100m),
            new PlannedLine("unearned", 100m, StatusDate.AddDays(-90), StatusDate.AddDays(-10), 0m),
            new PlannedLine("future", 400m, StatusDate.AddDays(1), StatusDate.AddDays(90), 0m)
        ], 550_000m));
        _executionPlans.Add(await AddExecutionPlanAsync(calendar, "Data Migration", "Migration", 1_000m, 2, 1,
        [
            new PlannedLine("earned", 200m, StatusDate.AddDays(-90), StatusDate.AddDays(-10), 100m),
            new PlannedLine("unearned", 100m, StatusDate.AddDays(-90), StatusDate.AddDays(-10), 0m),
            new PlannedLine("future", 200m, StatusDate.AddDays(1), StatusDate.AddDays(90), 0m)
        ], 250_000m));
        _executionPlans.Add(await AddExecutionPlanAsync(calendar, "Training & Go-Live", "Training", 1_000m, 1, 1,
        [
            new PlannedLine("earned", 80m, StatusDate.AddDays(-90), StatusDate.AddDays(-10), 100m),
            new PlannedLine("unearned", 20m, StatusDate.AddDays(-90), StatusDate.AddDays(-10), 0m),
            new PlannedLine("future", 150m, StatusDate.AddDays(1), StatusDate.AddDays(90), 0m)
        ], 100_000m));

        var scenario = NewPlan("Accelerated Scenario", PlanType.Scenario, PlanStatus.Planned);
        var scenarioBaseline = NewVersion(scenario.Id, 1, PlanVersionType.Baseline, PlanVersionStatus.Approved, "Scenario baseline");
        var scenarioWorking = NewVersion(scenario.Id, 2, PlanVersionType.Working, PlanVersionStatus.Active, "Scenario current");
        scenarioWorking.SourceBaselineId = scenarioBaseline.Id;
        _scenarioPlan = new PlanFixture(scenario.Id, scenarioWorking.Id, scenarioBaseline.Id, Guid.Empty, Guid.Empty,
            2, 1, "Scenario baseline");
        _context.AddRange(scenario, scenarioBaseline, scenarioWorking);
        await _context.SaveChangesAsync();
        await AddActivityAsync(calendar, scenario, scenarioBaseline, scenarioWorking, "Scenario", "excluded scenario actual",
            0m, 100_000m, StatusDate, StatusDate, 0m, actualSeconds: 3_600);
    }

    private async Task<PlanFixture> AddExecutionPlanAsync(
        WorkingCalendar calendar, string name, string code, decimal plannedRate, int currentWorkingVersion,
        int approvedBaselineVersion, IReadOnlyList<PlannedLine> lines, decimal actualCost)
    {
        var plan = NewPlan(name, PlanType.Execution, PlanStatus.InProgress);
        var supersededBaseline = approvedBaselineVersion > 1
            ? NewVersion(plan.Id, approvedBaselineVersion - 1, PlanVersionType.Baseline, PlanVersionStatus.Superseded, $"{code} BL-01")
            : null;
        var baselineName = $"{code} BL-{approvedBaselineVersion:D2}";
        var baseline = NewVersion(plan.Id, approvedBaselineVersion, PlanVersionType.Baseline, PlanVersionStatus.Approved, baselineName);
        var supersededWorking = currentWorkingVersion > 1
            ? NewVersion(plan.Id, currentWorkingVersion - 1, PlanVersionType.Working, PlanVersionStatus.Superseded, $"{code} V{currentWorkingVersion - 1}")
            : null;
        var working = NewVersion(plan.Id, currentWorkingVersion, PlanVersionType.Working, PlanVersionStatus.Active, $"{code} V{currentWorkingVersion}");
        working.SourceBaselineId = baseline.Id;
        _context.Add(plan);
        if (supersededBaseline is not null) _context.Add(supersededBaseline);
        _context.Add(baseline);
        if (supersededWorking is not null) _context.Add(supersededWorking);
        _context.Add(working);
        await _context.SaveChangesAsync();

        foreach (var line in lines)
        {
            var work = await AddActivityAsync(calendar, plan, baseline, working, code, $"{code} {line.Name}",
                line.PlannedHours, plannedRate, line.StartDate, line.EndDate, line.Progress);
            if (code == "Core" && line.Name == "earned") _coreEarnedWorkingActivityId = work.WorkingActivityId;
        }

        await AddActivityAsync(calendar, plan, baseline, working, code, $"{code} actual-cost record",
            0m, actualCost, StatusDate, StatusDate, 0m, actualSeconds: 3_600);
        return new PlanFixture(plan.Id, working.Id, baseline.Id, supersededWorking?.Id ?? Guid.Empty,
            supersededBaseline?.Id ?? Guid.Empty, currentWorkingVersion, approvedBaselineVersion, baselineName);
    }

    private async Task<ActivityFixture> AddActivityAsync(
        WorkingCalendar calendar, Plan plan, PlanVersion baseline, PlanVersion working,
        string planCode, string name, decimal plannedHours, decimal hourlyRate,
        DateTime startDate, DateTime endDate, decimal progress, int actualSeconds = 0)
    {
        var sourceActivityId = Guid.NewGuid();
        var resource = new Resource
        {
            TenantId = _tenantId, CreatedBy = _userId.ToString(), Name = $"{name} resource",
            Type = "Internal", Status = "Active", IsActive = true, BaseCalendarId = calendar.Id
        };
        var baselineActivity = NewActivity(baseline.Id, sourceActivityId, $"{planCode}-{Guid.NewGuid():N}"[..14], name,
            startDate, endDate, 0m);
        var workingActivity = NewActivity(working.Id, sourceActivityId, baselineActivity.Code, name,
            startDate, endDate, progress);
        var profile = new ResourceCostProfile
        {
            TenantId = _tenantId, CreatedBy = _userId.ToString(), ResourceId = resource.Id,
            EffectiveFrom = startDate.AddDays(-1), EffectiveTo = endDate.AddDays(1),
            CompensationType = CompensationType.Hourly, Amount = hourlyRate, Currency = "USD"
        };
        _context.AddRange(resource, baselineActivity, workingActivity, profile);
        await _context.SaveChangesAsync();

        _context.Set<PlanAssignment>().Add(new PlanAssignment
        {
            TenantId = _tenantId, CreatedBy = _userId.ToString(), PlanVersionId = baseline.Id,
            ActivityId = baselineActivity.Id, ResourceId = resource.Id, PlannedHours = plannedHours,
            StartDate = startDate, EndDate = endDate
        });
        if (actualSeconds > 0)
        {
            _context.Set<TimeEntry>().Add(new TimeEntry
            {
                TenantId = _tenantId, CreatedBy = _userId.ToString(), ResourceId = resource.Id,
                ProjectId = _projectId, PlanActivityId = workingActivity.Id, WorkDate = StatusDate,
                StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(9),
                ElapsedSeconds = actualSeconds, ActualSeconds = actualSeconds, EntrySource = TimeEntry.SourceManual
            });
        }
        await _context.SaveChangesAsync();
        return new ActivityFixture(baselineActivity.Id, workingActivity.Id);
    }

    private async Task AddUnassignedProjectActualAsync(decimal hourlyRate)
    {
        var calendar = await _context.Set<WorkingCalendar>().SingleAsync(item => item.TenantId == _tenantId && item.IsDefault);
        var resource = new Resource
        {
            TenantId = _tenantId, CreatedBy = _userId.ToString(), Name = "Unassigned project actual resource",
            Type = "Internal", Status = "Active", IsActive = true, BaseCalendarId = calendar.Id
        };
        _context.Set<Resource>().Add(resource);
        _context.Set<ResourceCostProfile>().Add(new ResourceCostProfile
        {
            TenantId = _tenantId, CreatedBy = _userId.ToString(), ResourceId = resource.Id,
            EffectiveFrom = StatusDate.AddDays(-1), EffectiveTo = StatusDate.AddDays(1),
            CompensationType = CompensationType.Hourly, Amount = hourlyRate, Currency = "USD"
        });
        _context.Set<TimeEntry>().Add(new TimeEntry
        {
            TenantId = _tenantId, CreatedBy = _userId.ToString(), ResourceId = resource.Id,
            ProjectId = _projectId, PlanActivityId = null, WorkDate = StatusDate,
            StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11),
            ElapsedSeconds = 3_600, ActualSeconds = 3_600, EntrySource = TimeEntry.SourceManual
        });
        await _context.SaveChangesAsync();
    }

    private Plan NewPlan(string name, PlanType type, PlanStatus status) => new()
    {
        Id = Guid.NewGuid(), TenantId = _tenantId, CreatedBy = _userId.ToString(),
        ProjectId = _projectId, PlanType = type, Name = name, Status = status
    };

    private PlanVersion NewVersion(Guid planId, int number, PlanVersionType type, PlanVersionStatus status, string name) => new()
    {
        Id = Guid.NewGuid(), TenantId = _tenantId, CreatedBy = _userId.ToString(),
        PlanId = planId, VersionNumber = number, Type = type, Status = status, Name = name
    };

    private PlanActivity NewActivity(Guid versionId, Guid sourceId, string code, string name,
        DateTime startDate, DateTime endDate, decimal progress) => new()
    {
        Id = Guid.NewGuid(), TenantId = _tenantId, CreatedBy = _userId.ToString(),
        PlanVersionId = versionId, SourceActivityId = sourceId, Code = code, Name = name,
        Type = PlanActivityType.Task, SchedulingMode = PlanSchedulingMode.Manual,
        StartDate = startDate, EndDate = endDate, Duration = Math.Max(0, (endDate.Date - startDate.Date).Days),
        Progress = progress
    };

    private ProjectReportingService CreateReportingService() => new(
        _context,
        _audit.Object,
        new ProjectEvmPlanLookup(_context),
        new ProjectEvmLaborCostLookup(_context, new ActualWorkCostLookup(_context), new ResourceLaborCostCalculator()),
        new ProjectEvmDirectCostLookup(_context),
        _managementContext.Object);

    private ProjectStatusReportService CreateStatusReportService(ProjectReportingService reporting)
    {
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var provisioning = new Mock<IProjectStatusReportWorkflowProvisioningService>();
        provisioning.Setup(mock => mock.EnsureAsync(_tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProjectStatusReportWorkflowDefinition>.Success(new(definitionId, versionId)));

        var workflowInstances = new Mock<IWorkflowInstanceService>();
        workflowInstances.Setup(mock => mock.GetInstanceAsync(
                ProjectStatusReportWorkflow.EntityType, It.IsAny<Guid>(), It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<WorkflowInstanceSummary?>.Success(null));
        workflowInstances.Setup(mock => mock.ProvisionAsync(
                ProjectStatusReportWorkflow.EntityType, It.IsAny<Guid>(), definitionId, versionId,
                It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string entityType, Guid entityId, Guid workflowDefinitionId, Guid workflowVersionId,
                WorkflowExecutionContext _, CancellationToken _) => Result<WorkflowInstanceSummary>.Success(new WorkflowInstanceSummary(
                Guid.NewGuid(), workflowDefinitionId, workflowVersionId, ProjectStatusReportWorkflow.DefinitionCode, 2,
                entityType, entityId, "Draft", "Draft", "مسودة", false, DateTime.UtcNow, _userId.ToString(), null, true)));

        var workflowExecution = new Mock<IWorkflowExecutionService>();
        workflowExecution.Setup(mock => mock.ExecuteTransitionAsync(
                It.IsAny<WorkflowTransitionRequest>(), It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()))
            .Returns(async (WorkflowTransitionRequest request, WorkflowExecutionContext _, CancellationToken cancellationToken) =>
            {
                if (request.TransitionCode == ProjectStatusReportWorkflow.Publish)
                {
                    var report = await _context.Set<ProjectStatusReport>().SingleAsync(item => item.Id == request.EntityId, cancellationToken);
                    report.ReportState = ProjectStatusReportState.Published;
                    report.PublishedAt = report.SourceDataCapturedAt;
                    report.PublishedByUserId = _userId;
                    report.PublishedByNameSnapshot = "Reporting Test User";
                    await _context.SaveChangesAsync(cancellationToken);
                }
                return Result<WorkflowTransitionResult>.Success(new WorkflowTransitionResult(
                    true, Guid.NewGuid(), "Draft", "Draft", "مسودة", "Published", "Published", "منشور",
                    null, null, Guid.NewGuid().ToByteArray()));
            });

        return new ProjectStatusReportService(
            _context, _audit.Object, _http.Object, reporting,
            new ProjectPlanProgressLookup(_context), new CapacityService(_context),
            provisioning.Object, workflowInstances.Object,
            workflowExecution.Object, _managementContext.Object,
            new FrozenTimeProvider(new DateTimeOffset(StatusDate, TimeSpan.Zero)));
    }

    private SaveProjectReportingConfigurationDto Configuration(
        IReadOnlyList<ProjectReportingPlanSelectionInputDto> plans, DateTime? effectiveFrom = null) => new()
    {
        EffectiveFrom = effectiveFrom ?? StatusDate.AddDays(-7),
        ReportingFrequency = "Weekly",
        TimeZoneId = "UTC",
        CurrencyCode = "USD",
        IncludedSections = Sections,
        Plans = plans
    };

    private static ProjectReportingPlanSelectionInputDto Selection(PlanFixture plan, bool include = true) =>
        Selection(plan.PlanId, plan.WorkingVersionId, plan.BaselineVersionId, include);

    private static ProjectReportingPlanSelectionInputDto Selection(Guid planId, Guid workingVersionId, Guid baselineVersionId, bool include = true) => new()
    {
        PlanId = planId, WorkingVersionId = workingVersionId, BaselineVersionId = baselineVersionId,
        IncludeInConsolidatedReporting = include
    };

    private static void AssertPerformance(ProjectPerformanceDto actual, decimal bac, decimal pv, decimal ev, decimal ac)
    {
        AssertClose(nameof(actual.BudgetAtCompletion), bac, actual.BudgetAtCompletion);
        AssertClose(nameof(actual.PlannedValue), pv, actual.PlannedValue);
        AssertClose(nameof(actual.EarnedValue), ev, actual.EarnedValue);
        AssertClose(nameof(actual.ActualCost), ac, actual.ActualCost);
    }

    private static void AssertClose(decimal expected, decimal? actual) =>
        AssertClose("EVM result", expected, actual);

    private static void AssertClose(string field, decimal expected, decimal? actual)
    {
        Assert.True(actual.HasValue, $"Expected {field} to be available.");
        Assert.True(Math.Abs(expected - actual!.Value) <= 0.01m, $"Expected {expected}, got {actual.Value}.");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed record PlanFixture(Guid PlanId, Guid WorkingVersionId, Guid BaselineVersionId, Guid SupersededWorkingVersionId,
        Guid SupersededBaselineVersionId, int WorkingVersionNumber, int BaselineVersionNumber, string BaselineName);
    private sealed record PlannedLine(string Name, decimal PlannedHours, DateTime StartDate, DateTime EndDate, decimal Progress);
    private sealed record ActivityFixture(Guid BaselineActivityId, Guid WorkingActivityId);

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
