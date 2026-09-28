using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class PlanVersionWorkflowServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Mock<IHttpContextAccessor> _accessor = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _planId = Guid.NewGuid();
    private readonly PlanningWorkflowTestDbContext _context;
    private readonly PlanVersionService _service;

    public PlanVersionWorkflowServiceTests()
    {
        _connection.Open();
        _context = new PlanningWorkflowTestDbContext(
            new DbContextOptionsBuilder<PlanningWorkflowTestDbContext>()
                .UseSqlite(_connection)
                .Options,
            _accessor.Object);
        _context.Database.EnsureCreated();
        _service = new PlanVersionService(_context, _audit.Object, _accessor.Object);
    }

    [Fact]
    public async Task CreateWorkingFromBaseline_ClonesCompleteModelAndRelationships()
    {
        SetTenant(_tenantA);
        var baseline = await SeedVersionAsync(PlanVersionType.Baseline, PlanVersionStatus.Approved, 1, includeModel: true);

        var result = await _service.CreateWorkingFromCurrentBaselineAsync(_planId, _userA.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        var working = await LoadVersionAsync(result.Value!.Id);
        Assert.Equal(PlanVersionType.Working, working.Type);
        Assert.Equal(PlanVersionStatus.Active, working.Status);
        Assert.Equal(baseline.Id, working.SourceBaselineId);
        Assert.Single(await _context.PlanVersions.Where(v => v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active).ToListAsync());
        AssertClonedModel(baseline, working);
        Assert.NotEqual(baseline.Activities.Single().Id, working.Activities.Single().Id);
        Assert.Equal(1, _audit.Invocations.Count(i => i.Method.Name == nameof(IAuditService.LogAsync)));
    }

    [Fact]
    public async Task CreateWorking_WhenActiveWorkingExists_ReturnsConflict()
    {
        SetTenant(_tenantA);
        await SeedVersionAsync(PlanVersionType.Baseline, PlanVersionStatus.Approved, 1, includeModel: false);
        await SeedVersionAsync(PlanVersionType.Working, PlanVersionStatus.Active, 1, includeModel: false);

        var result = await _service.CreateWorkingFromCurrentBaselineAsync(_planId, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task DiscardWorking_SoftDeletesOnlyWorkingVersionAndPreservesBaseline()
    {
        SetTenant(_tenantA);
        var baseline = await SeedVersionAsync(PlanVersionType.Baseline, PlanVersionStatus.Approved, 1, includeModel: true);
        var working = await SeedVersionAsync(PlanVersionType.Working, PlanVersionStatus.Active, 2, includeModel: true);
        var rowVersion = await CurrentRowVersionAsync(working.Id);

        var result = await _service.DiscardWorkingVersionAsync(_planId, rowVersion, _userA.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(await _context.PlanVersions.Where(v => v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active).ToListAsync());
        Assert.True(await _context.PlanVersions.IgnoreQueryFilters().Where(v => v.Id == working.Id).Select(v => v.IsDeleted).SingleAsync());
        var baselineAfter = await LoadVersionAsync(baseline.Id);
        Assert.Equal(baseline.Activities.Single().Name, baselineAfter.Activities.Single().Name);
    }

    [Fact]
    public async Task DiscardWorking_StaleRowVersionReturnsConflict()
    {
        SetTenant(_tenantA);
        await SeedVersionAsync(PlanVersionType.Working, PlanVersionStatus.Active, 1, includeModel: false);

        var result = await _service.DiscardWorkingVersionAsync(_planId, new byte[8], _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task CreateInitialBaseline_CreatesApprovedV1WithoutModifyingWorking()
    {
        SetTenant(_tenantA);
        var working = await SeedVersionAsync(PlanVersionType.Working, PlanVersionStatus.Active, 1, includeModel: true);
        var rowVersion = await CurrentRowVersionAsync(working.Id);

        var result = await _service.CreateInitialBaselineAsync(_planId, rowVersion, _userA.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, result.Value!.VersionNumber);
        Assert.Equal(PlanVersionType.Baseline, result.Value.Type);
        Assert.Equal(PlanVersionStatus.Approved, result.Value.Status);
        Assert.Equal(working.Id, result.Value.SourceVersionId);
        Assert.Empty(await _context.PlanVersions.Where(v => v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active).ToListAsync());
        Assert.True(await _context.PlanVersions.IgnoreQueryFilters().Where(v => v.Id == working.Id).Select(v => v.IsDeleted).SingleAsync());
        var baseline = await LoadVersionAsync(result.Value.Id);
        AssertClonedModel(working, baseline);

        var reopened = await _service.CreateWorkingFromCurrentBaselineAsync(_planId, _userA.ToString());
        Assert.True(reopened.IsSuccess, reopened.Error?.Message);

        var duplicate = await _service.CreateInitialBaselineAsync(_planId, rowVersion, _userA.ToString());
        Assert.True(duplicate.IsFailure);
        Assert.Equal("CONFLICT", duplicate.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task InitialBaseline_NonExecutionPlan_IsRejected()
    {
        SetTenant(_tenantA);
        var plan = new Plan { Id = _planId, TenantId = _tenantA, ProjectId = _projectId, PlanType = PlanType.Recovery, Name = "Recovery", CreatedBy = _userA.ToString() };
        _context.Plans.Add(plan);
        var working = new PlanVersion { PlanId = _planId, TenantId = _tenantA, VersionNumber = 1, Type = PlanVersionType.Working, Status = PlanVersionStatus.Active, CreatedBy = _userA.ToString() };
        _context.PlanVersions.Add(working);
        await _context.SaveChangesAsync();

        var result = await _service.CreateInitialBaselineAsync(_planId, await CurrentRowVersionAsync(working.Id), _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task CaptureBaseline_LeavesWorkingActiveAndAllowsMultipleImmutableSnapshots()
    {
        SetTenant(_tenantA);
        var working = await SeedVersionAsync(PlanVersionType.Working, PlanVersionStatus.Active, 1, includeModel: true);
        working.Activities.Single().StartDate = new DateTime(2026, 2, 1);
        working.Activities.Single().Duration = 4;
        await _context.SaveChangesAsync();

        var first = await _service.CaptureBaselineAsync(_planId, await CurrentRowVersionAsync(working.Id), _userA.ToString());
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(1, first.Value!.VersionNumber);
        Assert.Single(await _context.PlanVersions.Where(v => v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active).ToListAsync());

        working = await LoadVersionAsync(working.Id);
        working.Activities.Single().Duration = 7;
        await _context.SaveChangesAsync();
        var second = await _service.CaptureBaselineAsync(_planId, await CurrentRowVersionAsync(working.Id), _userA.ToString());
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(2, second.Value!.VersionNumber);

        var baselines = await _context.PlanVersions.Where(v => v.Type == PlanVersionType.Baseline).OrderBy(v => v.VersionNumber).ToListAsync();
        Assert.Equal(2, baselines.Count);
        Assert.All(baselines, baseline => Assert.Equal(PlanVersionStatus.Approved, baseline.Status));
        Assert.Equal(4, (await LoadVersionAsync(first.Value.Id)).Activities.Single().Duration);
        Assert.Equal(7, (await LoadVersionAsync(second.Value.Id)).Activities.Single().Duration);
    }

    [Fact]
    public async Task PromoteWorking_CreatesNextBaselineAndNextWorkingAtomically()
    {
        SetTenant(_tenantA);
        var oldBaseline = await SeedVersionAsync(PlanVersionType.Baseline, PlanVersionStatus.Approved, 1, includeModel: true);
        var working = await SeedVersionAsync(PlanVersionType.Working, PlanVersionStatus.Active, 2, includeModel: true);
        var rowVersion = await CurrentRowVersionAsync(working.Id);

        var result = await _service.PromoteWorkingToBaselineAsync(_planId, rowVersion, _userA.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, result.Value!.VersionNumber);
        Assert.Equal(PlanVersionType.Baseline, result.Value.Type);
        Assert.Equal(PlanVersionStatus.Approved, result.Value.Status);
        Assert.Equal(PlanVersionStatus.Superseded, await _context.PlanVersions.Where(v => v.Id == oldBaseline.Id).Select(v => v.Status).SingleAsync());
        Assert.True(await _context.PlanVersions.IgnoreQueryFilters().Where(v => v.Id == working.Id).Select(v => v.IsDeleted).SingleAsync());

        var nextWorking = await _context.PlanVersions.SingleAsync(v => v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active);
        Assert.Equal(3, nextWorking.VersionNumber);
        Assert.Equal(result.Value.Id, nextWorking.SourceBaselineId);
        var promotedBaseline = await LoadVersionAsync(result.Value.Id);
        var clonedWorking = await LoadVersionAsync(nextWorking.Id);
        AssertClonedModel(working, promotedBaseline);
        AssertClonedModel(promotedBaseline, clonedWorking);
    }

    [Fact]
    public async Task BaselineContent_IsImmutableAndTenantIsolated()
    {
        SetTenant(_tenantA);
        var baseline = await SeedVersionAsync(PlanVersionType.Baseline, PlanVersionStatus.Approved, 1, includeModel: true);
        SetTenant(_tenantB);

        var hidden = await _service.GetActiveWorkingVersionAsync(_planId);
        Assert.True(hidden.IsFailure);
        Assert.Equal("NOT_FOUND", hidden.Error!.Code, ignoreCase: true);

        SetTenant(_tenantA);
        var before = await LoadVersionAsync(baseline.Id);
        var created = await _service.CreateWorkingFromCurrentBaselineAsync(_planId, _userA.ToString());
        Assert.True(created.IsSuccess);
        var after = await LoadVersionAsync(baseline.Id);
        Assert.Equal(before.Activities.Single().Name, after.Activities.Single().Name);
        Assert.Equal(before.Calendars.Single().DayRules.Single().StartTime, after.Calendars.Single().DayRules.Single().StartTime);
    }

    [Fact]
    public async Task TextFormatting_PersistsForActivitiesAndWbsNodes()
    {
        SetTenant(_tenantA);
        var version = await SeedVersionAsync(PlanVersionType.Working, PlanVersionStatus.Active, 1, includeModel: true);
        var activity = version.Activities.Single();
        var wbs = version.WbsNodes.Single();
        activity.TextFormat = new PlanTextFormat
        {
            IsBold = true,
            IsUnderline = true,
            TextColorToken = PlanTextColorToken.Violet,
            HighlightColorToken = PlanHighlightColorToken.Pink,
            FontSizeToken = PlanFontSizeToken.ExtraLarge
        };
        wbs.TextFormat = new PlanTextFormat { IsItalic = true, TextColorToken = PlanTextColorToken.Teal };
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var reloaded = await LoadVersionAsync(version.Id);
        Assert.True(reloaded.Activities.Single().TextFormat?.IsBold == true);
        Assert.True(reloaded.Activities.Single().TextFormat?.IsUnderline == true);
        Assert.Equal(PlanTextColorToken.Violet, reloaded.Activities.Single().TextFormat?.TextColorToken);
        Assert.Equal(PlanHighlightColorToken.Pink, reloaded.Activities.Single().TextFormat?.HighlightColorToken);
        Assert.Equal(PlanFontSizeToken.ExtraLarge, reloaded.Activities.Single().TextFormat?.FontSizeToken);
        Assert.True(reloaded.WbsNodes.Single().TextFormat?.IsItalic == true);
        Assert.Equal(PlanTextColorToken.Teal, reloaded.WbsNodes.Single().TextFormat?.TextColorToken);
    }

    private async Task<PlanVersion> SeedVersionAsync(PlanVersionType type, PlanVersionStatus status, int number, bool includeModel)
    {
        var plan = await _context.Plans.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == _planId);
        if (plan is null)
        {
            plan = new Plan
            {
                Id = _planId,
                TenantId = _tenantA,
                ProjectId = _projectId,
                PlanType = PlanType.Execution,
                Name = "Execution",
                CreatedBy = _userA.ToString()
            };
            _context.Plans.Add(plan);
        }

        var version = new PlanVersion
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantA,
            PlanId = _planId,
            VersionNumber = number,
            Type = type,
            Status = status,
            Name = $"{type} v{number}",
            CreatedBy = _userA.ToString()
        };
        _context.PlanVersions.Add(version);
        if (includeModel) AddModel(version);
        await _context.SaveChangesAsync();
        return await LoadVersionAsync(version.Id);
    }

    private void AddModel(PlanVersion version)
    {
        var wbs = new PlanWbsNode { TenantId = _tenantA, PlanVersionId = version.Id, Code = "1", Name = "WBS", TextFormat = new PlanTextFormat { IsBold = true, TextColorToken = PlanTextColorToken.Blue }, CreatedBy = _userA.ToString() };
        var activity = new PlanActivity { TenantId = _tenantA, PlanVersionId = version.Id, Code = "A1", Name = "Activity", WbsId = wbs.Id, TextFormat = new PlanTextFormat { IsItalic = true, HighlightColorToken = PlanHighlightColorToken.Yellow, FontSizeToken = PlanFontSizeToken.Large }, CreatedBy = _userA.ToString() };
        var deliverable = new PlanDeliverable { TenantId = _tenantA, PlanVersionId = version.Id, Name = "Deliverable", WbsId = wbs.Id, ActivityId = activity.Id, CreatedBy = _userA.ToString() };
        wbs.ActivityId = activity.Id;
        wbs.DeliverableId = deliverable.Id;
        var calendar = new PlanCalendar { TenantId = _tenantA, ProjectId = _projectId, PlanVersionId = version.Id, Name = "Calendar", CreatedBy = _userA.ToString() };
        calendar.DayRules.Add(new CalendarDayRule { TenantId = _tenantA, PlanningCalendarId = calendar.Id, DayOfWeek = DayOfWeek.Monday, IsWorkingDay = true, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0), CreatedBy = _userA.ToString() });
        calendar.Holidays.Add(new CalendarHoliday { TenantId = _tenantA, PlanningCalendarId = calendar.Id, Name = "Holiday", Date = new DateTime(2026, 1, 1), CreatedBy = _userA.ToString() });
        version.WbsNodes.Add(wbs);
        version.Activities.Add(activity);
        version.Deliverables.Add(deliverable);
        version.Dependencies.Add(new PlanDependency { TenantId = _tenantA, PlanVersionId = version.Id, ActivityId = activity.Id, PredecessorActivityId = activity.Id, CreatedBy = _userA.ToString() });
        version.Assignments.Add(new PlanAssignment { TenantId = _tenantA, PlanVersionId = version.Id, ActivityId = activity.Id, ResourceId = Guid.NewGuid(), AllocationPct = 50, CreatedBy = _userA.ToString() });
        version.Calendars.Add(calendar);
    }

    private async Task<PlanVersion> LoadVersionAsync(Guid id) =>
        await _context.PlanVersions
            .Include(v => v.WbsNodes)
            .Include(v => v.Activities)
            .Include(v => v.Deliverables)
            .Include(v => v.Dependencies)
            .Include(v => v.Assignments)
            .Include(v => v.Calendars).ThenInclude(c => c.DayRules)
            .Include(v => v.Calendars).ThenInclude(c => c.Holidays)
            .SingleAsync(v => v.Id == id);

    private async Task<byte[]> CurrentRowVersionAsync(Guid id) =>
        await _context.PlanVersions.Where(v => v.Id == id).Select(v => v.RowVersion).SingleAsync();

    private static void AssertClonedModel(PlanVersion source, PlanVersion clone)
    {
        Assert.Equal(source.WbsNodes.Count, clone.WbsNodes.Count);
        Assert.Equal(source.Activities.Count, clone.Activities.Count);
        Assert.Equal(source.Deliverables.Count, clone.Deliverables.Count);
        Assert.Equal(source.Dependencies.Count, clone.Dependencies.Count);
        Assert.Equal(source.Assignments.Count, clone.Assignments.Count);
        Assert.Equal(source.Calendars.Count, clone.Calendars.Count);
        Assert.Equal(source.Calendars.Single().DayRules.Count, clone.Calendars.Single().DayRules.Count);
        Assert.Equal(source.Calendars.Single().Holidays.Count, clone.Calendars.Single().Holidays.Count);
        Assert.Equal(clone.Id, clone.Activities.Single().PlanVersionId);
        Assert.Equal(clone.Activities.Single().Id, clone.Dependencies.Single().ActivityId);
        Assert.Equal(clone.Activities.Single().Id, clone.Assignments.Single().ActivityId);
        Assert.Equal(clone.Calendars.Single().Id, clone.Calendars.Single().DayRules.Single().PlanningCalendarId);
        Assert.Equal(clone.WbsNodes.Single().Id, clone.Activities.Single().WbsId);
        Assert.Equal(source.WbsNodes.Single().TextFormat?.IsBold, clone.WbsNodes.Single().TextFormat?.IsBold);
        Assert.Equal(source.WbsNodes.Single().TextFormat?.TextColorToken, clone.WbsNodes.Single().TextFormat?.TextColorToken);
        Assert.Equal(source.Activities.Single().TextFormat?.IsItalic, clone.Activities.Single().TextFormat?.IsItalic);
        Assert.Equal(source.Activities.Single().TextFormat?.HighlightColorToken, clone.Activities.Single().TextFormat?.HighlightColorToken);
        Assert.Equal(source.Activities.Single().TextFormat?.FontSizeToken, clone.Activities.Single().TextFormat?.FontSizeToken);
    }

    private void SetTenant(Guid tenantId)
    {
        _accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("tenantId", tenantId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, _userA.ToString())
            }, "test"))
        });
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

internal sealed class PlanningWorkflowTestDbContext : DbContext
{
    private readonly IHttpContextAccessor _accessor;

    public PlanningWorkflowTestDbContext(DbContextOptions<PlanningWorkflowTestDbContext> options, IHttpContextAccessor accessor)
        : base(options) => _accessor = accessor;

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanVersion> PlanVersions => Set<PlanVersion>();
    public DbSet<PlanWbsNode> PlanWbsNodes => Set<PlanWbsNode>();
    public DbSet<PlanActivity> PlanActivities => Set<PlanActivity>();
    public DbSet<PlanDeliverable> PlanDeliverables => Set<PlanDeliverable>();
    public DbSet<PlanDependency> PlanDependencies => Set<PlanDependency>();
    public DbSet<PlanAssignment> PlanAssignments => Set<PlanAssignment>();
    public DbSet<CalendarTemplate> CalendarTemplates => Set<CalendarTemplate>();
    public DbSet<PlanCalendar> PlanCalendars => Set<PlanCalendar>();
    public DbSet<CalendarDayRule> CalendarDayRules => Set<CalendarDayRule>();
    public DbSet<CalendarHoliday> CalendarHolidays => Set<CalendarHoliday>();

    private Guid TenantId => Guid.TryParse(_accessor.HttpContext?.User.FindFirst("tenantId")?.Value, out var tenantId)
        ? tenantId
        : Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureBase(modelBuilder.Entity<Plan>());
        ConfigureBase(modelBuilder.Entity<PlanVersion>());
        ConfigureBase(modelBuilder.Entity<PlanWbsNode>());
        ConfigureBase(modelBuilder.Entity<PlanActivity>());
        ConfigureBase(modelBuilder.Entity<PlanDeliverable>());
        ConfigureBase(modelBuilder.Entity<PlanDependency>());
        ConfigureBase(modelBuilder.Entity<PlanAssignment>());
        ConfigureBase(modelBuilder.Entity<PlanningCalendar>());
        ConfigureBase(modelBuilder.Entity<CalendarDayRule>());
        ConfigureBase(modelBuilder.Entity<CalendarHoliday>());

        modelBuilder.Entity<PlanWbsNode>().OwnsOne(e => e.TextFormat, format =>
        {
            format.Property(f => f.IsSpecified).HasColumnName("TextFormatSpecified").IsRequired();
            format.Property(f => f.IsBold).HasColumnName("TextFormatBold").IsRequired();
            format.Property(f => f.IsItalic).HasColumnName("TextFormatItalic").IsRequired();
            format.Property(f => f.IsUnderline).HasColumnName("TextFormatUnderline").IsRequired();
            format.Property(f => f.TextColorToken).HasColumnName("TextFormatTextColor");
            format.Property(f => f.HighlightColorToken).HasColumnName("TextFormatHighlightColor");
            format.Property(f => f.FontSizeToken).HasColumnName("TextFormatFontSize");
        });
        modelBuilder.Entity<PlanActivity>().OwnsOne(e => e.TextFormat, format =>
        {
            format.Property(f => f.IsSpecified).HasColumnName("TextFormatSpecified").IsRequired();
            format.Property(f => f.IsBold).HasColumnName("TextFormatBold").IsRequired();
            format.Property(f => f.IsItalic).HasColumnName("TextFormatItalic").IsRequired();
            format.Property(f => f.IsUnderline).HasColumnName("TextFormatUnderline").IsRequired();
            format.Property(f => f.TextColorToken).HasColumnName("TextFormatTextColor");
            format.Property(f => f.HighlightColorToken).HasColumnName("TextFormatHighlightColor");
            format.Property(f => f.FontSizeToken).HasColumnName("TextFormatFontSize");
        });

        modelBuilder.Entity<Plan>().HasMany(p => p.Versions).WithOne(v => v.Plan).HasForeignKey(v => v.PlanId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PlanVersion>(e =>
        {
            e.HasMany(v => v.WbsNodes).WithOne(n => n.PlanVersion).HasForeignKey(n => n.PlanVersionId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(v => v.Activities).WithOne(a => a.PlanVersion).HasForeignKey(a => a.PlanVersionId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(v => v.Deliverables).WithOne(d => d.PlanVersion).HasForeignKey(d => d.PlanVersionId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(v => v.Dependencies).WithOne(d => d.PlanVersion).HasForeignKey(d => d.PlanVersionId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(v => v.Assignments).WithOne(a => a.PlanVersion).HasForeignKey(a => a.PlanVersionId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(v => v.Calendars).WithOne(c => c.PlanVersion).HasForeignKey(c => c.PlanVersionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(v => new { v.TenantId, v.PlanId, v.VersionNumber, v.Type }).IsUnique();
            e.HasIndex(v => new { v.TenantId, v.PlanId, v.Type }).IsUnique().HasFilter("\"Type\" = 0 AND \"Status\" = 0 AND \"IsDeleted\" = 0");
            e.HasIndex(v => new { v.TenantId, v.PlanId, v.Status }).IsUnique(false).HasFilter("\"Type\" = 1 AND \"Status\" = 3 AND \"IsDeleted\" = 0");
        });
        modelBuilder.Entity<PlanWbsNode>().HasOne(n => n.Parent).WithMany().HasForeignKey(n => n.ParentWbsId).OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<PlanningCalendar>().ToTable("PlanningCalendars").HasDiscriminator<string>("CalendarKind")
            .HasValue<CalendarTemplate>("Template").HasValue<PlanCalendar>("Plan");
        modelBuilder.Entity<CalendarTemplate>().HasMany(t => t.ProjectOverrides).WithOne(c => c.Template).HasForeignKey(c => c.TemplateId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PlanningCalendar>().HasMany(c => c.DayRules).WithOne(r => r.PlanningCalendar).HasForeignKey(r => r.PlanningCalendarId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PlanningCalendar>().HasMany(c => c.Holidays).WithOne(h => h.PlanningCalendar).HasForeignKey(h => h.PlanningCalendarId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PlanCalendar>().Property(c => c.ProjectId).IsRequired();
        modelBuilder.Entity<PlanCalendar>().Property(c => c.PlanVersionId).IsRequired();
    }

    private void ConfigureBase<TEntity>(EntityTypeBuilder<TEntity> entity) where TEntity : BaseEntity
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.RowVersion)
            .HasColumnType("BLOB")
            .IsRequired()
            .HasDefaultValueSql("randomblob(8)")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
        entity.HasQueryFilter(e => !e.IsDeleted && e.TenantId == TenantId);
    }

}
