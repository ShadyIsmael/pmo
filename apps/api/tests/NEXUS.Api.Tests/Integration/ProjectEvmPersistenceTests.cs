using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Persists a controlled plan, baseline, assignment, cost profile, and time
/// entry in SQLite, then verifies that normal service updates move SPI and CPI
/// independently without changing baseline PV/BAC. An un-baselined direct
/// cost remains in AC but makes CV/CPI unavailable rather than misleading.
/// </summary>
public sealed class ProjectEvmPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _resourceId = Guid.NewGuid();
    private readonly Guid _activityKey = Guid.NewGuid();
    private readonly Guid _planId = Guid.NewGuid();
    private readonly Guid _baselineVersionId = Guid.NewGuid();
    private readonly Guid _workingVersionId = Guid.NewGuid();
    private readonly Guid _baselineActivityId = Guid.NewGuid();
    private readonly Guid _workingActivityId = Guid.NewGuid();
    private readonly Guid _timeEntryId = Guid.NewGuid();
    private readonly DateTime _statusDate = DateTime.UtcNow.Date;

    public ProjectEvmPersistenceTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
        SetTenant();
        _audit.Setup(mock => mock.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _audit.Setup(mock => mock.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    [Fact]
    public async Task Persisted_baseline_progress_and_actual_cost_produce_independent_live_EVM_changes()
    {
        var planStart = _statusDate.AddDays(-10);
        var planEnd = _statusDate.AddDays(10);
        var calendar = new WorkingCalendar
        {
            TenantId = _tenantId,
            CreatedBy = _userId.ToString(),
            Name = "EVM test calendar",
            IsDefault = true,
            WorkingDaysPerWeek = 5,
            HoursPerDay = 8,
        };
        var project = new Project
        {
            Id = _projectId,
            TenantId = _tenantId,
            CreatedBy = _userId.ToString(),
            Name = "EVM controlled scenario",
            Code = "EVM-TEST",
            Priority = "Medium",
            Status = "Active",
            Currency = "USD",
        };
        var resource = new Resource
        {
            Id = _resourceId,
            TenantId = _tenantId,
            CreatedBy = _userId.ToString(),
            Name = "EVM resource",
            Type = "Internal",
            Status = "Active",
            IsActive = true,
            UserId = _userId,
            BaseCalendarId = calendar.Id,
        };
        var plan = new Plan
        {
            Id = _planId,
            TenantId = _tenantId,
            CreatedBy = _userId.ToString(),
            ProjectId = _projectId,
            PlanType = PlanType.Execution,
            Name = "Execution Plan",
            Status = PlanStatus.InProgress,
        };
        var baseline = new PlanVersion
        {
            Id = _baselineVersionId,
            TenantId = _tenantId,
            CreatedBy = _userId.ToString(),
            PlanId = _planId,
            VersionNumber = 1,
            Type = PlanVersionType.Baseline,
            Status = PlanVersionStatus.Approved,
            Name = "Approved baseline",
        };
        var working = new PlanVersion
        {
            Id = _workingVersionId,
            TenantId = _tenantId,
            CreatedBy = _userId.ToString(),
            PlanId = _planId,
            VersionNumber = 2,
            Type = PlanVersionType.Working,
            Status = PlanVersionStatus.Active,
            Name = "Working v2",
            SourceBaselineId = _baselineVersionId,
        };
        var baselineActivity = NewActivity(_baselineActivityId, _baselineVersionId, planStart, planEnd, 0m);
        baselineActivity.SourceActivityId = _activityKey;
        var workingActivity = NewActivity(_workingActivityId, _workingVersionId, planStart, planEnd, 50m);
        workingActivity.SourceActivityId = _activityKey;
        _context.AddRange(calendar, project, resource, plan, baseline, working, baselineActivity, workingActivity);
        _context.Set<ProjectMember>().Add(new ProjectMember
        {
            TenantId = _tenantId,
            CreatedBy = _userId.ToString(),
            ProjectId = _projectId,
            ResourceId = _resourceId,
            Role = "Contributor",
            AllocationPercentage = 100m,
            StartDate = planStart.AddDays(-30),
            EndDate = planEnd.AddDays(30),
            Status = "Active",
        });
        await _context.SaveChangesAsync();
        _context.PlanAssignments.Add(new PlanAssignment
        {
            TenantId = _tenantId,
            CreatedBy = _userId.ToString(),
            PlanVersionId = _baselineVersionId,
            ActivityId = _baselineActivityId,
            ResourceId = _resourceId,
            PlannedHours = 40m,
            StartDate = planStart,
            EndDate = planEnd,
        });
        _context.ResourceCostProfiles.Add(new ResourceCostProfile
        {
            TenantId = _tenantId,
            CreatedBy = _userId.ToString(),
            ResourceId = _resourceId,
            EffectiveFrom = planStart.AddDays(-2),
            EffectiveTo = planEnd.AddDays(2),
            CompensationType = CompensationType.Hourly,
            Amount = 10m,
            Currency = "USD",
        });
        _context.TimeEntries.Add(new TimeEntry
        {
            Id = _timeEntryId,
            TenantId = _tenantId,
            CreatedBy = _userId.ToString(),
            ResourceId = _resourceId,
            ProjectId = _projectId,
            PlanActivityId = _workingActivityId,
            WorkDate = _statusDate,
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(18),
            ElapsedSeconds = 36_000,
            ActualSeconds = 28_800,
            BreakSeconds = 7_200,
            EntrySource = TimeEntry.SourceManual,
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _context.SaveChangesAsync();

        var planLookup = new ProjectEvmPlanLookup(_context);
        var laborLookup = new ProjectEvmLaborCostLookup(_context, new ActualWorkCostLookup(_context), new ResourceLaborCostCalculator());
        var directCostLookup = new ProjectEvmDirectCostLookup(_context);
        var initial = await ReadMetricsAsync(planLookup, laborLookup, directCostLookup);
        Assert.Equal(_baselineVersionId, initial.BaselineId);
        Assert.Equal(400m, initial.BudgetAtCompletion);
        AssertClose(200m, initial.EarnedValue);
        AssertClose(80m, initial.ActualCost);
        Assert.True(initial.PlannedValue > 0m);
        Assert.True(initial.PlannedValue < initial.BudgetAtCompletion);
        AssertClose(2.5m, initial.CostPerformanceIndex);

        var currentActivity = await _context.PlanActivities.SingleAsync(item => item.Id == _workingActivityId);
        var activityUpdate = new UpdatePlanActivityDto
        {
            Code = currentActivity.Code,
            Name = currentActivity.Name,
            Type = currentActivity.Type,
            SchedulingMode = PlanSchedulingMode.Manual,
            ConstraintType = currentActivity.ConstraintType,
            StartDate = currentActivity.StartDate,
            EndDate = currentActivity.EndDate,
            Duration = currentActivity.Duration,
            EstimateHours = currentActivity.EstimateHours,
            Progress = 75m,
            SortOrder = currentActivity.SortOrder,
            RowVersion = currentActivity.RowVersion,
        };
        var planningService = new PlanActivityService(_context, _audit.Object, _http.Object);
        var progressUpdate = await planningService.UpdateAsync(_planId, _workingActivityId, activityUpdate, _userId.ToString());
        Assert.True(progressUpdate.IsSuccess, progressUpdate.Error?.Message);

        var afterProgress = await ReadMetricsAsync(planLookup, laborLookup, directCostLookup);
        AssertClose(initial.PlannedValue, afterProgress.PlannedValue);
        AssertClose(300m, afterProgress.EarnedValue);
        AssertClose(initial.ActualCost, afterProgress.ActualCost);
        Assert.NotEqual(initial.SchedulePerformanceIndex, afterProgress.SchedulePerformanceIndex);
        Assert.NotEqual(initial.CostPerformanceIndex, afterProgress.CostPerformanceIndex);

        var timeEntry = await _context.TimeEntries.SingleAsync(item => item.Id == _timeEntryId);
        var timeEntryService = new TimeEntryService(
            _context,
            Mock.Of<IResourceReferenceLookup>(),
            Mock.Of<IProjectResourcePlanningContextLookup>(),
            Mock.Of<IWorkItemReferenceLookup>(),
            Mock.Of<IWorkItemAssigneeLookup>(),
            Mock.Of<IManagementScopeResolver>(),
            Mock.Of<IManagementContextAccessor>(),
            _audit.Object,
            _http.Object);
        var actualWorkUpdate = await timeEntryService.UpdateManualAsync(_timeEntryId, new UpdateTimeEntryDto
        {
            ActualSeconds = 32_400,
            BreakSeconds = 3_600,
            RowVersion = timeEntry.RowVersion,
        }, _userId, _tenantId);
        Assert.True(actualWorkUpdate.IsSuccess, actualWorkUpdate.Error?.Message);

        var afterActualWork = await ReadMetricsAsync(planLookup, laborLookup, directCostLookup);
        AssertClose(afterProgress.PlannedValue, afterActualWork.PlannedValue);
        AssertClose(afterProgress.BudgetAtCompletion, afterActualWork.BudgetAtCompletion);
        AssertClose(afterProgress.EarnedValue, afterActualWork.EarnedValue);
        AssertClose(90m, afterActualWork.ActualCost);
        AssertClose(afterProgress.SchedulePerformanceIndex, afterActualWork.SchedulePerformanceIndex);
        Assert.NotEqual(afterProgress.CostPerformanceIndex, afterActualWork.CostPerformanceIndex);

        var projectLookup = new Mock<IProjectReferenceLookup>();
        projectLookup.Setup(mock => mock.FindAsync(_projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectReference(_projectId, project.Name, project.Currency));
        var costService = new CostService(_context, _audit.Object, projectLookup.Object, _http.Object);
        var directCost = await costService.CreateAsync(new CreateCostEntryDto
        {
            ProjectId = _projectId,
            Category = "Travel",
            Description = "Posted direct travel cost",
            Amount = 50m,
            Currency = "USD",
            IncurredDate = _statusDate,
            Type = "Actual",
            Status = "Posted",
        }, _userId.ToString());
        Assert.True(directCost.IsSuccess, directCost.Error?.Message);

        var withUnbaselinedDirectCost = await ReadMetricsAsync(planLookup, laborLookup, directCostLookup);
        AssertClose(afterActualWork.PlannedValue, withUnbaselinedDirectCost.PlannedValue);
        AssertClose(afterActualWork.EarnedValue, withUnbaselinedDirectCost.EarnedValue);
        AssertClose(140m, withUnbaselinedDirectCost.ActualCost);
        AssertClose(afterActualWork.SchedulePerformanceIndex, withUnbaselinedDirectCost.SchedulePerformanceIndex);
        Assert.Null(withUnbaselinedDirectCost.CostVariance);
        Assert.Null(withUnbaselinedDirectCost.CostPerformanceIndex);
        Assert.Equal("NON_LABOR_COSTS_NOT_BASELINED", withUnbaselinedDirectCost.CostVarianceUnavailableReason);
        Assert.Equal("NON_LABOR_COSTS_NOT_BASELINED", withUnbaselinedDirectCost.CostPerformanceIndexUnavailableReason);
    }

    private async Task<ScenarioMetrics> ReadMetricsAsync(
        ProjectEvmPlanLookup planLookup,
        ProjectEvmLaborCostLookup laborLookup,
        ProjectEvmDirectCostLookup directCostLookup)
    {
        var plan = await planLookup.GetAsync(_projectId, _tenantId);
        var labor = await laborLookup.GetAsync(_tenantId, _projectId, "USD", _statusDate, plan.Assignments);
        var direct = await directCostLookup.GetAsync(_tenantId, _projectId, "USD", _statusDate);
        var actualCost = labor.HasActualLaborData && labor.ActualLaborCost.HasValue
            ? labor.ActualLaborCost.Value + (direct.HasData ? direct.Amount : 0m)
            : (decimal?)null;
        var metrics = ProjectEvmCalculator.Calculate(
            labor.PlannedValue, labor.EarnedValue, actualCost,
            labor.PlannedValueUnavailableReason, labor.EarnedValueUnavailableReason,
            actualCost.HasValue ? null : labor.ActualLaborCostUnavailableReason,
            direct.HasData && actualCost.HasValue ? "NON_LABOR_COSTS_NOT_BASELINED" : null);
        return new ScenarioMetrics(
            plan.BaselineId,
            labor.BudgetAtCompletion,
            labor.PlannedValue,
            labor.EarnedValue,
            actualCost,
            metrics.CostVariance,
            metrics.SchedulePerformanceIndex,
            metrics.CostPerformanceIndex,
            metrics.CostVarianceUnavailableReason,
            metrics.CostPerformanceIndexUnavailableReason);
    }

    private PlanActivity NewActivity(Guid id, Guid versionId, DateTime start, DateTime end, decimal progress)
        => new()
        {
            Id = id,
            TenantId = _tenantId,
            CreatedBy = _userId.ToString(),
            PlanVersionId = versionId,
            Code = "A-1",
            Name = "Implementation",
            Type = PlanActivityType.Task,
            SchedulingMode = PlanSchedulingMode.Manual,
            StartDate = start,
            EndDate = end,
            Duration = 20,
            EstimateHours = 40m,
            Progress = progress,
        };

    private static void AssertClose(decimal? expected, decimal? actual)
    {
        Assert.True(expected.HasValue && actual.HasValue, "Expected and actual EVM values must be available.");
        Assert.InRange(Math.Abs(expected!.Value - actual!.Value), 0m, 0.0001m);
    }

    private void SetTenant()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenantId", _tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim("permission", TimeTrackingPermissions.ManageOwn),
        }, "EVM test"));
        _http.Setup(item => item.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_http.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed record ScenarioMetrics(
        Guid? BaselineId,
        decimal? BudgetAtCompletion,
        decimal? PlannedValue,
        decimal? EarnedValue,
        decimal? ActualCost,
        decimal? CostVariance,
        decimal? SchedulePerformanceIndex,
        decimal? CostPerformanceIndex,
        string? CostVarianceUnavailableReason,
        string? CostPerformanceIndexUnavailableReason);
}
