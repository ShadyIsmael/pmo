using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Application.Mapping;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class DevelopmentWbsSqlServerPersistenceTests
{
    private const string OptInVariable = "NEXUS_EF_WBS_SQLSERVER_TESTS";
    private const string ConnectionVariable = "NEXUS_EF_DEVELOPMENT_CONNECTION_STRING";
    private const string BackupVariable = "NEXUS_EF_WBS_TEST_BACKUP_PATH";

    [Fact]
    public async Task DevelopmentSqlServer_WbsCreateEditReloadAndRollback()
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
        ], "wbs-sqlserver-persistence-test"));
        httpContextAccessor.HttpContext = httpContext;
        AppDbContext.ConfigureTenantResolution(httpContextAccessor);

        await using var transaction = await context.Database.BeginTransactionAsync();
        var createdNodeIds = new List<Guid>();
        Guid testPlanId = Guid.Empty;
        try
        {
            var token = Guid.NewGuid().ToString("N");
            var plan = new Plan
            {
                TenantId = project.TenantId,
                ProjectId = project.Id,
                PlanType = PlanType.Execution,
                Name = $"WBS SQL persistence {token}",
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

            var service = new PlanWbsService(context, new Mock<IAuditService>().Object, httpContextAccessor);
            var rootCode = $"UT{token}R";
            var firstChildCode = $"UT{token}C1";
            var secondChildCode = $"UT{token}C2";
            var root = await service.CreateAsync(testPlanId, new CreatePlanWbsNodeDto
            {
                Code = rootCode,
                Name = "SQL persistence root"
            }, actorId.ToString());
            Assert.True(root.IsSuccess, root.Error?.Message);
            Assert.NotNull(root.Value);
            Assert.Null(root.Value!.TextFormat);
            createdNodeIds.Add(root.Value.Id);

            var firstChild = await service.CreateAsync(testPlanId, new CreatePlanWbsNodeDto
            {
                Code = firstChildCode,
                Name = "First child",
                ParentWbsId = root.Value.Id
            }, actorId.ToString());
            Assert.True(firstChild.IsSuccess, firstChild.Error?.Message);
            Assert.NotNull(firstChild.Value);
            Assert.Null(firstChild.Value!.TextFormat);
            createdNodeIds.Add(firstChild.Value.Id);

            var secondChild = await service.CreateAsync(testPlanId, new CreatePlanWbsNodeDto
            {
                Code = secondChildCode,
                Name = "Second child",
                ParentWbsId = root.Value.Id
            }, actorId.ToString());
            Assert.True(secondChild.IsSuccess, secondChild.Error?.Message);
            Assert.NotNull(secondChild.Value);
            Assert.Null(secondChild.Value!.TextFormat);
            Assert.Equal(firstChild.Value.SortOrder + 1, secondChild.Value.SortOrder);
            createdNodeIds.Add(secondChild.Value.Id);

            await AssertUnspecifiedStoredFlagsAsync(connection, transaction.GetDbTransaction(), root.Value.Id);
            await AssertUnspecifiedStoredFlagsAsync(connection, transaction.GetDbTransaction(), firstChild.Value.Id);
            await AssertUnspecifiedStoredFlagsAsync(connection, transaction.GetDbTransaction(), secondChild.Value.Id);

            var formatted = await service.UpdateAsync(testPlanId, firstChild.Value.Id, new UpdatePlanWbsNodeDto
            {
                Code = firstChildCode,
                Name = "First child formatted",
                ParentWbsId = root.Value.Id,
                SortOrder = firstChild.Value.SortOrder,
                RowVersion = firstChild.Value.RowVersion,
                TextFormat = new PlanTextFormatDto { IsBold = true }
            }, actorId.ToString());
            Assert.True(formatted.IsSuccess, formatted.Error?.Message);
            Assert.True(formatted.Value!.TextFormat?.IsBold);

            var cleared = await service.UpdateAsync(testPlanId, firstChild.Value.Id, new UpdatePlanWbsNodeDto
            {
                Code = firstChildCode,
                Name = "First child",
                ParentWbsId = root.Value.Id,
                SortOrder = firstChild.Value.SortOrder,
                RowVersion = formatted.Value.RowVersion,
                TextFormat = null
            }, actorId.ToString());
            Assert.True(cleared.IsSuccess, cleared.Error?.Message);
            Assert.Null(cleared.Value!.TextFormat);
            await AssertUnspecifiedStoredFlagsAsync(connection, transaction.GetDbTransaction(), firstChild.Value.Id);

            context.ChangeTracker.Clear();
            var reloadedTree = await service.GetTreeAsync(testPlanId);
            Assert.True(reloadedTree.IsSuccess, reloadedTree.Error?.Message);
            var reloadedNodes = Flatten(reloadedTree.Value!).ToDictionary(node => node.Code, StringComparer.Ordinal);
            var reloadedRoot = reloadedNodes[rootCode];
            var reloadedFirstChild = reloadedNodes[firstChildCode];
            var reloadedSecondChild = reloadedNodes[secondChildCode];
            Assert.Null(reloadedRoot.TextFormat);
            Assert.Null(reloadedFirstChild.TextFormat);
            Assert.Null(reloadedSecondChild.TextFormat);
            Assert.Equal(reloadedRoot.Id, reloadedFirstChild.ParentWbsId);
            Assert.Equal(reloadedRoot.Id, reloadedSecondChild.ParentWbsId);
            Assert.Equal(reloadedFirstChild.SortOrder + 1, reloadedSecondChild.SortOrder);
            Assert.NotEmpty(reloadedFirstChild.RowVersion);

            var invalidParentCode = $"UT{token}BAD";
            var invalidParent = await service.CreateAsync(testPlanId, new CreatePlanWbsNodeDto
            {
                Code = invalidParentCode,
                Name = "Invalid parent attempt",
                ParentWbsId = Guid.NewGuid()
            }, actorId.ToString());
            Assert.True(invalidParent.IsFailure);
            Assert.False(await context.PlanWbsNodes.IgnoreQueryFilters().AnyAsync(node => node.Code == invalidParentCode));
        }
        finally
        {
            await transaction.RollbackAsync();
            context.ChangeTracker.Clear();
        }

        Assert.False(await context.PlanWbsNodes.IgnoreQueryFilters().AnyAsync(node => createdNodeIds.Contains(node.Id)));
        if (testPlanId != Guid.Empty)
            Assert.False(await context.Plans.IgnoreQueryFilters().AnyAsync(plan => plan.Id == testPlanId));
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

    private static async Task AssertUnspecifiedStoredFlagsAsync(DbConnection connection, DbTransaction transaction, Guid nodeId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT TextFormatSpecified, TextFormatBold, TextFormatItalic, TextFormatUnderline FROM dbo.PlanWbsNodes WHERE Id = @id;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = nodeId;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"Expected WBS node {nodeId} was not persisted.");
        for (var ordinal = 0; ordinal < 4; ordinal++)
        {
            Assert.False(reader.IsDBNull(ordinal));
            Assert.False(Convert.ToBoolean(reader.GetValue(ordinal)));
        }
    }

    private static IEnumerable<PlanWbsNodeDto> Flatten(IEnumerable<PlanWbsNodeDto> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
                yield return child;
        }
    }

    private static string RequiredEnvironmentValue(string variableName) =>
        Environment.GetEnvironmentVariable(variableName) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set {variableName} securely before running the opt-in SQL Server persistence test.");
}
