using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public class PlanAssignmentServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlanAssignmentTestDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor = new();
    private readonly Mock<IAuditService> _auditService = new();
    private readonly Mock<IResourceService> _resourceService = new();
    private readonly Mock<IProjectResourcePlanningContextLookup> _planningContextLookup = new();
    private readonly PlanAssignmentService _assignmentService;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _defaultCalendarId = Guid.NewGuid();

    public PlanAssignmentServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new PlanAssignmentTestDbContext(
            new DbContextOptionsBuilder<PlanAssignmentTestDbContext>().UseSqlite(_connection).Options,
            _accessor.Object);
        _context.Database.EnsureCreated();
        _context.WorkingCalendars.Add(new WorkingCalendar
        {
            Id = _defaultCalendarId,
            TenantId = _tenantA,
            Name = "48-hour resource week",
            WorkingDaysPerWeek = 6,
            HoursPerDay = 8,
            IsDefault = true,
            CreatedBy = "test"
        });
        _context.SaveChanges();
        _planningContextLookup.Setup(x => x.HasActiveProjectMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _planningContextLookup.Setup(x => x.GetPlanningContextAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid projectId, Guid resourceId, CancellationToken _) => Task.FromResult<ProjectResourcePlanningContext?>(PlanningContext(projectId, resourceId, 100m)));
        _assignmentService = new PlanAssignmentService(_context, _resourceService.Object, _auditService.Object, _accessor.Object, _planningContextLookup.Object);
        _resourceService.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult(Result<ResourceDto>.Success(new ResourceDto { Id = id, BaseCalendarId = _defaultCalendarId })));
    }

    private ProjectResourcePlanningContext PlanningContext(Guid projectId, Guid resourceId, decimal allocationPercentage) => new(
        Guid.NewGuid(), projectId, resourceId, "FollowResourceCalendar", null, Array.Empty<Guid>(), true,
        allocationPercentage, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

    private void SetProjectAllocation(Guid resourceId, decimal allocationPercentage) =>
        _planningContextLookup.Setup(x => x.GetPlanningContextAsync(_projectId, resourceId, It.IsAny<CancellationToken>()))
            .Returns((Guid projectId, Guid requestedResourceId, CancellationToken _) => Task.FromResult<ProjectResourcePlanningContext?>(PlanningContext(projectId, requestedResourceId, allocationPercentage)));

    private static CreatePlanAssignmentDto Assignment(Guid activityId, Guid resourceId, decimal workHours = 1m) => new()
    {
        ActivityId = activityId,
        ResourceId = resourceId,
        AssignedWorkHours = workHours,
        StartDate = new DateTime(2026, 1, 4),
        EndDate = new DateTime(2026, 1, 10)
    };

    private void SetTenant(Guid tenantId, Guid userId)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("tenantId", tenantId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            }, "mock"))
        };
        _accessor.Setup(x => x.HttpContext).Returns(context);
    }

    private async Task<Guid> CreatePlanAsync()
    {
        SetTenant(_tenantA, _userA);
        var plan = new Plan
        {
            TenantId = _tenantA,
            ProjectId = _projectId,
            PlanType = PlanType.Execution,
            Name = "Plan",
            Status = PlanStatus.Draft,
            CreatedBy = _userA.ToString()
        };
        var workingVersion = new PlanVersion
        {
            TenantId = _tenantA,
            PlanId = plan.Id,
            VersionNumber = 1,
            Type = PlanVersionType.Working,
            Status = PlanVersionStatus.Active,
            Name = "Working v1",
            CreatedBy = _userA.ToString()
        };
        _context.Plans.Add(plan);
        _context.PlanVersions.Add(workingVersion);
        await _context.SaveChangesAsync();
        return plan.Id;
    }

    private async Task<Guid> CreateActivityAsync(Guid planId, string code = "A1")
    {
        var version = await _context.PlanVersions.SingleAsync(v => v.PlanId == planId);
        var activity = new PlanActivity
        {
            TenantId = _tenantA,
            PlanVersionId = version.Id,
            Code = code,
            Name = code,
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 10),
            CreatedBy = _userA.ToString()
        };
        _context.PlanActivities.Add(activity);
        await _context.SaveChangesAsync();
        return activity.Id;
    }

    private async Task<Guid> CreateBaselineAssignmentAsync(Guid planId)
    {
        var working = await _context.PlanVersions.SingleAsync(v => v.PlanId == planId);
        var baseline = new PlanVersion
        {
            TenantId = _tenantA,
            PlanId = planId,
            VersionNumber = 2,
            Type = PlanVersionType.Baseline,
            Status = PlanVersionStatus.Approved,
            Name = "Baseline",
            SourceVersionId = working.Id,
            CreatedBy = _userA.ToString()
        };
        var activity = new PlanActivity { TenantId = _tenantA, PlanVersionId = baseline.Id, Code = "B1", Name = "B1", CreatedBy = _userA.ToString() };
        var assignment = new PlanAssignment { TenantId = _tenantA, PlanVersion = baseline, ActivityId = activity.Id, ResourceId = Guid.NewGuid(), CreatedBy = _userA.ToString() };
        _context.PlanActivities.Add(activity);
        _context.PlanAssignments.Add(assignment);
        await _context.SaveChangesAsync();
        return assignment.Id;
    }

    [Fact]
    public async Task Create_ValidAssignment_PersistsPlanningAttributesAndAudits()
    {
        var planId = await CreatePlanAsync();
        var activityId = await CreateActivityAsync(planId);
        var resourceId = Guid.NewGuid();

        var result = await _assignmentService.CreateAsync(planId, new CreatePlanAssignmentDto
        {
            ActivityId = activityId, ResourceId = resourceId, AllocationPct = 50, HoursPerWeek = 20,
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31)
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(resourceId, result.Value!.ResourceId);
        Assert.Null(result.Value.AllocationPct);
        Assert.Equal(100m, result.Value.AssignedWorkHours);
        Assert.Equal(100m, result.Value.PlannedHours);
        _resourceService.Verify(r => r.GetByIdAsync(resourceId, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _auditService.Verify(a => a.LogAsync(It.Is<AuditEntry>(e => e.EntityType == "PlanAssignment" && e.Action == "Create"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Assignments_ConsumeProjectAllocationAndRejectWorkAboveRemainingCapacity()
    {
        var planId = await CreatePlanAsync();
        var resourceId = Guid.NewGuid();
        SetProjectAllocation(resourceId, 50m);
        var firstActivityId = await CreateActivityAsync(planId, "A1");
        var secondActivityId = await CreateActivityAsync(planId, "A2");
        var thirdActivityId = await CreateActivityAsync(planId, "A3");

        var first = await _assignmentService.CreateAsync(planId, Assignment(firstActivityId, resourceId, 10m), _userA.ToString());
        var second = await _assignmentService.CreateAsync(planId, Assignment(secondActivityId, resourceId, 6m), _userA.ToString());
        var capacity = await _assignmentService.GetCapacityAsync(
            _projectId, planId, resourceId, new DateTime(2026, 1, 4), new DateTime(2026, 1, 10));
        var overLimit = await _assignmentService.CreateAsync(planId, Assignment(thirdActivityId, resourceId, 9m), _userA.ToString());

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(10m, first.Value!.AssignedWorkHours);
        Assert.Equal(6m, second.Value!.AssignedWorkHours);
        Assert.True(capacity.IsSuccess);
        Assert.Equal(48m, capacity.Value!.ResourceCapacityHoursPerWeek);
        Assert.Equal(24m, capacity.Value.ProjectCapacityHours);
        Assert.Equal(16m, capacity.Value.AssignedWorkHours);
        Assert.Equal(8m, capacity.Value.RemainingAssignableHours);
        Assert.True(overLimit.IsFailure);
        Assert.Contains("remaining project capacity", overLimit.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assignments_AggregateAcrossMultiweekSchedulesAndOverlaps()
    {
        var planId = await CreatePlanAsync();
        var resourceId = Guid.NewGuid();
        SetProjectAllocation(resourceId, 50m);
        var firstActivityId = await CreateActivityAsync(planId, "A1");
        var secondActivityId = await CreateActivityAsync(planId, "A2");

        var first = await _assignmentService.CreateAsync(planId, new CreatePlanAssignmentDto
        {
            ActivityId = firstActivityId, ResourceId = resourceId, AssignedWorkHours = 40m,
            StartDate = new DateTime(2026, 1, 4), EndDate = new DateTime(2026, 1, 17)
        }, _userA.ToString());
        var beforeSecond = await _assignmentService.GetCapacityAsync(
            _projectId, planId, resourceId, new DateTime(2026, 1, 4), new DateTime(2026, 1, 17));
        var second = await _assignmentService.CreateAsync(planId, new CreatePlanAssignmentDto
        {
            ActivityId = secondActivityId, ResourceId = resourceId, AssignedWorkHours = 8m,
            StartDate = new DateTime(2026, 1, 4), EndDate = new DateTime(2026, 1, 17)
        }, _userA.ToString());
        var afterSecond = await _assignmentService.GetCapacityAsync(
            _projectId, planId, resourceId, new DateTime(2026, 1, 4), new DateTime(2026, 1, 17));

        Assert.True(first.IsSuccess);
        Assert.True(beforeSecond.IsSuccess);
        Assert.Equal(48m, beforeSecond.Value!.ProjectCapacityHours);
        Assert.Equal(40m, beforeSecond.Value.AssignedWorkHours);
        Assert.Equal(8m, beforeSecond.Value.RemainingAssignableHours);
        Assert.True(second.IsSuccess);
        Assert.True(afterSecond.IsSuccess);
        Assert.Equal(48m, afterSecond.Value!.AssignedWorkHours);
        Assert.Equal(0m, afterSecond.Value.RemainingAssignableHours);
    }

    [Fact]
    public async Task Create_UnknownResource_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var activityId = await CreateActivityAsync(planId);
        _resourceService.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ResourceDto>.Failure(Error.NotFound("Resource not found")));

        var result = await _assignmentService.CreateAsync(planId, Assignment(activityId, Guid.NewGuid()), _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
        Assert.Empty(_context.PlanAssignments.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Create_ActivityFromAnotherVersion_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var baselineAssignmentId = await CreateBaselineAssignmentAsync(planId);
        var baselineAssignment = await _context.PlanAssignments.IgnoreQueryFilters().SingleAsync(a => a.Id == baselineAssignmentId);

        var result = await _assignmentService.CreateAsync(planId, Assignment(baselineAssignment.ActivityId, Guid.NewGuid()), _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_DuplicateAssignment_ReturnsConflict()
    {
        var planId = await CreatePlanAsync();
        var activityId = await CreateActivityAsync(planId);
        var dto = Assignment(activityId, Guid.NewGuid());
        Assert.True((await _assignmentService.CreateAsync(planId, dto, _userA.ToString())).IsSuccess);

        var result = await _assignmentService.CreateAsync(planId, dto, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task GetAndUpdate_UseOnlyActiveWorkingVersion()
    {
        var planId = await CreatePlanAsync();
        var activityId = await CreateActivityAsync(planId);
        var created = (await _assignmentService.CreateAsync(planId, Assignment(activityId, Guid.NewGuid()), _userA.ToString())).Value!;
        await CreateBaselineAssignmentAsync(planId);

        var list = await _assignmentService.GetByPlanAsync(planId);
        var update = await _assignmentService.UpdateAsync(planId, created.Id, new UpdatePlanAssignmentDto
        {
            ActivityId = activityId, ResourceId = created.ResourceId, AssignedWorkHours = 1m, RowVersion = created.RowVersion
        }, _userA.ToString());

        Assert.True(list.IsSuccess);
        Assert.Single(list.Value!);
        Assert.True(update.IsSuccess);
        Assert.Null(update.Value!.AllocationPct);
        Assert.Equal(1m, update.Value.AssignedWorkHours);
        _auditService.Verify(a => a.LogAsync(It.Is<AuditEntry>(e => e.EntityType == "PlanAssignment" && e.Action == "Update"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_BaselineAssignment_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var assignmentId = await CreateBaselineAssignmentAsync(planId);

        var result = await _assignmentService.UpdateAsync(planId, assignmentId, new UpdatePlanAssignmentDto
        {
            ActivityId = Guid.NewGuid(), ResourceId = Guid.NewGuid(), RowVersion = new byte[8]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Delete_SoftDeletesAndAudits()
    {
        var planId = await CreatePlanAsync();
        var activityId = await CreateActivityAsync(planId);
        var created = (await _assignmentService.CreateAsync(planId, Assignment(activityId, Guid.NewGuid()), _userA.ToString())).Value!;

        var result = await _assignmentService.DeleteAsync(planId, created.Id, created.RowVersion, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.True((await _context.PlanAssignments.IgnoreQueryFilters().SingleAsync(a => a.Id == created.Id)).IsDeleted);
        Assert.Empty((await _assignmentService.GetByPlanAsync(planId)).Value!);
        _auditService.Verify(a => a.LogAsync(It.Is<AuditEntry>(e => e.EntityType == "PlanAssignment" && e.Action == "Delete"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAndDelete_StaleRowVersion_ReturnConflict()
    {
        var planId = await CreatePlanAsync();
        var activityId = await CreateActivityAsync(planId);
        var created = (await _assignmentService.CreateAsync(planId, Assignment(activityId, Guid.NewGuid()), _userA.ToString())).Value!;

        var update = await _assignmentService.UpdateAsync(planId, created.Id, new UpdatePlanAssignmentDto { ActivityId = activityId, ResourceId = created.ResourceId, AssignedWorkHours = 1m, RowVersion = new byte[8] }, _userA.ToString());
        var delete = await _assignmentService.DeleteAsync(planId, created.Id, new byte[8], _userA.ToString());

        Assert.True(update.IsFailure);
        Assert.True(delete.IsFailure);
        Assert.Equal("CONFLICT", update.Error!.Code, ignoreCase: true);
        Assert.Equal("CONFLICT", delete.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task TenantB_CannotReadOrModifyTenantAAssignment()
    {
        var planId = await CreatePlanAsync();
        var activityId = await CreateActivityAsync(planId);
        var created = (await _assignmentService.CreateAsync(planId, Assignment(activityId, Guid.NewGuid()), _userA.ToString())).Value!;
        SetTenant(_tenantB, Guid.NewGuid());

        var read = await _assignmentService.GetByPlanAsync(planId);
        var update = await _assignmentService.UpdateAsync(planId, created.Id, new UpdatePlanAssignmentDto { ActivityId = activityId, ResourceId = created.ResourceId, AssignedWorkHours = 1m, RowVersion = created.RowVersion }, "other-user");

        Assert.True(read.IsFailure);
        Assert.True(update.IsFailure);
        Assert.Equal("NOT_FOUND", read.Error!.Code, ignoreCase: true);
        Assert.Equal("NOT_FOUND", update.Error!.Code, ignoreCase: true);
    }

    // PLANNED HOURS hardening
    [Fact]
    public async Task Create_ExplicitPlannedHours_PreservedNotOverwritten()
    {
        var planId = await CreatePlanAsync();
        var activityId = await CreateActivityAsync(planId);
        var resourceId = Guid.NewGuid();

        var result = await _assignmentService.CreateAsync(planId, new CreatePlanAssignmentDto
        {
            ActivityId = activityId, ResourceId = resourceId, PlannedHours = 40,
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31)
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(40m, result.Value!.PlannedHours);
    }

    [Fact]
    public async Task Update_ExplicitPlannedHours_PreservedNotOverwritten()
    {
        var planId = await CreatePlanAsync();
        var activityId = await CreateActivityAsync(planId);
        var resourceId = Guid.NewGuid();
        await _assignmentService.CreateAsync(planId, new CreatePlanAssignmentDto
        {
            ActivityId = activityId, ResourceId = resourceId, PlannedHours = 40,
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31)
        }, _userA.ToString());

        var assignments = await _assignmentService.GetByPlanAsync(planId);
        var assignment = Assert.Single(assignments.Value!);

        var result = await _assignmentService.UpdateAsync(planId, assignment.Id, new UpdatePlanAssignmentDto
        {
            ActivityId = activityId,
            ResourceId = resourceId,
            HoursPerWeek = 60,
            PlannedHours = 40,
            StartDate = assignment.StartDate,
            EndDate = assignment.EndDate,
            RowVersion = assignment.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(40m, result.Value!.PlannedHours);
    }

    // ALLOCATION LINKAGE hardening
    [Fact]
    public async Task Create_OneAssignmentCreatesOneResourceAllocation()
    {
        var planId = await CreatePlanAsync();
        var activityId = await CreateActivityAsync(planId);
        var resourceId = Guid.NewGuid();

        var result = await _assignmentService.CreateAsync(planId, new CreatePlanAssignmentDto
        {
            ActivityId = activityId, ResourceId = resourceId, PlannedHours = 40,
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31)
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.ResourceAllocationId.HasValue);
        Assert.NotEqual(Guid.Empty, result.Value!.ResourceAllocationId.Value);

        var allocation = await _context.Set<ResourceAllocation>().SingleAsync(item => item.Id == result.Value.ResourceAllocationId.Value);
        Assert.Equal(_tenantA, allocation.TenantId);
        Assert.Equal(_projectId, allocation.ProjectId);
        Assert.Equal(resourceId, allocation.ResourceId);
    }

    // ROLE CONTEXT hardening
    [Fact]
    public async Task Create_InvalidProjectRole_ReturnsValidationError()
    {
        _planningContextLookup.Setup(x => x.HasActiveProjectMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var planId = await CreatePlanAsync();
        var activityId = await CreateActivityAsync(planId);
        var resourceId = Guid.NewGuid();

        var result = await _assignmentService.CreateAsync(planId, new CreatePlanAssignmentDto
        {
            ActivityId = activityId, ResourceId = resourceId, ProjectRoleId = Guid.NewGuid(),
            PlannedHours = 40, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31)
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

/// <summary>
/// Focused test context. AppDbContext discovers every module DbSet during EF
/// initialization, which makes unrelated module assemblies part of this test's
/// runtime graph. Assignment behavior only needs these four planning entities.
/// </summary>
internal sealed class PlanAssignmentTestDbContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PlanAssignmentTestDbContext(DbContextOptions<PlanAssignmentTestDbContext> options, IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanVersion> PlanVersions => Set<PlanVersion>();
    public DbSet<PlanActivity> PlanActivities => Set<PlanActivity>();
    public DbSet<PlanAssignment> PlanAssignments => Set<PlanAssignment>();
    public DbSet<WorkingCalendar> WorkingCalendars => Set<WorkingCalendar>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<ResourceCalendar> ResourceCalendars => Set<ResourceCalendar>();

    private Guid CurrentTenantId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
            return Guid.TryParse(value, out var tenantId)
                ? tenantId
                : Guid.Parse("11111111-1111-1111-1111-111111111111");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureBase(modelBuilder.Entity<Plan>());
        ConfigureBase(modelBuilder.Entity<PlanVersion>());
        ConfigureBase(modelBuilder.Entity<PlanActivity>());
        ConfigureBase(modelBuilder.Entity<PlanAssignment>());
        ConfigureBase(modelBuilder.Entity<WorkingCalendar>());
        ConfigureBase(modelBuilder.Entity<Holiday>());
        ConfigureBase(modelBuilder.Entity<ResourceCalendar>());
        ConfigureBase(modelBuilder.Entity<ResourceAllocation>());
        modelBuilder.Entity<ResourceAllocation>().Ignore(allocation => allocation.Resource);
        modelBuilder.Entity<ResourceCalendar>().Ignore(calendar => calendar.Resource);
        modelBuilder.Entity<WorkingCalendar>()
            .HasMany(calendar => calendar.Holidays)
            .WithOne(holiday => holiday.WorkingCalendar)
            .HasForeignKey(holiday => holiday.WorkingCalendarId);

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

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.Property(p => p.Name).IsRequired();
            entity.HasMany(p => p.Versions).WithOne(v => v.Plan).HasForeignKey(v => v.PlanId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(p => new { p.TenantId, p.ProjectId, p.PlanType }).IsUnique();
        });

        modelBuilder.Entity<PlanVersion>(entity =>
        {
            entity.HasMany(v => v.Activities).WithOne(a => a.PlanVersion).HasForeignKey(a => a.PlanVersionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(v => v.Assignments).WithOne(a => a.PlanVersion).HasForeignKey(a => a.PlanVersionId).OnDelete(DeleteBehavior.Cascade);
            entity.Ignore(v => v.WbsNodes);
            entity.Ignore(v => v.Deliverables);
            entity.Ignore(v => v.Dependencies);
            entity.HasIndex(v => new { v.TenantId, v.PlanId, v.VersionNumber }).IsUnique();
        });

        modelBuilder.Entity<PlanActivity>(entity =>
        {
            entity.Property(a => a.Code).IsRequired();
            entity.Property(a => a.Name).IsRequired();
            entity.HasIndex(a => new { a.TenantId, a.PlanVersionId, a.Code }).IsUnique();
        });

        modelBuilder.Entity<PlanAssignment>(entity =>
        {
            entity.Property(a => a.ActivityId).IsRequired();
            entity.Property(a => a.ResourceId).IsRequired();
            entity.HasIndex(a => new { a.TenantId, a.PlanVersionId, a.ActivityId, a.ResourceId }).IsUnique();
        });
    }

    private void ConfigureBase<TEntity>(EntityTypeBuilder<TEntity> entity)
        where TEntity : BaseEntity
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CreatedBy).IsRequired();
        entity.Property(e => e.RowVersion)
            .HasColumnType("BLOB")
            .IsRequired()
            .HasDefaultValueSql("randomblob(8)")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
        entity.HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
    }

}
