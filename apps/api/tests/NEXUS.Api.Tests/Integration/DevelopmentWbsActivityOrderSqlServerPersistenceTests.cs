using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Persistence;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Opt-in SQL Server regression for mixed Task/Milestone WBS ordering. All test
/// rows are written inside one transaction and rolled back before returning.
/// </summary>
public sealed class DevelopmentWbsActivityOrderSqlServerPersistenceTests
{
    private const string OptInVariable = "NEXUS_EF_WBS_SQLSERVER_TESTS";
    private const string ConnectionVariable = "NEXUS_EF_DEVELOPMENT_CONNECTION_STRING";
    private const string BackupVariable = "NEXUS_EF_WBS_TEST_BACKUP_PATH";

    [Fact]
    public async Task DevelopmentSqlServer_TaskMilestoneMoveInsertReloadAndRollback()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal))
            throw Xunit.Sdk.SkipException.ForSkip($"Set {OptInVariable}=1 to run this transaction-scoped Development SQL Server persistence test.");

        Assert.Equal("Development", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
        var connectionString = RequiredEnvironmentValue(ConnectionVariable);
        var backupPath = Environment.GetEnvironmentVariable(BackupVariable);
        if (!string.IsNullOrWhiteSpace(backupPath))
        {
            Assert.True(Path.IsPathFullyQualified(backupPath), $"{BackupVariable} must be an absolute SQL Server-accessible path when supplied.");
        }

        var httpContextAccessor = new HttpContextAccessor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.OpenConnectionAsync();
        var connection = context.Database.GetDbConnection();
        var target = await ReadTargetIdentityAsync(connection);
        Assert.Equal("Shady", target.Server);
        Assert.Equal("NexusPmo", target.Database);
        Assert.True(target.DatabaseId > 0);
        if (!string.IsNullOrWhiteSpace(backupPath))
            await VerifyBackupAsync(connection, backupPath, target.Server, target.Database);

        var project = await context.Set<Project>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(value => !value.IsDeleted)
            .OrderBy(value => value.Id)
            .Select(value => new { value.Id, value.TenantId })
            .FirstOrDefaultAsync();
        Assert.NotNull(project);

        var actorId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenantId", project!.TenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, actorId.ToString())
        ], "wbs-activity-order-sqlserver-test"));
        httpContextAccessor.HttpContext = httpContext;
        AppDbContext.ConfigureTenantResolution(httpContextAccessor);

        await using var transaction = await context.Database.BeginTransactionAsync();
        var testIds = new List<Guid>();
        Guid testPlanId = Guid.Empty;
        try
        {
            var token = Guid.NewGuid().ToString("N");
            var plan = new Plan
            {
                TenantId = project.TenantId,
                ProjectId = project.Id,
                PlanType = PlanType.Execution,
                Name = $"WBS order SQL persistence {token}",
                Status = PlanStatus.Draft,
                CreatedBy = actorId.ToString()
            };
            var workingVersion = new PlanVersion
            {
                TenantId = project.TenantId,
                Plan = plan,
                PlanId = plan.Id,
                VersionNumber = 1,
                Type = PlanVersionType.Working,
                Status = PlanVersionStatus.Active,
                Name = "Working v1",
                CreatedBy = actorId.ToString()
            };
            context.Plans.Add(plan);
            context.PlanVersions.Add(workingVersion);
            await context.SaveChangesAsync();
            testPlanId = plan.Id;

            var audit = new Mock<IAuditService>().Object;
            var wbsService = new PlanWbsService(context, audit, httpContextAccessor);
            var activityService = new PlanActivityService(context, audit, httpContextAccessor);

            var rootTask = await CreateRowAsync(wbsService, activityService, testPlanId, actorId, token, "root-task", PlanActivityType.Task, null, 0);
            var rootMilestone = await CreateRowAsync(wbsService, activityService, testPlanId, actorId, token, "root-milestone", PlanActivityType.Milestone, null, 1);
            var childTask = await CreateRowAsync(wbsService, activityService, testPlanId, actorId, token, "child-task", PlanActivityType.Task, rootTask.Node.Id, 0);
            var childMilestone = await CreateRowAsync(wbsService, activityService, testPlanId, actorId, token, "child-milestone", PlanActivityType.Milestone, rootTask.Node.Id, 1);
            testIds.AddRange([rootTask.Node.Id, rootTask.Activity.Id, rootMilestone.Node.Id, rootMilestone.Activity.Id,
                childTask.Node.Id, childTask.Activity.Id, childMilestone.Node.Id, childMilestone.Activity.Id]);

            var standaloneRootTask = await activityService.CreateAsync(testPlanId, new CreatePlanActivityDto
            {
                Code = Code(token, "standalone-root-task"),
                Name = "standalone-root-task",
                Type = PlanActivityType.Task,
                Duration = 1,
                SchedulingMode = PlanSchedulingMode.Manual,
                Progress = 0,
                SortOrder = 2
            }, actorId.ToString());
            Assert.True(standaloneRootTask.IsSuccess, standaloneRootTask.Error?.Message);
            testIds.Add(standaloneRootTask.Value!.Id);

            // Legacy plans can have only the reverse WBS -> activity link.
            // Preserve that representation while allowing its row to move.
            var reverseOnlyNode = await wbsService.CreateAsync(testPlanId, new CreatePlanWbsNodeDto
            {
                Code = Code(token, "reverse-only-node"),
                Name = "reverse-only-node",
                SortOrder = 3
            }, actorId.ToString());
            Assert.True(reverseOnlyNode.IsSuccess, reverseOnlyNode.Error?.Message);
            var reverseOnlyActivity = await activityService.CreateAsync(testPlanId, new CreatePlanActivityDto
            {
                Code = Code(token, "reverse-only-activity"),
                Name = "reverse-only-activity",
                Type = PlanActivityType.Milestone,
                Duration = 0,
                SchedulingMode = PlanSchedulingMode.Manual,
                Progress = 0,
                SortOrder = 3
            }, actorId.ToString());
            Assert.True(reverseOnlyActivity.IsSuccess, reverseOnlyActivity.Error?.Message);
            var reverseOnlyEntity = await context.PlanWbsNodes.SingleAsync(node => node.Id == reverseOnlyNode.Value!.Id);
            reverseOnlyEntity.ActivityId = reverseOnlyActivity.Value!.Id;
            await context.SaveChangesAsync();
            testIds.Add(reverseOnlyNode.Value.Id);
            testIds.Add(reverseOnlyActivity.Value.Id);

            // Existing malformed links must be rejected without repairing or
            // reordering them implicitly. Both rows remain inside this rollback.
            var deletedNode = await wbsService.CreateAsync(testPlanId, new CreatePlanWbsNodeDto
            {
                Code = Code(token, "deleted-node"),
                Name = "deleted-node",
                SortOrder = 4
            }, actorId.ToString());
            Assert.True(deletedNode.IsSuccess, deletedNode.Error?.Message);
            var deletedNodeEntity = await context.PlanWbsNodes.SingleAsync(node => node.Id == deletedNode.Value!.Id);
            deletedNodeEntity.IsDeleted = true;

            var baselineVersion = new PlanVersion
            {
                TenantId = project.TenantId,
                PlanId = plan.Id,
                VersionNumber = 2,
                Type = PlanVersionType.Baseline,
                Status = PlanVersionStatus.Approved,
                Name = "Baseline v1",
                CreatedBy = actorId.ToString()
            };
            var baselineNode = new PlanWbsNode
            {
                TenantId = project.TenantId,
                PlanVersion = baselineVersion,
                PlanVersionId = baselineVersion.Id,
                Code = Code(token, "baseline-node"),
                Name = "baseline-node",
                CreatedBy = actorId.ToString()
            };
            context.PlanVersions.Add(baselineVersion);
            context.PlanWbsNodes.Add(baselineNode);
            var deletedLinkActivity = new PlanActivity
            {
                TenantId = project.TenantId,
                PlanVersionId = workingVersion.Id,
                Code = Code(token, "deleted-link-activity"),
                Name = "deleted-link-activity",
                Type = PlanActivityType.Task,
                Duration = 1,
                SchedulingMode = PlanSchedulingMode.Manual,
                WbsId = deletedNode.Value.Id,
                CreatedBy = actorId.ToString()
            };
            var foreignVersionLinkActivity = new PlanActivity
            {
                TenantId = project.TenantId,
                PlanVersionId = workingVersion.Id,
                Code = Code(token, "foreign-version-link-activity"),
                Name = "foreign-version-link-activity",
                Type = PlanActivityType.Task,
                Duration = 1,
                SchedulingMode = PlanSchedulingMode.Manual,
                WbsId = baselineNode.Id,
                CreatedBy = actorId.ToString()
            };
            context.PlanActivities.AddRange(deletedLinkActivity, foreignVersionLinkActivity);
            await context.SaveChangesAsync();
            testIds.Add(deletedNode.Value.Id);
            testIds.Add(baselineNode.Id);
            testIds.Add(deletedLinkActivity.Id);
            testIds.Add(foreignVersionLinkActivity.Id);

            var movedRootMilestone = await MoveAsync(context, activityService, testPlanId, actorId, rootMilestone, rootTask, PlanActivityMovePosition.Before);
            Assert.True(movedRootMilestone.IsSuccess, movedRootMilestone.Error?.Message);
            var movedStandalone = await MoveUnlinkedRootActivityAsync(context, activityService, testPlanId, actorId,
                standaloneRootTask.Value!, rootMilestone.Activity, PlanActivityMovePosition.Before);
            Assert.True(movedStandalone.IsSuccess, movedStandalone.Error?.Message);
            var movedChildTask = await MoveAsync(context, activityService, testPlanId, actorId, childTask, childMilestone, PlanActivityMovePosition.After);
            Assert.True(movedChildTask.IsSuccess, movedChildTask.Error?.Message);

            var insertedTask = await CreateRowAsync(wbsService, activityService, testPlanId, actorId, token, "inserted-child-task", PlanActivityType.Task, rootTask.Node.Id, 2);
            testIds.Add(insertedTask.Node.Id);
            testIds.Add(insertedTask.Activity.Id);
            var movedInsertedTask = await MoveAsync(context, activityService, testPlanId, actorId, insertedTask, childMilestone, PlanActivityMovePosition.After);
            Assert.True(movedInsertedTask.IsSuccess, movedInsertedTask.Error?.Message);

            var movedReverseOnly = await MoveUnlinkedRootActivityAsync(context, activityService, testPlanId, actorId,
                reverseOnlyActivity.Value!, rootMilestone.Activity, PlanActivityMovePosition.Before);
            Assert.True(movedReverseOnly.IsSuccess, movedReverseOnly.Error?.Message);

            var unifiedOrderBeforeInvalidMoves = await ReadUnifiedRootOrderAsync(context, workingVersion.Id);
            var deletedLinkDto = (await activityService.GetByPlanAsync(testPlanId)).Value!.Single(item => item.Id == deletedLinkActivity.Id);
            var deletedLinkMove = await MoveUnlinkedRootActivityAsync(context, activityService, testPlanId, actorId,
                deletedLinkDto, rootTask.Activity, PlanActivityMovePosition.Before);
            Assert.True(deletedLinkMove.IsFailure);
            var foreignVersionLinkDto = (await activityService.GetByPlanAsync(testPlanId)).Value!.Single(item => item.Id == foreignVersionLinkActivity.Id);
            var foreignVersionMove = await MoveUnlinkedRootActivityAsync(context, activityService, testPlanId, actorId,
                foreignVersionLinkDto, rootTask.Activity, PlanActivityMovePosition.Before);
            Assert.True(foreignVersionMove.IsFailure);
            Assert.Equal(unifiedOrderBeforeInvalidMoves, await ReadUnifiedRootOrderAsync(context, workingVersion.Id));

            // A parent cannot be moved beneath one of its descendant WBS nodes.
            var sourceParent = await context.PlanWbsNodes.SingleAsync(node => node.Id == rootTask.Node.Id);
            var sourceParentActivity = await context.PlanActivities.SingleAsync(activity => activity.Id == rootTask.Activity.Id);
            var cyclicMove = await activityService.MoveAsync(testPlanId, rootTask.Activity.Id, new MovePlanActivityDto
            {
                TargetWbsId = childMilestone.Node.Id,
                Position = PlanActivityMovePosition.Child,
                RowVersion = sourceParentActivity.RowVersion,
                WbsRowVersion = sourceParent.RowVersion
            }, actorId.ToString());
            Assert.True(cyclicMove.IsFailure);

            context.ChangeTracker.Clear();
            var reloadedTree = await wbsService.GetTreeAsync(testPlanId);
            Assert.True(reloadedTree.IsSuccess, reloadedTree.Error?.Message);
            var nodesByCode = Flatten(reloadedTree.Value!).ToDictionary(node => node.Code, StringComparer.Ordinal);
            var reloadedRootTask = nodesByCode[Code(token, "root-task")];
            var reloadedRootMilestone = nodesByCode[Code(token, "root-milestone")];
            var reloadedChildTask = nodesByCode[Code(token, "child-task")];
            var reloadedChildMilestone = nodesByCode[Code(token, "child-milestone")];
            var reloadedInsertedTask = nodesByCode[Code(token, "inserted-child-task")];

            Assert.Equal(new[] { standaloneRootTask.Value!.Id, reverseOnlyNode.Value!.Id, reloadedRootMilestone.Id, reloadedRootTask.Id },
                await ReadUnifiedRootOrderAsync(context, workingVersion.Id));
            Assert.Equal(new[] { 0, 1, 2, 3 },
                (await ReadUnifiedRootRowsAsync(context, workingVersion.Id)).Select(row => row.SortOrder).ToArray());
            Assert.Equal(reloadedRootTask.Id, reloadedChildMilestone.ParentWbsId);
            Assert.Equal(reloadedRootTask.Id, reloadedInsertedTask.ParentWbsId);
            Assert.Equal(reloadedRootTask.Id, reloadedChildTask.ParentWbsId);
            Assert.Equal(new[] { reloadedChildMilestone.Id, reloadedInsertedTask.Id, reloadedChildTask.Id },
                reloadedRootTask.Children.OrderBy(node => node.SortOrder).Select(node => node.Id).ToArray());
            Assert.Equal(new[] { 0, 1, 2 }, reloadedRootTask.Children.OrderBy(node => node.SortOrder).Select(node => node.SortOrder).ToArray());

            var reloadedActivities = await activityService.GetByPlanAsync(testPlanId);
            Assert.True(reloadedActivities.IsSuccess, reloadedActivities.Error?.Message);
            var activitiesByCode = reloadedActivities.Value!.ToDictionary(activity => activity.Code, StringComparer.Ordinal);
            Assert.Equal(reloadedRootTask.Id, activitiesByCode[Code(token, "root-task")].WbsId);
            Assert.Equal(reloadedRootMilestone.Id, activitiesByCode[Code(token, "root-milestone")].WbsId);
            Assert.Equal(reloadedChildTask.Id, activitiesByCode[Code(token, "child-task")].WbsId);
            Assert.Equal(reloadedChildMilestone.Id, activitiesByCode[Code(token, "child-milestone")].WbsId);
            Assert.Equal(reloadedInsertedTask.Id, activitiesByCode[Code(token, "inserted-child-task")].WbsId);
            Assert.Null(activitiesByCode[Code(token, "standalone-root-task")].WbsId);
            Assert.Null(activitiesByCode[Code(token, "reverse-only-activity")].WbsId);
            Assert.Equal(reverseOnlyActivity.Value!.Id, nodesByCode[Code(token, "reverse-only-node")].ActivityId);
            Assert.Equal(deletedNode.Value.Id, activitiesByCode[Code(token, "deleted-link-activity")].WbsId);
            Assert.Equal(baselineNode.Id, activitiesByCode[Code(token, "foreign-version-link-activity")].WbsId);
            Assert.Equal(9, activitiesByCode.Count);
        }
        finally
        {
            await transaction.RollbackAsync();
            context.ChangeTracker.Clear();
        }

        Assert.False(await context.PlanActivities.IgnoreQueryFilters().AnyAsync(activity => testIds.Contains(activity.Id)));
        Assert.False(await context.PlanWbsNodes.IgnoreQueryFilters().AnyAsync(node => testIds.Contains(node.Id)));
        if (testPlanId != Guid.Empty)
            Assert.False(await context.Plans.IgnoreQueryFilters().AnyAsync(plan => plan.Id == testPlanId));
    }

    private static async Task<(PlanWbsNodeDto Node, PlanActivityDto Activity)> CreateRowAsync(
        PlanWbsService wbsService,
        PlanActivityService activityService,
        Guid planId,
        Guid actorId,
        string token,
        string suffix,
        PlanActivityType type,
        Guid? parentWbsId,
        int sortOrder)
    {
        var code = Code(token, suffix);
        var node = await wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto
        {
            Code = code,
            Name = suffix,
            ParentWbsId = parentWbsId,
            SortOrder = sortOrder
        }, actorId.ToString());
        Assert.True(node.IsSuccess, node.Error?.Message);

        var activity = await activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = code,
            Name = suffix,
            Type = type,
            Duration = type == PlanActivityType.Milestone ? 0 : 1,
            SchedulingMode = PlanSchedulingMode.Manual,
            Progress = 0,
            WbsId = node.Value!.Id,
            SortOrder = sortOrder
        }, actorId.ToString());
        Assert.True(activity.IsSuccess, activity.Error?.Message);
        return (node.Value!, activity.Value!);
    }

    private static async Task<NEXUS.SharedKernel.Result<PlanActivityDto>> MoveAsync(
        AppDbContext context,
        PlanActivityService activityService,
        Guid planId,
        Guid actorId,
        (PlanWbsNodeDto Node, PlanActivityDto Activity) source,
        (PlanWbsNodeDto Node, PlanActivityDto Activity) target,
        PlanActivityMovePosition position)
    {
        var sourceNode = await context.PlanWbsNodes.SingleAsync(node => node.Id == source.Node.Id);
        var targetNode = await context.PlanWbsNodes.SingleAsync(node => node.Id == target.Node.Id);
        return await activityService.MoveAsync(planId, source.Activity.Id, new MovePlanActivityDto
        {
            TargetActivityId = target.Activity.Id,
            Position = position,
            RowVersion = source.Activity.RowVersion,
            WbsRowVersion = sourceNode.RowVersion,
            TargetWbsRowVersion = targetNode.RowVersion
        }, actorId.ToString());
    }

    private static async Task<NEXUS.SharedKernel.Result<PlanActivityDto>> MoveUnlinkedRootActivityAsync(
        AppDbContext context,
        PlanActivityService activityService,
        Guid planId,
        Guid actorId,
        PlanActivityDto source,
        PlanActivityDto target,
        PlanActivityMovePosition position)
    {
        var sourceActivity = await context.PlanActivities.SingleAsync(activity => activity.Id == source.Id);
        var sourceNode = await context.PlanWbsNodes.FirstOrDefaultAsync(node =>
            node.PlanVersionId == source.PlanVersionId && !node.IsDeleted &&
            (node.Id == source.WbsId || node.ActivityId == source.Id));
        var targetNode = await context.PlanWbsNodes.SingleAsync(node => node.Id == target.WbsId);
        return await activityService.MoveAsync(planId, source.Id, new MovePlanActivityDto
        {
            TargetActivityId = target.Id,
            Position = position,
            RowVersion = sourceActivity.RowVersion,
            WbsRowVersion = sourceNode?.RowVersion ?? [],
            TargetWbsRowVersion = targetNode.RowVersion
        }, actorId.ToString());
    }

    private static async Task<List<Guid>> ReadUnifiedRootOrderAsync(AppDbContext context, Guid planVersionId) =>
        (await ReadUnifiedRootRowsAsync(context, planVersionId)).Select(row => row.Id).ToList();

    private static async Task<List<UnifiedRootRow>> ReadUnifiedRootRowsAsync(AppDbContext context, Guid planVersionId)
    {
        var nodes = await context.PlanWbsNodes
            .Where(node => node.PlanVersionId == planVersionId && !node.IsDeleted && node.ParentWbsId == null)
            .Select(node => new UnifiedRootRow(node.Id, node.SortOrder, node.Code))
            .ToListAsync();
        var activities = await context.PlanActivities
            .Where(activity => activity.PlanVersionId == planVersionId && !activity.IsDeleted && activity.WbsId == null &&
                               !context.PlanWbsNodes.Any(node => node.PlanVersionId == planVersionId && !node.IsDeleted && node.ActivityId == activity.Id))
            .Select(activity => new UnifiedRootRow(activity.Id, activity.SortOrder, activity.Code))
            .ToListAsync();
        return nodes.Concat(activities)
            .OrderBy(row => row.SortOrder)
            .ThenBy(row => row.Code, StringComparer.Ordinal)
            .ThenBy(row => row.Id)
            .ToList();
    }

    private sealed record UnifiedRootRow(Guid Id, int SortOrder, string Code);

    private static string Code(string token, string suffix) => $"UT{token}{suffix}";

    private static IEnumerable<PlanWbsNodeDto> Flatten(IEnumerable<PlanWbsNodeDto> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
                yield return child;
        }
    }

    private static async Task<(string Server, string Database, int DatabaseId)> ReadTargetIdentityAsync(DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)), DB_NAME(), DB_ID();";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "SQL Server did not return the connected Development identity.");
        return (reader.GetString(0), reader.GetString(1), Convert.ToInt32(reader.GetValue(2)));
    }

    private static async Task VerifyBackupAsync(DbConnection connection, string backupPath, string expectedServer, string expectedDatabase)
    {
        await using (var headerCommand = connection.CreateCommand())
        {
            headerCommand.CommandText = "RESTORE HEADERONLY FROM DISK = @backupPath;";
            var parameter = headerCommand.CreateParameter();
            parameter.ParameterName = "@backupPath";
            parameter.Value = backupPath;
            headerCommand.Parameters.Add(parameter);

            await using var reader = await headerCommand.ExecuteReaderAsync();
            var backupSets = 0;
            while (await reader.ReadAsync())
            {
                backupSets++;
                Assert.Equal(1, Convert.ToInt32(reader["BackupType"]));
                Assert.Equal(expectedServer, Convert.ToString(reader["ServerName"]), ignoreCase: true);
                Assert.Equal(expectedDatabase, Convert.ToString(reader["DatabaseName"]), ignoreCase: true);
            }
            Assert.Equal(1, backupSets);
        }

        await using var verifyCommand = connection.CreateCommand();
        verifyCommand.CommandText = "RESTORE VERIFYONLY FROM DISK = @backupPath WITH CHECKSUM;";
        var verifyParameter = verifyCommand.CreateParameter();
        verifyParameter.ParameterName = "@backupPath";
        verifyParameter.Value = backupPath;
        verifyCommand.Parameters.Add(verifyParameter);
        await verifyCommand.ExecuteNonQueryAsync();
    }

    private static string RequiredEnvironmentValue(string variableName) =>
        Environment.GetEnvironmentVariable(variableName) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set {variableName} securely before running the opt-in SQL Server persistence test.");
}
