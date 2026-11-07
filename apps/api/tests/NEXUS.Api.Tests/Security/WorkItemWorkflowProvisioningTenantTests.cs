using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.Modules.WorkItems.Application;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class WorkItemWorkflowProvisioningTenantTests
{
    private static readonly Guid TenantA = Guid.Parse("a1100000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("b2200000-0000-0000-0000-000000000002");

    [Fact]
    public async Task EnsureCurrentTenant_UsesAuthenticatedTenantClaim_AndIgnoresAmbientTrustedTenantDuringHttpRequest()
    {
        await using var fixture = await Fixture.CreateAsync();
        var accessor = AuthenticatedHttpContext(TenantA);
        var service = fixture.CreateService(accessor);

        using var trusted = TrustedTenantExecutionContext.Enter(TenantB);
        var result = await service.EnsureCurrentTenantAsync("test-actor");

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.DefinitionAId, result.Value!.DefinitionId);
        Assert.NotEqual(fixture.DefinitionBId, result.Value.DefinitionId);
    }

    [Fact]
    public async Task EnsureCurrentTenant_WithoutHttpOrTrustedTenant_FailsClosed()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = fixture.CreateService(new HttpContextAccessor());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureCurrentTenantAsync("test-actor"));
    }

    [Fact]
    public async Task EnsureCurrentTenant_UnauthenticatedHttpContext_DoesNotUseTenantClaimOrTrustedTenant()
    {
        await using var fixture = await Fixture.CreateAsync();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenantId", TenantA.ToString()) }))
            }
        };
        var service = fixture.CreateService(accessor);

        using var trusted = TrustedTenantExecutionContext.Enter(TenantB);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureCurrentTenantAsync("test-actor"));
    }

    [Fact]
    public async Task EnsureCurrentTenant_TrustedSeedContext_SelectsOnlyExplicitTenant_WhenHttpContextIsAbsent()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = fixture.CreateService(new HttpContextAccessor());

        using (TrustedTenantExecutionContext.Enter(TenantB))
        {
            var result = await service.EnsureCurrentTenantAsync("trusted-seed");
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(fixture.DefinitionBId, result.Value!.DefinitionId);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureCurrentTenantAsync("after-seed"));
    }

    [Fact]
    public async Task EnsureCurrentTenant_InvalidHttpTenantClaim_IgnoresTrustedSeedContext()
    {
        await using var fixture = await Fixture.CreateAsync();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenantId", "not-a-guid") }, "test"))
            }
        };
        var service = fixture.CreateService(accessor);

        using var trusted = TrustedTenantExecutionContext.Enter(TenantB);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureCurrentTenantAsync("test-actor"));
    }

    private static IHttpContextAccessor AuthenticatedHttpContext(Guid tenantId) => new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("tenantId", tenantId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, "actor-a")
            }, "test"))
        }
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ProvisioningTestDbContext _db;
        private readonly Mock<IWorkItemStatusReferenceLookup> _statusLookup;

        private Fixture(ProvisioningTestDbContext db, Guid definitionAId, Guid definitionBId)
        {
            _db = db;
            DefinitionAId = definitionAId;
            DefinitionBId = definitionBId;
            _statusLookup = new Mock<IWorkItemStatusReferenceLookup>();
            _statusLookup.Setup(lookup => lookup.GetAsync(false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorkItemStatusReference[]
                {
                    new(Guid.NewGuid(), "Open", "Open", "Open", 1, true, null, true)
                });
        }

        public Guid DefinitionAId { get; }
        public Guid DefinitionBId { get; }

        public WorkItemWorkflowProvisioningService CreateService(IHttpContextAccessor accessor) =>
            new(_db, _statusLookup.Object, accessor);

        public static async Task<Fixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ProvisioningTestDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            var db = new ProvisioningTestDbContext(options);
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();

            var definitionA = CreatePublishedDefinition(TenantA, WorkItemWorkflow.DefinitionCode);
            var definitionB = CreatePublishedDefinition(TenantB, WorkItemWorkflow.DefinitionCode);
            db.Set<WorkflowDefinition>().AddRange(definitionA.Definition, definitionB.Definition);
            await db.SaveChangesAsync();

            return new Fixture(db, definitionA.Definition.Id, definitionB.Definition.Id);
        }

        private static (WorkflowDefinition Definition, WorkflowDefinitionVersion Version) CreatePublishedDefinition(Guid tenantId, string code)
        {
            var definition = new WorkflowDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = code,
                Domain = WorkflowDomains.Task,
                EntityType = WorkItemWorkflow.EntityType,
                IsActive = true
            };
            var version = new WorkflowDefinitionVersion
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowDefinitionId = definition.Id,
                VersionNumber = 1,
                LifecycleStatus = WorkflowDefinitionVersionLifecycleStatus.Published
            };
            version.States.Add(new WorkflowState
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowDefinitionVersionId = version.Id,
                Code = "Open",
                NameEn = "Open",
                NameAr = "Open",
                IsInitial = true,
                IsActive = true
            });
            definition.Versions.Add(version);
            definition.ActivePublishedVersionId = version.Id;
            return (definition, version);
        }

        public async ValueTask DisposeAsync()
        {
            await _db.Database.CloseConnectionAsync();
            await _db.DisposeAsync();
        }
    }

    private sealed class ProvisioningTestDbContext(DbContextOptions<ProvisioningTestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WorkflowDefinition>(entity =>
            {
                entity.Ignore(definition => definition.ActivePublishedVersion);
                entity.HasMany(definition => definition.Versions)
                    .WithOne(version => version.WorkflowDefinition)
                    .HasForeignKey(version => version.WorkflowDefinitionId)
                    .OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<WorkflowDefinitionVersion>()
                .Property(version => version.LifecycleStatus)
                .HasConversion<string>();
            modelBuilder.Entity<WorkflowState>()
                .HasOne(state => state.WorkflowDefinitionVersion)
                .WithMany(version => version.States)
                .HasForeignKey(state => state.WorkflowDefinitionVersionId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<WorkflowTransition>()
                .HasOne(transition => transition.WorkflowDefinitionVersion)
                .WithMany(version => version.Transitions)
                .HasForeignKey(transition => transition.WorkflowDefinitionVersionId)
                .OnDelete(DeleteBehavior.NoAction);

            // Match the production composite state relationships so EF can
            // distinguish the incoming and outgoing transition collections.
            modelBuilder.Entity<WorkflowTransition>()
                .HasOne(transition => transition.FromState)
                .WithMany(state => state.OutgoingTransitions)
                .HasForeignKey(transition => new { transition.WorkflowDefinitionVersionId, transition.FromStateId })
                .HasPrincipalKey(state => new { state.WorkflowDefinitionVersionId, state.Id })
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<WorkflowTransition>()
                .HasOne(transition => transition.ToState)
                .WithMany(state => state.IncomingTransitions)
                .HasForeignKey(transition => new { transition.WorkflowDefinitionVersionId, transition.ToStateId })
                .HasPrincipalKey(state => new { state.WorkflowDefinitionVersionId, state.Id })
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
