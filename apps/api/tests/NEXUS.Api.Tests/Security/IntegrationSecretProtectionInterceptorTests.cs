using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Integrations.Application;
using NEXUS.Modules.Integrations.Domain;
using NEXUS.Modules.Integrations.Infrastructure;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class IntegrationSecretProtectionInterceptorTests
{
    [Fact]
    public void Synchronous_save_persists_only_protected_connection_and_webhook_secrets()
    {
        using var connection = OpenConnection();
        var protector = CreateProtector();
        var interceptor = new IntegrationSecretProtectionInterceptor(protector);
        var options = CreateOptions(connection, interceptor);
        var createdConnection = NewConnection("sync-api-key", "sync-api-secret");
        var createdWebhook = NewWebhook("sync-webhook-secret");

        using (var db = new SecretDbContext(options))
        {
            db.Database.EnsureCreated();
            db.AddRange(createdConnection, createdWebhook);
            db.SaveChanges();
        }

        using var stored = new SecretDbContext(CreateOptions(connection));
        var storedConnection = stored.Set<IntegrationConnection>().Single();
        var storedWebhook = stored.Set<IntegrationWebhook>().Single();

        AssertSecretStoredAndReadable(protector, storedConnection.ApiKey, "sync-api-key");
        AssertSecretStoredAndReadable(protector, storedConnection.ApiSecret, "sync-api-secret");
        AssertSecretStoredAndReadable(protector, storedWebhook.Secret, "sync-webhook-secret");
    }

    [Fact]
    public async Task Asynchronous_save_persists_only_protected_connection_and_webhook_secrets()
    {
        using var connection = OpenConnection();
        var protector = CreateProtector();
        var options = CreateOptions(connection, new IntegrationSecretProtectionInterceptor(protector));

        await using (var db = new SecretDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.AddRange(NewConnection("async-api-key", "async-api-secret"), NewWebhook("async-webhook-secret"));
            await db.SaveChangesAsync();
        }

        await using var stored = new SecretDbContext(CreateOptions(connection));
        var storedConnection = await stored.Set<IntegrationConnection>().SingleAsync();
        var storedWebhook = await stored.Set<IntegrationWebhook>().SingleAsync();

        AssertSecretStoredAndReadable(protector, storedConnection.ApiKey, "async-api-key");
        AssertSecretStoredAndReadable(protector, storedConnection.ApiSecret, "async-api-secret");
        AssertSecretStoredAndReadable(protector, storedWebhook.Secret, "async-webhook-secret");
    }

    [Fact]
    public void Updating_a_legacy_row_protects_its_existing_secret_values()
    {
        using var connection = OpenConnection();
        var legacyConnection = NewConnection("legacy-api-key", "legacy-api-secret");
        using (var seed = new SecretDbContext(CreateOptions(connection)))
        {
            seed.Database.EnsureCreated();
            seed.Add(legacyConnection);
            seed.SaveChanges();
        }

        var protector = CreateProtector();
        using (var update = new SecretDbContext(CreateOptions(connection, new IntegrationSecretProtectionInterceptor(protector))))
        {
            var row = update.Set<IntegrationConnection>().Single();
            row.Name = "Updated connection";
            update.SaveChanges();
        }

        using var stored = new SecretDbContext(CreateOptions(connection));
        var updated = stored.Set<IntegrationConnection>().Single();
        AssertSecretStoredAndReadable(protector, updated.ApiKey, "legacy-api-key");
        AssertSecretStoredAndReadable(protector, updated.ApiSecret, "legacy-api-secret");
    }

    [Fact]
    public void Integration_service_requires_an_explicit_secret_protector()
    {
        using var db = new SecretDbContext(new DbContextOptionsBuilder<SecretDbContext>().Options);

        Assert.Throws<ArgumentNullException>(() => new IntegrationService(db, null!));
        Assert.Contains(
            typeof(IIntegrationSecretProtector),
            typeof(IntegrationService).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType));
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static DbContextOptions<SecretDbContext> CreateOptions(
        SqliteConnection connection,
        IntegrationSecretProtectionInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<SecretDbContext>().UseSqlite(connection);
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        return builder.Options;
    }

    private static IntegrationSecretProtector CreateProtector() =>
        new(DataProtectionProvider.Create("nexus-secret-interceptor-tests"));

    private static IntegrationConnection NewConnection(string apiKey, string apiSecret) => new()
    {
        TenantId = Guid.NewGuid(),
        Name = "Test connection",
        Provider = "test-provider",
        ApiKey = apiKey,
        ApiSecret = apiSecret
    };

    private static IntegrationWebhook NewWebhook(string secret) => new()
    {
        TenantId = Guid.NewGuid(),
        Name = "Test webhook",
        Url = "https://example.test/webhook",
        Secret = secret
    };

    private static void AssertSecretStoredAndReadable(
        IIntegrationSecretProtector protector,
        string? storedValue,
        string expectedPlaintext)
    {
        Assert.StartsWith("NEXUSINTDP1:", storedValue, StringComparison.Ordinal);
        Assert.DoesNotContain(expectedPlaintext, storedValue, StringComparison.Ordinal);
        Assert.True(protector.TryUnprotect(storedValue, out var plaintext));
        Assert.Equal(expectedPlaintext, plaintext);
    }

    private sealed class SecretDbContext(DbContextOptions<SecretDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IntegrationConnection>(entity =>
            {
                entity.HasKey(row => row.Id);
                entity.Ignore(row => row.Syncs);
            });
            modelBuilder.Entity<IntegrationWebhook>().HasKey(row => row.Id);
        }
    }
}
