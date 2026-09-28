using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Persistence;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class PlanningCalendarQueryFilterTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor = new();
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _templateA = Guid.NewGuid();
    private readonly Guid _templateB = Guid.NewGuid();
    private readonly Guid _planCalendarA = Guid.NewGuid();
    private readonly Guid _planCalendarB = Guid.NewGuid();

    public PlanningCalendarQueryFilterTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);

        // EnsureCreated forces full model construction, including the TPH calendar hierarchy.
        _context.Database.EnsureCreated();
        Seed();
    }

    [Fact]
    public async Task RootAndDerivedCalendarQueries_ApplyTenantAndSoftDeleteFilters()
    {
        Assert.NotNull(_context.Model.FindEntityType(typeof(PlanningCalendar)));

        SetTenant(_tenantA);

        var rootRows = await _context.Set<PlanningCalendar>().ToListAsync();
        var templates = await _context.CalendarTemplates.ToListAsync();
        var planCalendars = await _context.PlanCalendars.ToListAsync();

        Assert.Equal(2, rootRows.Count);
        Assert.Single(templates);
        Assert.Single(planCalendars);
        Assert.Equal(_templateA, templates[0].Id);
        Assert.Equal(_planCalendarA, planCalendars[0].Id);
        Assert.DoesNotContain(rootRows, calendar => calendar.TenantId == _tenantB);

        var deletedTemplate = await _context.CalendarTemplates.IgnoreQueryFilters().SingleAsync(t => t.Id == _templateA);
        deletedTemplate.IsDeleted = true;
        await _context.SaveChangesAsync();

        Assert.Empty(await _context.CalendarTemplates.ToListAsync());
        Assert.Single(await _context.PlanCalendars.ToListAsync());
        Assert.Single(await _context.Set<PlanningCalendar>().ToListAsync());

        SetTenant(_tenantB);
        Assert.Single(await _context.CalendarTemplates.ToListAsync());
        Assert.Single(await _context.PlanCalendars.ToListAsync());
        Assert.Equal(_templateB, (await _context.CalendarTemplates.ToListAsync())[0].Id);
        Assert.Equal(_planCalendarB, (await _context.PlanCalendars.ToListAsync())[0].Id);
    }

    private void Seed()
    {
        var planA = new Plan { TenantId = _tenantA, ProjectId = Guid.NewGuid(), Name = "Plan A", PlanType = PlanType.Execution, CreatedBy = "test" };
        var planB = new Plan { TenantId = _tenantB, ProjectId = Guid.NewGuid(), Name = "Plan B", PlanType = PlanType.Execution, CreatedBy = "test" };
        var versionA = new PlanVersion { TenantId = _tenantA, PlanId = planA.Id, VersionNumber = 1, Type = PlanVersionType.Working, Status = PlanVersionStatus.Active, CreatedBy = "test" };
        var versionB = new PlanVersion { TenantId = _tenantB, PlanId = planB.Id, VersionNumber = 1, Type = PlanVersionType.Working, Status = PlanVersionStatus.Active, CreatedBy = "test" };

        _context.AddRange(
            planA,
            planB,
            versionA,
            versionB,
            new CalendarTemplate { Id = _templateA, TenantId = _tenantA, Name = "Tenant A", CreatedBy = "test" },
            new CalendarTemplate { Id = _templateB, TenantId = _tenantB, Name = "Tenant B", CreatedBy = "test" },
            new PlanCalendar { Id = _planCalendarA, TenantId = _tenantA, ProjectId = planA.ProjectId, PlanVersionId = versionA.Id, Name = "Project A", CreatedBy = "test" },
            new PlanCalendar { Id = _planCalendarB, TenantId = _tenantB, ProjectId = planB.ProjectId, PlanVersionId = versionB.Id, Name = "Project B", CreatedBy = "test" });
        _context.SaveChanges();
    }

    private void SetTenant(Guid tenantId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenantId", tenantId.ToString()) }, "test"))
        };
        _accessor.Setup(x => x.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_accessor.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
