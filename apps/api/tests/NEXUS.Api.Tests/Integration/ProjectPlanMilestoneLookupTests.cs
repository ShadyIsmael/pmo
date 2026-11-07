using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Persistence;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class ProjectPlanMilestoneLookupTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public ProjectPlanMilestoneLookupTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task ReturnsNextFiveIncompleteMilestonesFromTheDefaultPlansActiveWorkingVersion()
    {
        SetTenant();
        var statusDate = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);
        var otherProjectId = Guid.NewGuid();
        _context.Projects.AddRange(
            new Project { Id = _projectId, TenantId = _tenantId, Code = "P-1", Name = "Project", Status = "Active", Currency = "USD", CreatedBy = _userId.ToString() },
            new Project { Id = otherProjectId, TenantId = _tenantId, Code = "P-2", Name = "Other Project", Status = "Active", Currency = "USD", CreatedBy = _userId.ToString() });
        await _context.SaveChangesAsync();

        var previousPlan = NewPlan(_projectId, "Previous Plan");
        var currentPlan = NewPlan(_projectId, "Current Plan");
        var unrelatedPlan = NewPlan(otherProjectId, "Unrelated Project Plan");
        _context.Plans.AddRange(previousPlan, currentPlan, unrelatedPlan);
        await _context.SaveChangesAsync();
        previousPlan.CreatedAt = statusDate.AddDays(-5);
        currentPlan.CreatedAt = statusDate.AddDays(-3);
        unrelatedPlan.CreatedAt = statusDate.AddDays(-1);
        await _context.SaveChangesAsync();

        var previousWorking = NewWorkingVersion(previousPlan, 1);
        var currentWorking = NewWorkingVersion(currentPlan, 1);
        var currentBaseline = new PlanVersion
        {
            TenantId = _tenantId,
            PlanId = currentPlan.Id,
            VersionNumber = 1,
            Type = PlanVersionType.Baseline,
            Status = PlanVersionStatus.Approved,
            Name = "Approved Baseline",
            CreatedBy = _userId.ToString()
        };
        var unrelatedWorking = NewWorkingVersion(unrelatedPlan, 1);
        _context.PlanVersions.AddRange(previousWorking, currentWorking, currentBaseline, unrelatedWorking);
        await _context.SaveChangesAsync();

        var today = statusDate;
        var currentPlanActivities = new[]
        {
            NewMilestone(currentWorking, "Today milestone", today),
            NewMilestone(currentWorking, "Tomorrow milestone", today.AddDays(1)),
            NewMilestone(currentWorking, "A checkpoint", today.AddDays(2)),
            NewMilestone(currentWorking, "B checkpoint", today.AddDays(2)),
            NewMilestone(currentWorking, "C checkpoint", today.AddDays(3)),
            NewMilestone(currentWorking, "D checkpoint", today.AddDays(4)),
            NewMilestone(currentWorking, "Past incomplete milestone", today.AddDays(-1)),
            NewMilestone(currentWorking, "Completed by progress", today.AddDays(1), progress: 100m),
            NewMilestone(currentWorking, "Completed by actual finish", today.AddDays(1), actualFinish: today),
            NewMilestone(currentWorking, "Undated milestone", null),
            NewMilestone(previousWorking, "Previous-plan milestone", today),
            NewMilestone(currentBaseline, "Baseline-only milestone", today),
            NewMilestone(unrelatedWorking, "Other-project milestone", today)
        };
        var deletedMilestone = NewMilestone(currentWorking, "Deleted milestone", today);
        deletedMilestone.IsDeleted = true;
        _context.PlanActivities.AddRange(currentPlanActivities.Append(deletedMilestone));
        await _context.SaveChangesAsync();

        var lookup = new ProjectPlanMilestoneLookup(_context);
        var result = await lookup.GetUpcomingAsync(_projectId, statusDate);

        Assert.Equal(new[] { "Today milestone", "Tomorrow milestone", "A checkpoint", "B checkpoint", "C checkpoint" }, result.Select(item => item.Name));
        Assert.All(result, milestone => Assert.True(milestone.DueDate >= statusDate));
        Assert.DoesNotContain(result, milestone => milestone.CompletedDate.HasValue || milestone.Progress >= 100m);
        Assert.DoesNotContain(result, milestone => milestone.Name.Contains("Previous", StringComparison.Ordinal));
        Assert.DoesNotContain(result, milestone => milestone.Name.Contains("Baseline", StringComparison.Ordinal));
        Assert.DoesNotContain(result, milestone => milestone.Name.Contains("Other-project", StringComparison.Ordinal));
        Assert.DoesNotContain(result, milestone => milestone.Name.Contains("Deleted", StringComparison.Ordinal));
    }

    private void SetTenant()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("tenantId", _tenantId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
            }, "mock"))
        };
        _accessor.Setup(item => item.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_accessor.Object);
    }

    private Plan NewPlan(Guid projectId, string name) => new()
    {
        TenantId = _tenantId,
        ProjectId = projectId,
        Name = name,
        CreatedBy = _userId.ToString()
    };

    private PlanVersion NewWorkingVersion(Plan plan, int versionNumber) => new()
    {
        TenantId = _tenantId,
        PlanId = plan.Id,
        VersionNumber = versionNumber,
        Type = PlanVersionType.Working,
        Status = PlanVersionStatus.Active,
        Name = $"Working v{versionNumber}",
        CreatedBy = _userId.ToString()
    };

    private PlanActivity NewMilestone(PlanVersion version, string name, DateTime? endDate, decimal? progress = 0m, DateTime? actualFinish = null) => new()
    {
        TenantId = _tenantId,
        PlanVersionId = version.Id,
        Code = Guid.NewGuid().ToString("N"),
        Name = name,
        Type = PlanActivityType.Milestone,
        Duration = 0,
        EndDate = endDate,
        Progress = progress,
        ActualFinish = actualFinish,
        CreatedBy = _userId.ToString()
    };

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}