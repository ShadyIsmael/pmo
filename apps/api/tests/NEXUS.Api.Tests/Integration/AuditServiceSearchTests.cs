using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Persistence;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class AuditServiceSearchTests
{
    [Theory]
    [InlineData("Alex Admin")]
    [InlineData("Project.Updated")]
    [InlineData("Project")]
    [InlineData("Status changed")]
    public async Task GetPagedAsync_searches_user_action_entity_and_change_text(string search)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        var tenantId = Guid.NewGuid();
        var tenantEntryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await SeedAuditEntriesAsync(context,
            new AuditEntry
            {
                Id = tenantEntryId,
                TenantId = tenantId,
                EntityType = "Project",
                EntityId = Guid.NewGuid(),
                Action = "Project.Updated",
                Changes = "Status changed",
                UserName = "Alex Admin",
                Timestamp = DateTime.UtcNow,
            },
            new AuditEntry
            {
                TenantId = Guid.NewGuid(),
                EntityType = "Project",
                EntityId = Guid.NewGuid(),
                Action = "Project.Updated",
                Changes = "Status changed",
                UserName = "Alex Admin",
                Timestamp = DateTime.UtcNow.AddMinutes(-1),
            });
        Assert.Equal(2, await context.AuditEntries.IgnoreQueryFilters().CountAsync());
        ConfigureTenant(tenantId);
        Assert.Equal(1, await context.AuditEntries.CountAsync());

        var result = await new AuditService(context).GetPagedAsync(1, 25, search: search);

        Assert.Single(result.Items);
        Assert.Equal("Alex Admin", result.Items[0].UserName);
        Assert.Equal(tenantEntryId, result.Items[0].Id);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_applies_search_before_count_and_pagination()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        var tenantId = Guid.NewGuid();
        var older = new AuditEntry { TenantId = tenantId, EntityType = "Project", EntityId = Guid.NewGuid(), Action = "Project.Created", UserName = "Taylor", Timestamp = DateTime.UtcNow.AddMinutes(-1) };
        var newer = new AuditEntry { TenantId = tenantId, EntityType = "Project", EntityId = Guid.NewGuid(), Action = "Project.Updated", UserName = "Alex", Timestamp = DateTime.UtcNow };
        await SeedAuditEntriesAsync(context, older, newer,
            new AuditEntry { TenantId = Guid.NewGuid(), EntityType = "Project", EntityId = Guid.NewGuid(), Action = "Project.Created", UserName = "Alex", Timestamp = DateTime.UtcNow.AddMinutes(1) });
        Assert.Equal(3, await context.AuditEntries.IgnoreQueryFilters().CountAsync());
        ConfigureTenant(tenantId);
        Assert.Equal(2, await context.AuditEntries.CountAsync());

        var result = await new AuditService(context).GetPagedAsync(2, 1, search: "Project");

        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(older.Id, result.Items[0].Id);
    }

    private static async Task SeedAuditEntriesAsync(AppDbContext context, params AuditEntry[] entries)
    {
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        await using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE AuditEntries (
                Id TEXT NOT NULL PRIMARY KEY, EntityType TEXT NOT NULL, EntityId TEXT NOT NULL,
                Action TEXT NOT NULL, Changes TEXT NULL, OriginalValues TEXT NULL, NewValues TEXT NULL,
                UserId TEXT NULL, UserName TEXT NULL, IpAddress TEXT NULL, UserAgent TEXT NULL,
                Timestamp TEXT NOT NULL, TenantId TEXT NOT NULL, CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NULL,
                IsDeleted INTEGER NOT NULL, RowVersion BLOB NOT NULL DEFAULT (randomblob(8)))
            """;
        await create.ExecuteNonQueryAsync();

        context.AuditEntries.AddRange(entries);
        await context.SaveChangesAsync();
    }

    private static void ConfigureTenant(Guid tenantId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenantId", tenantId.ToString())], "audit-search-test")),
        };
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor { HttpContext = httpContext });
    }
}
