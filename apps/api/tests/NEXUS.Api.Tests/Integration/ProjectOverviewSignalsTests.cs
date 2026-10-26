using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.WorkItems.Application;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class ProjectOverviewSignalsTests
{
    [Fact]
    public async Task Baseline_assignment_uses_weekly_hours_and_dates_when_total_hours_are_not_stored()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var baselineId = Guid.NewGuid();
        var workingId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var workingActivityId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var sourceActivityId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.Date.AddDays(1);
        var endDate = startDate.AddDays(11);

        await using var context = new PlanEvmLookupTestDbContext(
            new DbContextOptionsBuilder<PlanEvmLookupTestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options)
        {
            TenantId = tenantId
        };
        var plan = new Plan
        {
            TenantId = tenantId,
            CreatedBy = "test",
            Id = planId,
            ProjectId = projectId,
            PlanType = PlanType.Execution,
            Name = "Execution Plan",
            Status = PlanStatus.InProgress
        };
        var baseline = new PlanVersion
        {
            TenantId = tenantId,
            CreatedBy = "test",
            Id = baselineId,
            PlanId = planId,
            VersionNumber = 1,
            Type = PlanVersionType.Baseline,
            Status = PlanVersionStatus.Approved,
            Name = "Baseline v1"
        };
        var working = new PlanVersion
        {
            TenantId = tenantId,
            CreatedBy = "test",
            Id = workingId,
            PlanId = planId,
            VersionNumber = 2,
            Type = PlanVersionType.Working,
            Status = PlanVersionStatus.Active,
            Name = "Working v2"
        };
        var baselineActivity = NewPlanActivity(activityId, tenantId, baselineId, sourceActivityId, startDate, endDate);
        var workingActivity = NewPlanActivity(workingActivityId, tenantId, workingId, sourceActivityId, startDate, endDate);
        context.AddRange(plan, baseline, working, baselineActivity, workingActivity);
        context.PlanAssignments.Add(new PlanAssignment
        {
            TenantId = tenantId,
            CreatedBy = "test",
            PlanVersionId = baselineId,
            ActivityId = activityId,
            ResourceId = resourceId,
            HoursPerWeek = 8m,
            StartDate = startDate,
            EndDate = endDate
        });
        await context.SaveChangesAsync();

        var snapshot = await new ProjectEvmPlanLookup(context).GetAsync(projectId, tenantId);

        var assignment = Assert.Single(snapshot.Assignments);
        Assert.Equal(16m, assignment.PlannedHours);
        Assert.Equal(startDate, assignment.StartDate);
        Assert.Equal(endDate, assignment.EndDate);
    }

    [Fact]
    public async Task Issue_overview_uses_target_resolution_date_before_due_date()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var statusDate = DateTime.UtcNow.Date;
        await using var context = new WorkItemOverviewTestDbContext(
            new DbContextOptionsBuilder<WorkItemOverviewTestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options)
        {
            TenantId = tenantId
        };

        var targetResolutionOverdueId = Guid.NewGuid();
        var futureTargetOverridesPastDueId = Guid.NewGuid();
        context.WorkItems.AddRange(
            NewIssue(targetResolutionOverdueId, tenantId, projectId, statusDate.AddDays(-10), statusDate.AddDays(-1)),
            NewIssue(futureTargetOverridesPastDueId, tenantId, projectId, statusDate.AddDays(-3), statusDate.AddDays(2)),
            NewIssue(Guid.NewGuid(), tenantId, projectId, statusDate.AddDays(-5), statusDate.AddDays(-4), status: "Resolved"),
            NewIssue(Guid.NewGuid(), Guid.NewGuid(), projectId, statusDate.AddDays(-5), statusDate.AddDays(-4)),
            new WorkItem
            {
                TenantId = tenantId,
                CreatedBy = "test",
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Type = WorkItemType.Task.ToString(),
                Priority = WorkItemPriority.High.ToString(),
                Status = "To Do",
                Source = WorkItemSource.Internal.ToString(),
                Title = "Task with old due date",
                DueDate = statusDate.AddDays(-2)
            });
        await context.SaveChangesAsync();

        var statusLookup = new Mock<IWorkItemStatusReferenceLookup>();
        statusLookup
            .Setup(lookup => lookup.GetAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemStatusReference[]
            {
                new(Guid.NewGuid(), "To Do", "To Do", "للتنفيذ", 10, true, "blue", true),
                new(Guid.NewGuid(), "Resolved", "Resolved", "تم الحل", 20, true, "emerald", false)
            });

        var service = new WorkItemService(
            context,
            Mock.Of<IAuditService>(),
            Mock.Of<IProjectReferenceLookup>(),
            new HttpContextAccessor(),
            Mock.Of<IWorkItemCommentAccess>(),
            Mock.Of<IWorkItemCommentAuthorLookup>(),
            Mock.Of<IWorkItemAttachmentStorage>(),
            Options.Create(new WorkItemAttachmentOptions()),
            statusLookup.Object,
            Mock.Of<IManagementContextAccessor>(),
            Mock.Of<IWorkItemAssigneeLookup>(),
            Mock.Of<IResourceReferenceLookup>(),
            Mock.Of<IUserReferenceLookup>(),
            Mock.Of<IWorkItemWorkflowProvisioningService>(),
            Mock.Of<IWorkflowInstanceService>(),
            Mock.Of<IProjectWorkflowConfigurationService>());

        var overview = await service.GetProjectOverviewAsync(projectId);

        Assert.Equal(2, overview.OpenIssueCount);
        Assert.Equal(1, overview.OverdueOpenIssueCount);
        Assert.Equal("blue", overview.StatusCounts.Single(item => item.Status == "To Do").Color);
        Assert.Equal("emerald", overview.StatusCounts.Single(item => item.Status == "Resolved").Color);
        Assert.Equal(statusDate.AddDays(-1), overview.TopIssues.Single(item => item.Id == targetResolutionOverdueId).DueDate);
        Assert.Equal(statusDate.AddDays(2), overview.TopIssues.Single(item => item.Id == futureTargetOverridesPastDueId).DueDate);
    }

    private static WorkItem NewIssue(
        Guid id,
        Guid tenantId,
        Guid projectId,
        DateTime? dueDate,
        DateTime? targetResolutionDate,
        string status = "To Do") => new()
    {
        Id = id,
        TenantId = tenantId,
        CreatedBy = "test",
        ProjectId = projectId,
        Type = WorkItemType.Issue.ToString(),
        Priority = WorkItemPriority.High.ToString(),
        Status = status,
        Source = WorkItemSource.Internal.ToString(),
        Title = "Issue",
        DueDate = dueDate,
        TargetResolutionDate = targetResolutionDate
    };

    private static PlanActivity NewPlanActivity(
        Guid id,
        Guid tenantId,
        Guid versionId,
        Guid sourceActivityId,
        DateTime startDate,
        DateTime endDate) => new()
    {
        Id = id,
        TenantId = tenantId,
        CreatedBy = "test",
        PlanVersionId = versionId,
        SourceActivityId = sourceActivityId,
        Code = "A-1",
        Name = "Scheduled activity",
        Type = PlanActivityType.Task,
        StartDate = startDate,
        EndDate = endDate
    };

    private sealed class PlanEvmLookupTestDbContext(DbContextOptions<PlanEvmLookupTestDbContext> options) : DbContext(options)
    {
        public Guid TenantId { get; set; }
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<PlanVersion> PlanVersions => Set<PlanVersion>();
        public DbSet<PlanActivity> PlanActivities => Set<PlanActivity>();
        public DbSet<PlanAssignment> PlanAssignments => Set<PlanAssignment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Plan>().HasMany(plan => plan.Versions).WithOne(version => version.Plan).HasForeignKey(version => version.PlanId);
            modelBuilder.Entity<PlanVersion>().HasMany(version => version.Activities).WithOne(activity => activity.PlanVersion).HasForeignKey(activity => activity.PlanVersionId);
            modelBuilder.Entity<PlanVersion>().HasMany(version => version.Assignments).WithOne(assignment => assignment.PlanVersion).HasForeignKey(assignment => assignment.PlanVersionId);
            modelBuilder.Entity<PlanVersion>().Ignore(version => version.WbsNodes);
            modelBuilder.Entity<PlanVersion>().Ignore(version => version.Deliverables);
            modelBuilder.Entity<PlanVersion>().Ignore(version => version.Dependencies);
            modelBuilder.Entity<PlanActivity>().Ignore(activity => activity.TextFormat);
            modelBuilder.Entity<Plan>().HasQueryFilter(item => item.TenantId == TenantId && !item.IsDeleted);
            modelBuilder.Entity<PlanVersion>().HasQueryFilter(item => item.TenantId == TenantId && !item.IsDeleted);
            modelBuilder.Entity<PlanActivity>().HasQueryFilter(item => item.TenantId == TenantId && !item.IsDeleted);
            modelBuilder.Entity<PlanAssignment>().HasQueryFilter(item => item.TenantId == TenantId && !item.IsDeleted);
        }
    }

    private sealed class WorkItemOverviewTestDbContext(DbContextOptions<WorkItemOverviewTestDbContext> options) : DbContext(options)
    {
        public Guid TenantId { get; set; }
        public DbSet<WorkItem> WorkItems => Set<WorkItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WorkItem>().HasQueryFilter(item => item.TenantId == TenantId && !item.IsDeleted);
        }
    }
}
