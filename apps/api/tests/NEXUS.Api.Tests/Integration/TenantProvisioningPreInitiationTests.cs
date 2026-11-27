using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class TenantProvisioningPreInitiationTests
{
    [Fact]
    public async Task CreateAsync_ProvisionsOrganizationPhaseWithinTenantTransaction()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var tenantScope = TrustedTenantExecutionContext.Enter(fixture.TenantId);
        fixture.EstablishAuthenticatedRequest();
        var audit = CreateSuccessfulAudit();
        var lifecycle = new ProjectLifecycleCatalogService(fixture.Db, fixture.Http, audit.Object);
        var provisioning = new TenantProvisioningService(fixture.Db, fixture.Http, audit.Object, lifecycle);

        var result = await provisioning.CreateAsync(new CreateTenantRequest(
            $"TEN-{fixture.TenantId:N}"[..18], "Provisioning test tenant", "Provisioning test organization", fixture.TenantId));

        Assert.True(result.Code == TenantProvisioningCode.Created,
            $"Expected a created Tenant with its protected phase, received {result.Code}: {result.Error}");
        Assert.NotNull(result.Value);
        var organization = await fixture.Db.Organizations.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == result.Value.OrganizationId);
        var phase = await fixture.Db.ProjectPhaseCatalogEntries.IgnoreQueryFilters()
            .SingleAsync(item => item.OrganizationId == organization.Id && item.NormalizedCode == "PRE-INITIATION");
        Assert.Equal(fixture.TenantId, phase.TenantId);
        Assert.True(phase.IsActive);
        Assert.True(phase.IsDefault);
        Assert.Equal(0, phase.DisplayOrder);
        Assert.Equal("Pre-Initiation", phase.NameEn);
        Assert.Equal("مرحلة ما قبل البدء", phase.NameAr);
    }

    [Fact]
    public async Task CreateAsync_RollsBackTenantAndOrganizationWhenPhaseProvisioningFails()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var tenantScope = TrustedTenantExecutionContext.Enter(fixture.TenantId);
        fixture.EstablishAuthenticatedRequest();
        var audit = CreateSuccessfulAudit();
        var lifecycle = new Mock<IOrganizationLifecycleHandler>();
        lifecycle.Setup(item => item.HandleOrganizationCreatedAsync(
                fixture.TenantId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Conflict("phase provisioning rejected")));
        var provisioning = new TenantProvisioningService(fixture.Db, fixture.Http, audit.Object, lifecycle.Object);

        var result = await provisioning.CreateAsync(new CreateTenantRequest(
            $"TEN-{fixture.TenantId:N}"[..18], "Rollback test tenant", "Rollback test organization", fixture.TenantId));

        Assert.Equal(TenantProvisioningCode.ValidationFailed, result.Code);
        Assert.Equal(0, await fixture.Db.Tenants.IgnoreQueryFilters().CountAsync(item => item.Id == fixture.TenantId));
        Assert.Equal(0, await fixture.Db.Organizations.IgnoreQueryFilters().CountAsync(item => item.TenantId == fixture.TenantId));
    }

    private static Mock<IAuditService> CreateSuccessfulAudit()
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(item => item.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return audit;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public AppDbContext Db { get; }
        public HttpContextAccessor Http { get; }
        public Guid TenantId { get; }
        public Guid ActorId { get; }

        private Fixture(SqliteConnection connection, AppDbContext db, HttpContextAccessor http, Guid tenantId, Guid actorId)
        {
            _connection = connection;
            Db = db;
            Http = http;
            TenantId = tenantId;
            ActorId = actorId;
        }

        public void EstablishAuthenticatedRequest() => Http.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, ActorId.ToString("D")),
                new Claim("tenantId", TenantId.ToString("D"))
            ], "phase-provisioning-test"))
        };

        public static async Task<Fixture> CreateAsync()
        {
            var tenantId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var http = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")),
                        new Claim("tenantId", tenantId.ToString("D"))
                    ], "phase-provisioning-test"))
                }
            };
            AppDbContext.ConfigureTenantResolution(http);

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = $"preinit-provision-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 30
            }.ToString();
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
            var db = new SqliteTestAppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, db, http, tenantId, actorId);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
