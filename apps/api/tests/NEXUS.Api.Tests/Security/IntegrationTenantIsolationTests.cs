using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Integrations.Application;
using NEXUS.Modules.Integrations.Domain;
using NEXUS.Modules.Integrations.Infrastructure;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

/// <summary>
/// Regression coverage for integration connection tenant isolation:
/// IntegrationService must scope every read to the caller's tenant so no
/// tenant can observe another tenant's integration connections or credentials.
/// </summary>
[Collection("Static tenant resolution")]
public sealed class IntegrationTenantIsolationTests : IDisposable
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private readonly Guid TenantAConnectionId = Guid.NewGuid();
    private readonly Guid TenantBConnectionId = Guid.NewGuid();

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HttpContextAccessor _httpContextAccessor = new();
    private readonly AppDbContext _db;
    private readonly IIntegrationSecretProtector _secretProtector = new IntegrationSecretProtector(
        DataProtectionProvider.Create("integration-tenant-isolation-tests"));
    private readonly IntegrationService _service;

    public IntegrationTenantIsolationTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new SqliteTestAppDbContext(options);
        _db.Database.EnsureCreated();
        AppDbContext.ConfigureTenantResolution(_httpContextAccessor);
        SetTenantContext(TenantA);

        Seed();

        _service = new IntegrationService(_db, _secretProtector);
    }

    [Fact]
    public async Task GetPagedAsync_returns_only_caller_tenant_connections()
    {
        SetTenantContext(TenantA);
        var pageA = await _service.GetPagedAsync(TenantA, 1, 20);
        Assert.Single(pageA.Items);
        Assert.Equal("TenantA-Connection", pageA.Items[0].Name);

        SetTenantContext(TenantB);
        var pageB = await _service.GetPagedAsync(TenantB, 1, 20);
        Assert.Single(pageB.Items);
        Assert.Equal("TenantB-Connection", pageB.Items[0].Name);
    }

    [Fact]
    public async Task GetByIdAsync_returns_not_found_for_other_tenant_connection()
    {
        SetTenantContext(TenantA);
        var result = await _service.GetByIdAsync(TenantA, TenantBConnectionId);
        Assert.False(result.IsSuccess);
        Assert.Equal(Error.NotFound().Code, result.Error?.Code);
    }

    [Fact]
    public async Task GetByIdAsync_returns_own_tenant_connection_without_secret_values()
    {
        SetTenantContext(TenantA);
        var result = await _service.GetByIdAsync(TenantA, TenantAConnectionId);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("TenantA-Connection", result.Value.Name);
        Assert.True(result.Value.HasApiKey);
        Assert.True(result.Value.HasApiSecret);

        var serialized = JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("secret-key-a", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-secret-a", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("NEXUSINTDP1:", serialized, StringComparison.Ordinal);
        Assert.Null(result.Value.GetType().GetProperty("ApiKey"));
        Assert.Null(result.Value.GetType().GetProperty("ApiSecret"));
    }

    private void Seed()
    {
        _db.IntegrationConnections.AddRange(
            new IntegrationConnection
            {
                Id = TenantAConnectionId,
                TenantId = TenantA,
                Name = "TenantA-Connection",
                Provider = "gitlab",
                ApiKey = _secretProtector.Protect("secret-key-a"),
                ApiSecret = _secretProtector.Protect("secret-secret-a"),
                IsActive = true
            },
            new IntegrationConnection
            {
                Id = TenantBConnectionId,
                TenantId = TenantB,
                Name = "TenantB-Connection",
                Provider = "gitlab",
                ApiKey = _secretProtector.Protect("secret-key-b"),
                ApiSecret = _secretProtector.Protect("secret-secret-b"),
                IsActive = true
            });
        _db.SaveChanges();
    }

    private void SetTenantContext(Guid tenantId)
    {
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim("tenantId", tenantId.ToString()) },
                authenticationType: "test"))
        };
    }

    public void Dispose()
    {
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        _db.Dispose();
        _connection.Dispose();
    }
}
