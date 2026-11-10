using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Domain;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class PlanVersionComparisonServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Mock<IHttpContextAccessor> _accessor = new();
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _planId = Guid.NewGuid();
    private readonly Guid _otherPlanId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _resource1 = Guid.NewGuid();
    private readonly PlanningWorkflowTestDbContext _context;
    private readonly PlanVersionComparisonService _service;

    public PlanVersionComparisonServiceTests()
    {
        _connection.Open();
        _context = new PlanningWorkflowTestDbContext(
            new DbContextOptionsBuilder<PlanningWorkflowTestDbContext>().UseSqlite(_connection).Options,
            _accessor.Object);
        _context.Database.EnsureCreated();
        SetTenant(_tenantA);
        _service = new PlanVersionComparisonService(_context);
    }

    [Fact]
    public async Task IdenticalClonedVersions_AreReportedUnchanged()
    {
        await AddPlanAsync(_planId);
        var from = AddVersion(_planId, _tenantA, 1, PlanVersionStatus.Approved);
        var to = AddVersion(_planId, _tenantA, 2, PlanVersionStatus.Superseded);
        AddModel(from, "Original", includeSecondActivity: false);
        AddModel(to, "Original", includeSecondActivity: false);
        await _context.SaveChangesAsync();

        var result = await _service.CompareAsync(_planId, from.Id, to.Id);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.All(new[] { result.Value!.Wbs, result.Value.Activities, result.Value.Dependencies,
            result.Value.Deliverables, result.Value.Assignments, result.Value.Calendars },
            changes => Assert.All(changes, change => Assert.Equal("Unchanged", change.ChangeType)));
        Assert.All(result.Value.Summary.Values, summary => Assert.Equal(summary.Total, summary.Unchanged));
    }

    [Fact]
    public async Task Comparison_TreatsUnspecifiedPersistedTextFormatAsAbsent()
    {
        await AddPlanAsync(_planId);
        var from = AddVersion(_planId, _tenantA, 1, PlanVersionStatus.Approved);
        var to = AddVersion(_planId, _tenantA, 2, PlanVersionStatus.Superseded);
        AddModel(from, "Original", includeSecondActivity: false);
        AddModel(to, "Original", includeSecondActivity: false);
        to.WbsNodes.Single(node => node.Code == "1").TextFormat = PlanTextFormat.Unspecified();
        to.Activities.Single(activity => activity.Code == "A1").TextFormat = PlanTextFormat.Unspecified();
        await _context.SaveChangesAsync();

        var result = await _service.CompareAsync(_planId, from.Id, to.Id);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var wbs = Assert.Single(result.Value!.Wbs, change => change.Key == "1");
        var activity = Assert.Single(result.Value.Activities, change => change.Key == "activity:A1");
        Assert.Equal("Unchanged", wbs.ChangeType);
        Assert.Equal("Unchanged", activity.ChangeType);
        Assert.DoesNotContain("TextFormat", wbs.Fields.Keys);
        Assert.DoesNotContain("TextFormat", activity.Fields.Keys);
    }

    [Fact]
    public async Task Comparison_ReturnsAddedRemovedModifiedAndFieldLevelValues()
    {
        await AddPlanAsync(_planId);
        var from = AddVersion(_planId, _tenantA, 1, PlanVersionStatus.Approved);
        var to = AddVersion(_planId, _tenantA, 2, PlanVersionStatus.Superseded);
        AddModel(from, "Before", includeSecondActivity: false);
        AddModel(to, "After", includeSecondActivity: true);
        to.Activities.Single(a => a.Code == "A1").Duration = 3;
        to.Dependencies.Single(d => d.ActivityId == to.Activities.Single(a => a.Code == "A1").Id).Type = PlanDependencyType.StartToStart;
        to.Assignments.Single(a => a.ResourceId == _resource1).AllocationPct = 75;
        to.Deliverables.Single(d => d.Name == "D1").Description = "Updated";
        to.Calendars.Single().DayRules.Single().StartTime = new TimeSpan(10, 0, 0);
        await _context.SaveChangesAsync();

        var result = await _service.CompareAsync(_planId, from.Id, to.Id);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value!.Wbs, d => d.ChangeType == "Modified" && d.Fields.ContainsKey("Name"));
        var activity = Assert.Single(result.Value.Activities, d => d.Key == "activity:A1");
        Assert.Equal("Modified", activity.ChangeType);
        Assert.Equal(1, activity.Fields["Duration"].Before);
        Assert.Equal(3, activity.Fields["Duration"].After);
        Assert.Contains(result.Value.Activities, d => d.ChangeType == "Added" && d.Key == "activity:A2");
        Assert.Contains(result.Value.Deliverables, d => d.ChangeType == "Removed" && d.Key == "deliverable:D0");
        Assert.Contains(result.Value.Assignments, d => d.ChangeType == "Added");
        Assert.Contains(result.Value.Calendars, d => d.ChangeType == "Modified" && d.Fields.ContainsKey("DayRules"));
        Assert.True(result.Value.Summary["Activities"].Modified > 0);
        Assert.True(result.Value.Summary["Activities"].Added > 0);
    }

    [Fact]
    public async Task RelationshipChanges_UseLogicalKeysAndReportEdges()
    {
        await AddPlanAsync(_planId);
        var from = AddVersion(_planId, _tenantA, 1, PlanVersionStatus.Approved);
        var to = AddVersion(_planId, _tenantA, 2, PlanVersionStatus.Superseded);
        AddModel(from, "Before", includeSecondActivity: true);
        AddModel(to, "Before", includeSecondActivity: true);
        var fromA1 = from.Activities.Single(a => a.Code == "A1");
        var fromA2 = from.Activities.Single(a => a.Code == "A2");
        var toA1 = to.Activities.Single(a => a.Code == "A1");
        var toA2 = to.Activities.Single(a => a.Code == "A2");
        from.Dependencies.Clear();
        from.Dependencies.Add(new PlanDependency { TenantId = _tenantA, PlanVersionId = from.Id, ActivityId = fromA2.Id, PredecessorActivityId = fromA1.Id, CreatedBy = "test" });
        to.Dependencies.Clear();
        to.Dependencies.Add(new PlanDependency { TenantId = _tenantA, PlanVersionId = to.Id, ActivityId = toA1.Id, PredecessorActivityId = toA2.Id, CreatedBy = "test" });
        toA1.WbsId = to.WbsNodes.Single(n => n.Code == "2").Id;
        await _context.SaveChangesAsync();

        var result = await _service.CompareAsync(_planId, from.Id, to.Id);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value!.Dependencies, d => d.ChangeType == "Removed");
        Assert.Contains(result.Value.Dependencies, d => d.ChangeType == "Added");
        var activity = Assert.Single(result.Value.Activities, d => d.Key == "activity:A1");
        Assert.Equal("Modified", activity.ChangeType);
        Assert.Contains("Wbs", activity.Fields.Keys);
    }

    [Fact]
    public async Task Comparison_RejectsCrossPlanAndCrossTenantVersions()
    {
        await AddPlanAsync(_planId);
        await AddPlanAsync(_otherPlanId);
        var from = AddVersion(_planId, _tenantA, 1, PlanVersionStatus.Approved);
        var otherPlanVersion = AddVersion(_otherPlanId, _tenantA, 1, PlanVersionStatus.Approved);
        var otherTenantVersion = AddVersion(_planId, _tenantB, 2, PlanVersionStatus.Superseded);
        await _context.SaveChangesAsync();

        var crossPlan = await _service.CompareAsync(_planId, from.Id, otherPlanVersion.Id);
        Assert.True(crossPlan.IsFailure);
        Assert.Equal("VALIDATION_ERROR", crossPlan.Error!.Code);

        var crossTenant = await _service.CompareAsync(_planId, from.Id, otherTenantVersion.Id);
        Assert.True(crossTenant.IsFailure);
        Assert.Equal("NOT_FOUND", crossTenant.Error!.Code);
    }

    private async Task AddPlanAsync(Guid id)
    {
        _context.Plans.Add(new Plan { Id = id, TenantId = _tenantA, ProjectId = _projectId, PlanType = PlanType.Execution, Name = id.ToString(), CreatedBy = "test" });
        await _context.SaveChangesAsync();
    }

    private PlanVersion AddVersion(Guid planId, Guid tenantId, int number, PlanVersionStatus status)
    {
        var version = new PlanVersion { Id = Guid.NewGuid(), PlanId = planId, TenantId = tenantId, VersionNumber = number, Type = PlanVersionType.Baseline, Status = status, CreatedBy = "test" };
        _context.PlanVersions.Add(version);
        return version;
    }

    private void AddModel(PlanVersion version, string label, bool includeSecondActivity)
    {
        var wbs1 = new PlanWbsNode { TenantId = _tenantA, PlanVersionId = version.Id, Code = "1", Name = label, CreatedBy = "test" };
        var wbs2 = new PlanWbsNode { TenantId = _tenantA, PlanVersionId = version.Id, Code = "2", Name = "Second", CreatedBy = "test" };
        var a1 = new PlanActivity { TenantId = _tenantA, PlanVersionId = version.Id, Code = "A1", Name = label, Duration = 1, WbsId = wbs1.Id, CreatedBy = "test" };
        var d1 = new PlanDeliverable { TenantId = _tenantA, PlanVersionId = version.Id, Name = includeSecondActivity ? "D1" : "D0", WbsId = wbs1.Id, ActivityId = a1.Id, CreatedBy = "test" };
        wbs1.ActivityId = a1.Id;
        wbs1.DeliverableId = d1.Id;
        version.WbsNodes.Add(wbs1);
        version.Activities.Add(a1);
        version.Deliverables.Add(d1);
        version.Dependencies.Add(new PlanDependency { TenantId = _tenantA, PlanVersionId = version.Id, ActivityId = a1.Id, PredecessorActivityId = a1.Id, CreatedBy = "test" });
        version.Assignments.Add(new PlanAssignment { TenantId = _tenantA, PlanVersionId = version.Id, ActivityId = a1.Id, ResourceId = _resource1, AllocationPct = 50, CreatedBy = "test" });
        if (includeSecondActivity)
        {
            var a2 = new PlanActivity { TenantId = _tenantA, PlanVersionId = version.Id, Code = "A2", Name = "Second activity", Duration = 2, WbsId = wbs2.Id, CreatedBy = "test" };
            version.WbsNodes.Add(wbs2);
            version.Activities.Add(a2);
            version.Deliverables.Add(new PlanDeliverable { TenantId = _tenantA, PlanVersionId = version.Id, Name = "D2", WbsId = wbs2.Id, ActivityId = a2.Id, CreatedBy = "test" });
            version.Dependencies.Add(new PlanDependency { TenantId = _tenantA, PlanVersionId = version.Id, ActivityId = a2.Id, PredecessorActivityId = a1.Id, CreatedBy = "test" });
            version.Assignments.Add(new PlanAssignment { TenantId = _tenantA, PlanVersionId = version.Id, ActivityId = a2.Id, ResourceId = Guid.NewGuid(), CreatedBy = "test" });
        }
        var calendar = new PlanCalendar { TenantId = _tenantA, ProjectId = _projectId, PlanVersionId = version.Id, Name = "Calendar", CreatedBy = "test" };
        calendar.DayRules.Add(new CalendarDayRule { TenantId = _tenantA, PlanningCalendarId = calendar.Id, DayOfWeek = DayOfWeek.Monday, IsWorkingDay = true, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0), CreatedBy = "test" });
        calendar.Holidays.Add(new CalendarHoliday { TenantId = _tenantA, PlanningCalendarId = calendar.Id, Name = "Holiday", Date = new DateTime(2026, 1, 1), CreatedBy = "test" });
        version.Calendars.Add(calendar);
    }

    private void SetTenant(Guid tenantId) => _accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenantId", tenantId.ToString()) }, "test"))
    });

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
