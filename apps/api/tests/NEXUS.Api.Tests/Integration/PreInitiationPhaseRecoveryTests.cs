using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Moq;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;
using TenantEntity = NEXUS.Modules.Organization.Domain.Tenant;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Verifies the protected Pre-Initiation phase lifecycle: idempotent creation,
/// Organization scoping, duplicate prevention, and cross-Organization isolation.
/// </summary>
public sealed class PreInitiationPhaseRecoveryTests
{
    private const string CanonicalCode = "pre-initiation";
    private const string CanonicalNormalizedCode = "PRE-INITIATION";
    private const string CanonicalNameEn = "Pre-Initiation";
    private const string CanonicalNameAr = "\u0645\u0631\u062D\u0644\u0629 \u0645\u0627 \u0642\u0628\u0644 \u0627\u0644\u0628\u062F\u0621";
    private const int CanonicalDisplayOrder = 0;

    private static async Task<(AppDbContext Db, TenantEntity Tenant, OrganizationEntity Org)> CreateTenantAndOrgAsync(string suffix)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"DataSource=file:memdb-{Guid.NewGuid()};Mode=Memory;Cache=Shared")
            .Options;
        var db = new SqliteTestAppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Code = $"TENANT-{suffix}",
            DisplayName = $"Tenant {suffix}",
            Status = TenantLifecycleStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        db.Set<TenantEntity>().Add(tenant);

        var org = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = $"Organization {suffix}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        db.Set<OrganizationEntity>().Add(org);
        using (TrustedTenantExecutionContext.Enter(tenant.Id))
            await db.SaveChangesAsync();

        return (db, tenant, org);
    }

    [Fact]
    public async Task EnsurePreInitiation_CreatesExactlyOnePhase_ForNewOrganization()
    {
        var (db, tenant, org) = await CreateTenantAndOrgAsync("NEW-ORG");
        await using (db)
        {
            using var tenantScope = TrustedTenantExecutionContext.Enter(tenant.Id);
            var service = new ProjectLifecycleCatalogService(db, new HttpContextAccessor(), Mock.Of<IAuditService>());
            var actorId = Guid.NewGuid();

            var result1 = await service.EnsurePreInitiationPhaseAsync(tenant.Id, org.Id, actorId);
            var result2 = await service.EnsurePreInitiationPhaseAsync(tenant.Id, org.Id, actorId);

            Assert.True(result1.IsSuccess);
            Assert.True(result2.IsSuccess);
            Assert.Equal(result1.Value!.Id, result2.Value!.Id);

            var count = await db.Set<ProjectPhaseCatalogEntry>().IgnoreQueryFilters()
                .CountAsync(p => p.TenantId == tenant.Id && p.OrganizationId == org.Id
                    && p.NormalizedCode == CanonicalNormalizedCode);
            Assert.Equal(1, count);

            var phase = result1.Value!;
            Assert.Equal(CanonicalCode, phase.Code);
            Assert.Equal(CanonicalNormalizedCode, phase.NormalizedCode);
            Assert.Equal(CanonicalNameEn, phase.NameEn);
            Assert.Equal(CanonicalNameAr, phase.NameAr);
            Assert.Equal(CanonicalDisplayOrder, phase.DisplayOrder);
            Assert.True(phase.IsActive);
            Assert.True(phase.IsDefault);
            Assert.False(phase.IsContinuousActivity);
        }
    }

    [Fact]
    public async Task EnsurePreInitiation_RecoversMissingPhase_WithoutDuplicating()
    {
        var (db, tenant, org) = await CreateTenantAndOrgAsync("RECOVER");
        await using (db)
        {
            using var tenantScope = TrustedTenantExecutionContext.Enter(tenant.Id);
            var service = new ProjectLifecycleCatalogService(db, new HttpContextAccessor(), Mock.Of<IAuditService>());
            var actorId = Guid.NewGuid();

            // First call creates the phase
            var first = await service.EnsurePreInitiationPhaseAsync(tenant.Id, org.Id, actorId);
            Assert.True(first.IsSuccess);
            var originalId = first.Value!.Id;

            // Simulate deletion (soft-delete)
            var tracked = await db.Set<ProjectPhaseCatalogEntry>()
                .FirstAsync(p => p.Id == originalId);
            tracked.IsDeleted = true;
            await db.SaveChangesAsync();

            // Recovery should restore the same identity
            var recovered = await service.EnsurePreInitiationPhaseAsync(tenant.Id, org.Id, actorId);
            Assert.True(recovered.IsSuccess);
            Assert.Equal(originalId, recovered.Value!.Id);

            // Verify no duplicates
            var count = await db.Set<ProjectPhaseCatalogEntry>().IgnoreQueryFilters()
                .CountAsync(p => p.TenantId == tenant.Id && p.OrganizationId == org.Id
                    && p.NormalizedCode == CanonicalNormalizedCode);
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task EnsurePreInitiation_RecoversInactiveExistingPhase_AndRestoresCanonicalDefault()
    {
        var (db, tenant, org) = await CreateTenantAndOrgAsync("INACTIVE-RECOVER");
        await using (db)
        {
            using var tenantScope = TrustedTenantExecutionContext.Enter(tenant.Id);
            var service = new ProjectLifecycleCatalogService(db, new HttpContextAccessor(), Mock.Of<IAuditService>());
            var actorId = Guid.NewGuid();
            var initial = await service.EnsurePreInitiationPhaseAsync(tenant.Id, org.Id, actorId);
            Assert.True(initial.IsSuccess);

            var inactive = await db.Set<ProjectPhaseCatalogEntry>().SingleAsync(item => item.Id == initial.Value!.Id);
            inactive.IsActive = false;
            inactive.IsDefault = false;
            await db.SaveChangesAsync();

            var recovered = await service.EnsurePreInitiationPhaseAsync(tenant.Id, org.Id, actorId);

            Assert.True(recovered.IsSuccess, recovered.Error?.Message);
            Assert.Equal(initial.Value!.Id, recovered.Value!.Id);
            Assert.True(recovered.Value.IsActive);
            Assert.True(recovered.Value.IsDefault);
            Assert.Equal(CanonicalDisplayOrder, recovered.Value.DisplayOrder);
        }
    }

    [Fact]
    public async Task EnsurePreInitiation_DoesNotCreateTenantLevelPhase()
    {
        var (db, tenant, org) = await CreateTenantAndOrgAsync("NO-TENANT");
        await using (db)
        {
            using var tenantScope = TrustedTenantExecutionContext.Enter(tenant.Id);
            var service = new ProjectLifecycleCatalogService(db, new HttpContextAccessor(), Mock.Of<IAuditService>());
            var actorId = Guid.NewGuid();

            // Should fail when organizationId is null (Tenant-level not allowed)
            var result = await service.EnsurePreInitiationPhaseAsync(tenant.Id, null, actorId);
            Assert.True(result.IsFailure);

            // Verify no Tenant-level Pre-Initiation was created
            var tenantLevelCount = await db.Set<ProjectPhaseCatalogEntry>().IgnoreQueryFilters()
                .CountAsync(p => p.TenantId == tenant.Id && p.OrganizationId == null
                    && p.NormalizedCode == CanonicalNormalizedCode);
            Assert.Equal(0, tenantLevelCount);
        }
    }

    [Fact]
    public async Task EnsurePreInitiation_IsolatesByOrganization()
    {
        var (db, tenant, org1) = await CreateTenantAndOrgAsync("ISO-1");
        await using (db)
        {
            using var tenantScope = TrustedTenantExecutionContext.Enter(tenant.Id);
            // Create a second Organization in the same Tenant
            var org2 = new OrganizationEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "Organization 2",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test"
            };
            db.Set<OrganizationEntity>().Add(org2);
            await db.SaveChangesAsync();

            var service = new ProjectLifecycleCatalogService(db, new HttpContextAccessor(), Mock.Of<IAuditService>());
            var actorId = Guid.NewGuid();

            var phase1 = await service.EnsurePreInitiationPhaseAsync(tenant.Id, org1.Id, actorId);
            var phase2 = await service.EnsurePreInitiationPhaseAsync(tenant.Id, org2.Id, actorId);

            Assert.True(phase1.IsSuccess);
            Assert.True(phase2.IsSuccess);
            Assert.NotEqual(phase1.Value!.Id, phase2.Value!.Id);

            // Each Organization should have exactly one Pre-Initiation phase
            var org1Count = await db.Set<ProjectPhaseCatalogEntry>().IgnoreQueryFilters()
                .CountAsync(p => p.OrganizationId == org1.Id && p.NormalizedCode == CanonicalNormalizedCode);
            var org2Count = await db.Set<ProjectPhaseCatalogEntry>().IgnoreQueryFilters()
                .CountAsync(p => p.OrganizationId == org2.Id && p.NormalizedCode == CanonicalNormalizedCode);
            Assert.Equal(1, org1Count);
            Assert.Equal(1, org2Count);
        }
    }

    [Fact]
    public async Task EnsurePreInitiation_PreservesExistingPhaseId()
    {
        var (db, tenant, org) = await CreateTenantAndOrgAsync("PRESERVE-ID");
        await using (db)
        {
            using var tenantScope = TrustedTenantExecutionContext.Enter(tenant.Id);
            var service = new ProjectLifecycleCatalogService(db, new HttpContextAccessor(), Mock.Of<IAuditService>());
            var actorId = Guid.NewGuid();

            // Create
            var first = await service.EnsurePreInitiationPhaseAsync(tenant.Id, org.Id, actorId);
            Assert.True(first.IsSuccess);
            var originalId = first.Value!.Id;

            // Call again — should return same ID
            var second = await service.EnsurePreInitiationPhaseAsync(tenant.Id, org.Id, actorId);
            Assert.True(second.IsSuccess);
            Assert.Equal(originalId, second.Value!.Id);

            // Call again — still same
            var third = await service.EnsurePreInitiationPhaseAsync(tenant.Id, org.Id, actorId);
            Assert.True(third.IsSuccess);
            Assert.Equal(originalId, third.Value!.Id);
        }
    }

    [Fact]
    public async Task EnsurePreInitiation_ClearsConflictingDefaults()
    {
        var (db, tenant, org) = await CreateTenantAndOrgAsync("CONFLICT-DEFAULT");
        await using (db)
        {
            using var tenantScope = TrustedTenantExecutionContext.Enter(tenant.Id);
            var actorId = Guid.NewGuid();

            // Manually create a conflicting default phase
            var conflictingPhase = new ProjectPhaseCatalogEntry
            {
                TenantId = tenant.Id,
                OrganizationId = org.Id,
                Code = "planning",
                NormalizedCode = "PLANNING",
                NameEn = "Planning",
                NameAr = "التخطيط",
                DisplayOrder = 20,
                IsDefault = true,
                IsActive = true,
                CreatedBy = actorId.ToString()
            };
            db.Set<ProjectPhaseCatalogEntry>().Add(conflictingPhase);
            await db.SaveChangesAsync();

            var service = new ProjectLifecycleCatalogService(db, new HttpContextAccessor(), Mock.Of<IAuditService>());
            var result = await service.EnsurePreInitiationPhaseAsync(tenant.Id, org.Id, actorId);

            Assert.True(result.IsSuccess);

            // Pre-Initiation should be the default
            Assert.True(result.Value!.IsDefault);

            // Conflicting phase should no longer be default
            var tracked = await db.Set<ProjectPhaseCatalogEntry>()
                .FirstAsync(p => p.Id == conflictingPhase.Id);
            Assert.False(tracked.IsDefault);
        }
    }

    [Fact]
    public async Task HandleOrganizationCreatedAsync_ProvisionsPreInitiation()
    {
        var (db, tenant, org) = await CreateTenantAndOrgAsync("HANDLE-CREATE");
        await using (db)
        {
            using var tenantScope = TrustedTenantExecutionContext.Enter(tenant.Id);
            var service = new ProjectLifecycleCatalogService(db, new HttpContextAccessor(), Mock.Of<IAuditService>());
            var actorId = Guid.NewGuid();

            var result = await service.HandleOrganizationCreatedAsync(tenant.Id, org.Id, actorId);

            Assert.True(result.IsSuccess, result.Error?.Message);
            var count = await db.Set<ProjectPhaseCatalogEntry>().IgnoreQueryFilters()
                .CountAsync(p => p.TenantId == tenant.Id && p.OrganizationId == org.Id
                    && p.NormalizedCode == CanonicalNormalizedCode);
            Assert.Equal(1, count);
        }
    }
}
