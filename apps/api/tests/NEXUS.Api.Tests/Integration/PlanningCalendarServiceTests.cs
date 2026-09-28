using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public class PlanningCalendarServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlanningCalendarTestDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor = new();
    private readonly Mock<IAuditService> _auditService = new();
    private readonly PlanningCalendarService _service;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public PlanningCalendarServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new PlanningCalendarTestDbContext(
            new DbContextOptionsBuilder<PlanningCalendarTestDbContext>().UseSqlite(_connection).Options,
            _accessor.Object);
        _context.Database.EnsureCreated();
        _service = new PlanningCalendarService(_context, _auditService.Object, _accessor.Object);
    }

    private void SetTenant(Guid tenantId, Guid userId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("tenantId", tenantId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            }, "mock"))
        };
        _accessor.Setup(a => a.HttpContext).Returns(httpContext);
    }

    private async Task<(Guid PlanId, Guid WorkingVersionId)> CreatePlanAsync()
    {
        SetTenant(_tenantA, _userA);
        var plan = new Plan { TenantId = _tenantA, ProjectId = _projectId, PlanType = PlanType.Execution, Name = "Execution", CreatedBy = _userA.ToString() };
        var version = new PlanVersion { TenantId = _tenantA, PlanId = plan.Id, VersionNumber = 1, Type = PlanVersionType.Working, Status = PlanVersionStatus.Active, Name = "Working v1", CreatedBy = _userA.ToString() };
        _context.Plans.Add(plan);
        _context.PlanVersions.Add(version);
        await _context.SaveChangesAsync();
        return (plan.Id, version.Id);
    }

    private static List<CalendarDayRuleDto> StandardDays() =>
        Enum.GetValues<DayOfWeek>().Select(day => new CalendarDayRuleDto
        {
            DayOfWeek = day,
            IsWorkingDay = day is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
            StartTime = day is not (DayOfWeek.Saturday or DayOfWeek.Sunday) ? new TimeSpan(9, 0, 0) : null,
            EndTime = day is not (DayOfWeek.Saturday or DayOfWeek.Sunday) ? new TimeSpan(17, 0, 0) : null
        }).ToList();

    private async Task<CalendarTemplateDto> CreateTemplateAsync(string name = "Standard")
    {
        SetTenant(_tenantA, _userA);
        var result = await _service.CreateTemplateAsync(new CreateCalendarTemplateDto { Name = name, DayRules = StandardDays() }, _userA.ToString());
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    [Fact]
    public async Task CreateTemplate_PersistsDayTimeRulesAndAudits()
    {
        var result = await _service.CreateTemplateAsync(new CreateCalendarTemplateDto { Name = "Standard", DayRules = StandardDays() }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value!.DayRules.Count);
        Assert.Equal(new TimeSpan(9, 0, 0), result.Value.DayRules.Single(d => d.DayOfWeek == DayOfWeek.Monday).StartTime);
        _auditService.Verify(a => a.LogAsync(It.Is<AuditEntry>(e => e.EntityType == "CalendarTemplate" && e.Action == "Create"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTemplate_InvalidWorkingRule_ReturnsValidation()
    {
        var days = StandardDays();
        days[0].EndTime = new TimeSpan(8, 0, 0);

        var result = await _service.CreateTemplateAsync(new CreateCalendarTemplateDto { Name = "Invalid", DayRules = days }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task ReadTemplates_IsTenantScoped()
    {
        var template = await CreateTemplateAsync();
        var list = await _service.GetTemplatesAsync();
        var single = await _service.GetTemplateAsync(template.Id);

        Assert.True(list.IsSuccess);
        Assert.Single(list.Value!);
        Assert.True(single.IsSuccess);
        Assert.Equal(template.Id, single.Value!.Id);

        SetTenant(_tenantB, Guid.NewGuid());
        var hidden = await _service.GetTemplateAsync(template.Id);
        Assert.True(hidden.IsFailure);
        Assert.Equal("NOT_FOUND", hidden.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task CreatePlanCalendar_FromTenantTemplate_CopiesRulesAndScopesToWorkingVersion()
    {
        var plan = await CreatePlanAsync();
        var template = await CreateTemplateAsync();

        var result = await _service.CreatePlanCalendarAsync(_projectId, plan.PlanId, new CreatePlanCalendarDto { TemplateId = template.Id, Name = "Project Standard" }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(template.Id, result.Value!.TemplateId);
        Assert.Equal(plan.WorkingVersionId, result.Value.PlanVersionId);
        Assert.Equal(7, result.Value.DayRules.Count);
    }

    [Fact]
    public async Task CreatePlanCalendar_WithoutTemplate_IsProjectSpecific()
    {
        var plan = await CreatePlanAsync();
        var result = await _service.CreatePlanCalendarAsync(_projectId, plan.PlanId, new CreatePlanCalendarDto { Name = "Shutdown", DayRules = StandardDays() }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.TemplateId);
        Assert.Equal(_projectId, result.Value.ProjectId);
    }

    [Fact]
    public async Task CrossTenantTemplateReference_IsRejected()
    {
        await CreatePlanAsync();
        var template = await CreateTemplateAsync();
        SetTenant(_tenantB, Guid.NewGuid());
        var foreignPlan = new Plan { TenantId = _tenantB, ProjectId = _projectId, PlanType = PlanType.Execution, Name = "Tenant B Plan", CreatedBy = "tenant-b" };
        var foreignVersion = new PlanVersion { TenantId = _tenantB, PlanId = foreignPlan.Id, VersionNumber = 1, Type = PlanVersionType.Working, Status = PlanVersionStatus.Active, Name = "Working v1", CreatedBy = "tenant-b" };
        _context.Plans.Add(foreignPlan);
        _context.PlanVersions.Add(foreignVersion);
        await _context.SaveChangesAsync();

        var result = await _service.CreatePlanCalendarAsync(_projectId, foreignPlan.Id, new CreatePlanCalendarDto { TemplateId = template.Id, Name = "Foreign" }, "tenant-b");

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task WrongProjectReference_IsNotFound()
    {
        var plan = await CreatePlanAsync();
        var result = await _service.CreatePlanCalendarAsync(Guid.NewGuid(), plan.PlanId, new CreatePlanCalendarDto { Name = "Wrong" }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task BaselineCalendar_CanBeReadButNotUpdatedOrDeleted()
    {
        var plan = await CreatePlanAsync();
        var baseline = new PlanVersion { TenantId = _tenantA, PlanId = plan.PlanId, VersionNumber = 2, Type = PlanVersionType.Baseline, Status = PlanVersionStatus.Approved, Name = "Baseline", CreatedBy = _userA.ToString() };
        var calendar = new PlanCalendar { TenantId = _tenantA, ProjectId = _projectId, PlanVersion = baseline, Name = "Baseline Calendar", CreatedBy = _userA.ToString() };
        _context.PlanCalendars.Add(calendar);
        await _context.SaveChangesAsync();

        var read = await _service.GetByVersionAsync(_projectId, plan.PlanId, baseline.Id);
        var update = await _service.UpdatePlanCalendarAsync(_projectId, plan.PlanId, calendar.Id, new UpdatePlanCalendarDto { Name = "Changed", RowVersion = calendar.RowVersion }, _userA.ToString());
        var delete = await _service.DeletePlanCalendarAsync(_projectId, plan.PlanId, calendar.Id, calendar.RowVersion, _userA.ToString());

        Assert.True(read.IsSuccess);
        Assert.Single(read.Value!);
        Assert.True(update.IsFailure);
        Assert.True(delete.IsFailure);
        Assert.Equal("VALIDATION_ERROR", update.Error!.Code, ignoreCase: true);
        Assert.Equal("VALIDATION_ERROR", delete.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_StaleRowVersion_ReturnsConflict()
    {
        var plan = await CreatePlanAsync();
        var created = (await _service.CreatePlanCalendarAsync(_projectId, plan.PlanId, new CreatePlanCalendarDto { Name = "Calendar", DayRules = StandardDays() }, _userA.ToString())).Value!;
        var result = await _service.UpdatePlanCalendarAsync(_projectId, plan.PlanId, created.Id, new UpdatePlanCalendarDto { Name = "Changed", RowVersion = new byte[8] }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_WithCurrentRowVersion_PersistsCalendar()
    {
        var plan = await CreatePlanAsync();
        var created = (await _service.CreatePlanCalendarAsync(_projectId, plan.PlanId, new CreatePlanCalendarDto { Name = "Calendar", DayRules = StandardDays() }, _userA.ToString())).Value!;

        var result = await _service.UpdatePlanCalendarAsync(_projectId, plan.PlanId, created.Id, new UpdatePlanCalendarDto
        {
            Name = "Calendar updated",
            DayRules = StandardDays(),
            Holidays = new List<CalendarHolidayDto>(),
            RowVersion = created.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Calendar updated", result.Value!.Name);
        Assert.Equal("Calendar updated", (await _context.PlanCalendars.SingleAsync(c => c.Id == created.Id)).Name);
    }

    [Fact]
    public async Task Delete_SoftDeletesWorkingCalendar()
    {
        var plan = await CreatePlanAsync();
        var created = (await _service.CreatePlanCalendarAsync(_projectId, plan.PlanId, new CreatePlanCalendarDto { Name = "Calendar", DayRules = StandardDays() }, _userA.ToString())).Value!;

        var result = await _service.DeletePlanCalendarAsync(_projectId, plan.PlanId, created.Id, created.RowVersion, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.True((await _context.PlanCalendars.IgnoreQueryFilters().SingleAsync(c => c.Id == created.Id)).IsDeleted);
        Assert.Empty((await _service.GetByPlanAsync(_projectId, plan.PlanId)).Value!);
        _auditService.Verify(a => a.LogAsync(It.Is<AuditEntry>(e => e.EntityType == "PlanCalendar" && e.Action == "Delete"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TenantB_CannotReadOrModifyTenantACalendar()
    {
        var plan = await CreatePlanAsync();
        var created = (await _service.CreatePlanCalendarAsync(_projectId, plan.PlanId, new CreatePlanCalendarDto { Name = "Calendar", DayRules = StandardDays() }, _userA.ToString())).Value!;
        SetTenant(_tenantB, Guid.NewGuid());

        var read = await _service.GetByPlanAsync(_projectId, plan.PlanId);
        var update = await _service.UpdatePlanCalendarAsync(_projectId, plan.PlanId, created.Id, new UpdatePlanCalendarDto { Name = "Changed", RowVersion = created.RowVersion }, "tenant-b");

        Assert.True(read.IsFailure);
        Assert.True(update.IsFailure);
        Assert.Equal("NOT_FOUND", read.Error!.Code, ignoreCase: true);
        Assert.Equal("NOT_FOUND", update.Error!.Code, ignoreCase: true);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

internal sealed class PlanningCalendarTestDbContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PlanningCalendarTestDbContext(DbContextOptions<PlanningCalendarTestDbContext> options, IHttpContextAccessor httpContextAccessor) : base(options) => _httpContextAccessor = httpContextAccessor;

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanVersion> PlanVersions => Set<PlanVersion>();
    public DbSet<CalendarTemplate> CalendarTemplates => Set<CalendarTemplate>();
    public DbSet<PlanCalendar> PlanCalendars => Set<PlanCalendar>();
    public DbSet<CalendarDayRule> CalendarDayRules => Set<CalendarDayRule>();
    public DbSet<CalendarHoliday> CalendarHolidays => Set<CalendarHoliday>();

    private Guid CurrentTenantId => Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value, out var tenantId)
        ? tenantId : Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<PlanTextFormat>();
        ConfigureBase(modelBuilder.Entity<Plan>());
        ConfigureBase(modelBuilder.Entity<PlanVersion>());
        ConfigureBase(modelBuilder.Entity<PlanningCalendar>());
        ConfigureBase(modelBuilder.Entity<CalendarDayRule>());
        ConfigureBase(modelBuilder.Entity<CalendarHoliday>());

        modelBuilder.Entity<Plan>(e => e.HasMany(p => p.Versions).WithOne(v => v.Plan).HasForeignKey(v => v.PlanId).OnDelete(DeleteBehavior.Cascade));
        modelBuilder.Entity<PlanVersion>(e =>
        {
            e.HasMany(v => v.Calendars).WithOne(c => c.PlanVersion).HasForeignKey(c => c.PlanVersionId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(v => v.Activities); e.Ignore(v => v.WbsNodes); e.Ignore(v => v.Deliverables); e.Ignore(v => v.Dependencies); e.Ignore(v => v.Assignments);
        });

        modelBuilder.Entity<PlanningCalendar>().ToTable("PlanningCalendars").HasDiscriminator<string>("CalendarKind")
            .HasValue<CalendarTemplate>("Template").HasValue<PlanCalendar>("Plan");
        modelBuilder.Entity<CalendarTemplate>().HasMany(t => t.ProjectOverrides).WithOne(c => c.Template).HasForeignKey(c => c.TemplateId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PlanningCalendar>().HasMany(c => c.DayRules).WithOne(r => r.PlanningCalendar).HasForeignKey(r => r.PlanningCalendarId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PlanningCalendar>().HasMany(c => c.Holidays).WithOne(h => h.PlanningCalendar).HasForeignKey(h => h.PlanningCalendarId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PlanCalendar>().Property(c => c.ProjectId).IsRequired();
        modelBuilder.Entity<PlanCalendar>().Property(c => c.PlanVersionId).IsRequired();
        modelBuilder.Entity<CalendarDayRule>().Property(r => r.RowVersion).HasColumnType("BLOB").IsRequired().HasDefaultValueSql("randomblob(8)").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        modelBuilder.Entity<CalendarHoliday>().Property(h => h.RowVersion).HasColumnType("BLOB").IsRequired().HasDefaultValueSql("randomblob(8)").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        modelBuilder.Entity<PlanningCalendar>().Property(c => c.RowVersion).HasColumnType("BLOB").IsRequired().HasDefaultValueSql("randomblob(8)").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
    }

    private void ConfigureBase<TEntity>(EntityTypeBuilder<TEntity> entity) where TEntity : BaseEntity
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CreatedBy).IsRequired();
        entity.HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
    }
}
