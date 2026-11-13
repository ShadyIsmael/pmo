using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NEXUS.Persistence;
using NEXUS.Api.Management;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;
using TenantEntity = NEXUS.Modules.Organization.Domain.Tenant;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Contract for the single protected Pre-Initiation lifecycle phase: one canonical,
/// active, default, order-zero record per exact Tenant/Organization scope, created
/// only through the approved provisioning/recovery path.
/// </summary>
[Collection("Static tenant resolution")]
public sealed class PreInitiationPhaseProtectionTests
{
    private const string CanonicalCode = "pre-initiation";
    private const string CanonicalNormalizedCode = "PRE-INITIATION";
    private const string CanonicalNameEn = "Pre-Initiation";
    private const string CanonicalNameAr = "مرحلة ما قبل البدء";
    private const int CanonicalDisplayOrder = 0;

    private static readonly (string Code, string NameEn, string NameAr, int Order, bool Continuous)[] LegacyPhaseSeeds =
    [
        (CanonicalCode, CanonicalNameEn, CanonicalNameAr, 10, false),
        ("initiation", "Initiation", "البدء", 20, false),
        ("planning", "Planning", "التخطيط", 30, false),
        ("execution", "Execution", "التنفيذ", 40, false),
        ("monitoring-controlling", "Monitoring & Controlling", "المراقبة والتحكم", 50, true),
        ("closure", "Closure", "الإغلاق", 60, false)
    ];

    [Fact]
    public async Task PreInitiationPhase_CannotBeDeactivated()
    {
        await using var host = await TestHost.CreateAsync();

        var preInitiation = await host.FindTenantPhaseAsync();
        Assert.NotNull(preInitiation);

        var service = host.CreateLifecycleService();

        // Deactivation is the lifecycle catalog soft-delete operation.
        var result = await service.SetPhaseActiveAsync(preInitiation!.Id, false, preInitiation.RowVersion, host.ActorId);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode); // Conflict
        Assert.Contains("pre-initiation", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("deactivat", result.Error.Message, StringComparison.OrdinalIgnoreCase);

        var persisted = await host.FindTenantPhaseAsync();
        Assert.True(persisted!.IsActive);
        Assert.Equal(CanonicalCode, persisted.Code);
    }

    [Fact]
    public async Task PreInitiationPhase_CannotBeUpdated()
    {
        await using var host = await TestHost.CreateAsync();

        var preInitiation = await host.FindTenantPhaseAsync();
        Assert.NotNull(preInitiation);

        var service = host.CreateLifecycleService();

        // Attempt to update the Pre-Initiation phase.
        var request = new ProjectLifecycleCatalogRequest(
            OrganizationId: null,
            Code: "pre-initiation-renamed",
            NameEn: "Pre-Initiation Modified",
            NameAr: "مرحلة ما قبل البدء",
            DescriptionEn: null,
            DescriptionAr: null,
            DisplayOrder: preInitiation!.DisplayOrder,
            IsDefault: true,
            IsActive: true,
            IsContinuousActivity: false,
            RowVersion: preInitiation.RowVersion);

        var result = await service.UpdatePhaseAsync(preInitiation.Id, request, host.ActorId);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode); // Conflict
        Assert.Contains("protected", result.Error.Message, StringComparison.OrdinalIgnoreCase);

        var persisted = await host.FindTenantPhaseAsync();
        Assert.Equal(CanonicalCode, persisted!.Code);
        Assert.Equal(CanonicalNameEn, persisted.NameEn);
        Assert.Equal(CanonicalDisplayOrder, persisted.DisplayOrder);
    }

    [Fact]
    public async Task PreInitiationPhase_CannotBeReordered()
    {
        await using var host = await TestHost.CreateAsync();

        var preInitiation = await host.FindTenantPhaseAsync();
        Assert.NotNull(preInitiation);

        var service = host.CreateLifecycleService();

        // Attempt to change DisplayOrder (reorder).
        var request = new ProjectLifecycleCatalogRequest(
            OrganizationId: null,
            Code: CanonicalCode,
            NameEn: CanonicalNameEn,
            NameAr: CanonicalNameAr,
            DescriptionEn: null,
            DescriptionAr: null,
            DisplayOrder: 99,
            IsDefault: true,
            IsActive: true,
            IsContinuousActivity: false,
            RowVersion: preInitiation!.RowVersion);

        var result = await service.UpdatePhaseAsync(preInitiation.Id, request, host.ActorId);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode); // Conflict
        Assert.Contains("protected", result.Error.Message, StringComparison.OrdinalIgnoreCase);

        var persisted = await host.FindTenantPhaseAsync();
        Assert.Equal(CanonicalDisplayOrder, persisted!.DisplayOrder);
    }

    [Fact]
    public async Task CannotCreateDuplicatePreInitiationPhaseInSameScope()
    {
        await using var host = await TestHost.CreateAsync();

        var service = host.CreateLifecycleService();

        // Attempt to create another Pre-Initiation phase at tenant level.
        var request = new ProjectLifecycleCatalogRequest(
            OrganizationId: null,
            Code: CanonicalCode,
            NameEn: "Pre-Initiation Duplicate",
            NameAr: CanonicalNameAr,
            DescriptionEn: null,
            DescriptionAr: null,
            DisplayOrder: 5,
            IsDefault: false,
            IsActive: true,
            IsContinuousActivity: false);

        var result = await service.CreatePhaseAsync(request, host.ActorId);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode); // Conflict
        Assert.Contains("already exists", result.Error.Message, StringComparison.OrdinalIgnoreCase);

        var tenantPhases = await host.PhaseRowsAsync(host.TenantId, null);
        Assert.Single(tenantPhases, phase => phase.NormalizedCode == CanonicalNormalizedCode);
    }

    [Fact]
    public async Task PreInitiationPhase_IsProvisionedIndependentlyForEachOrganization()
    {
        await using var host = await TestHost.CreateAsync();
        var firstOrganizationId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();
        await host.AddOrganizationAsync(firstOrganizationId, host.TenantId, "First Test Organization");
        await host.AddOrganizationAsync(secondOrganizationId, host.TenantId, "Second Test Organization");

        var service = host.CreateLifecycleService();
        var first = await service.EnsurePreInitiationPhaseAsync(host.TenantId, firstOrganizationId, host.ActorId);
        var second = await service.EnsurePreInitiationPhaseAsync(host.TenantId, secondOrganizationId, host.ActorId);
        var tenant = await service.EnsurePreInitiationPhaseAsync(host.TenantId, null, host.ActorId);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(tenant.IsSuccess);
        Assert.NotNull(first.Value);
        Assert.NotNull(second.Value);
        Assert.NotEqual(first.Value.Id, second.Value.Id);
        Assert.NotEqual(first.Value.Id, tenant.Value!.Id);
        Assert.Equal(host.TenantId, tenant.Value.TenantId);
        Assert.Null(tenant.Value.OrganizationId);

        AssertPreInitiationContract(first.Value, expectedOrganizationId: firstOrganizationId);
        AssertPreInitiationContract(second.Value, expectedOrganizationId: secondOrganizationId);
        AssertPreInitiationContract(tenant.Value, expectedOrganizationId: null);

        Assert.Single(await host.PhaseRowsAsync(host.TenantId, firstOrganizationId));
        Assert.Single(await host.PhaseRowsAsync(host.TenantId, secondOrganizationId));

        // The Tenant scope keeps its legacy phases and exactly one Pre-Initiation identity.
        var tenantRows = await host.PhaseRowsAsync(host.TenantId, null);
        Assert.Equal(3, tenantRows.Count);
        Assert.Single(tenantRows, phase => phase.NormalizedCode == CanonicalNormalizedCode);
    }

    [Fact]
    public async Task PreInitiationPhase_RecoveryIsIdempotentAndRepairsExistingEntry()
    {
        await using var host = await TestHost.CreateAsync();
        var service = host.CreateLifecycleService();

        var first = await service.EnsurePreInitiationPhaseAsync(host.TenantId, host.OrganizationId, host.ActorId);
        var second = await service.EnsurePreInitiationPhaseAsync(host.TenantId, host.OrganizationId, host.ActorId);
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Id, second.Value!.Id);

        // Simulate a legacy row with drift that bypassed the protected API.
        var phase = await host.Db.ProjectPhaseCatalogEntries
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == first.Value.Id);
        phase.IsActive = false;
        phase.IsDefault = false;
        phase.DisplayOrder = 99;
        phase.NameEn = "Legacy Pre Initiation";
        phase.NameAr = "قديم";
        phase.IsContinuousActivity = true;
        await host.Db.SaveChangesAsync();

        var recovered = await service.EnsurePreInitiationPhaseAsync(host.TenantId, host.OrganizationId, host.ActorId);
        Assert.True(recovered.IsSuccess);
        Assert.Equal(first.Value.Id, recovered.Value!.Id);

        var persisted = await host.Db.ProjectPhaseCatalogEntries
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == first.Value.Id);
        AssertPreInitiationContract(persisted, expectedOrganizationId: host.OrganizationId);
        Assert.Equal(CanonicalNormalizedCode, persisted.NormalizedCode);
        Assert.Single(await host.PhaseRowsAsync(host.TenantId, host.OrganizationId));
    }

    [Fact]
    public async Task PreInitiationPhase_RecoveryReusesSoftDeletedIdentity()
    {
        await using var host = await TestHost.CreateAsync();
        var service = host.CreateLifecycleService();

        var seeded = await host.FindTenantPhaseAsync();
        Assert.NotNull(seeded);

        seeded!.IsDeleted = true;
        seeded.IsActive = false;
        seeded.IsDefault = false;
        await host.Db.SaveChangesAsync();

        var recovered = await service.EnsurePreInitiationPhaseAsync(host.TenantId, null, host.ActorId);

        Assert.True(recovered.IsSuccess);
        Assert.Equal(seeded.Id, recovered.Value!.Id);

        var rows = await host.PhaseRowsAsync(host.TenantId, null);
        Assert.Equal(3, rows.Count);
        var restored = Assert.Single(rows, phase => phase.NormalizedCode == CanonicalNormalizedCode);
        Assert.Equal(seeded.Id, restored.Id);
        AssertPreInitiationContract(restored, expectedOrganizationId: null);
    }

    [Fact]
    public async Task PreInitiationPhase_RepeatedProvisioningIsIdempotent()
    {
        await using var host = await TestHost.CreateAsync();
        var organizationId = Guid.NewGuid();
        await host.AddOrganizationAsync(organizationId, host.TenantId, "Repeated Provisioning Organization");
        var service = host.CreateLifecycleService();

        Guid? firstId = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var result = await service.EnsurePreInitiationPhaseAsync(host.TenantId, organizationId, host.ActorId);
            Assert.True(result.IsSuccess);
            AssertPreInitiationContract(result.Value!, expectedOrganizationId: organizationId);
            firstId ??= result.Value!.Id;
            Assert.Equal(firstId, result.Value!.Id);
        }

        Assert.Single(await host.PhaseRowsAsync(host.TenantId, organizationId));
    }

    [Fact]
    public async Task PreInitiationProvisioning_CreatesNoNonPreInitiationPhases()
    {
        await using var host = await TestHost.CreateAsync(seedPhaseCatalog: false);
        var organizationId = Guid.NewGuid();
        await host.AddOrganizationAsync(organizationId, host.TenantId, "Empty Catalog Organization");
        var service = host.CreateLifecycleService();

        var tenant = await service.EnsurePreInitiationPhaseAsync(host.TenantId, null, host.ActorId);
        var organization = await service.EnsurePreInitiationPhaseAsync(host.TenantId, organizationId, host.ActorId);

        Assert.True(tenant.IsSuccess);
        Assert.True(organization.IsSuccess);

        var tenantPhases = await host.PhaseRowsAsync(host.TenantId, null);
        var organizationPhases = await host.PhaseRowsAsync(host.TenantId, organizationId);
        Assert.Single(tenantPhases);
        Assert.Equal(CanonicalNormalizedCode, tenantPhases[0].NormalizedCode);
        Assert.Single(organizationPhases);
        Assert.Equal(CanonicalNormalizedCode, organizationPhases[0].NormalizedCode);

        var allPhases = await host.Db.ProjectPhaseCatalogEntries.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, allPhases.Count);
        Assert.All(allPhases, phase => Assert.Equal(CanonicalNormalizedCode, phase.NormalizedCode));
    }

    [Fact]
    public async Task PreInitiationPhase_RecoveryRestoresCanonicalCode()
    {
        await using var host = await TestHost.CreateAsync();
        var service = host.CreateLifecycleService();

        var seeded = await host.FindTenantPhaseAsync();
        Assert.NotNull(seeded);

        // Legacy drift that keeps the normalized identity but changes the display code.
        seeded!.Code = "Pre Initiation";
        await host.Db.SaveChangesAsync();

        var recovered = await service.EnsurePreInitiationPhaseAsync(host.TenantId, null, host.ActorId);

        Assert.True(recovered.IsSuccess);
        var persisted = await host.FindTenantPhaseAsync();
        Assert.Equal(CanonicalCode, persisted!.Code);
        Assert.Equal(CanonicalNormalizedCode, persisted.NormalizedCode);
    }

    [Fact]
    public async Task SeedProvisioning_CreatesOnlyPreInitiationAndIsIdempotent()
    {
        await using var host = await TestHost.CreateAsync(SeedDataProvider.TenantId, seedPhaseCatalog: false);

        await host.RunLifecycleSeedAsync();

        var firstRun = await host.PhaseRowsAsync(SeedDataProvider.TenantId, null);
        Assert.Single(firstRun);
        AssertPreInitiationContract(firstRun[0], expectedOrganizationId: null);
        var seededId = firstRun[0].Id;

        await host.RunLifecycleSeedAsync();

        var secondRun = await host.PhaseRowsAsync(SeedDataProvider.TenantId, null);
        Assert.Single(secondRun);
        Assert.Equal(seededId, secondRun[0].Id);
        AssertPreInitiationContract(secondRun[0], expectedOrganizationId: null);
    }

    [Fact]
    public async Task SeedProvisioning_RestoresSoftDeletedPreInitiationWithoutDuplicate()
    {
        await using var host = await TestHost.CreateAsync(SeedDataProvider.TenantId, seedPhaseCatalog: false);
        await host.RunLifecycleSeedAsync();

        var seeded = await host.FindTenantPhaseAsync();
        Assert.NotNull(seeded);
        seeded!.IsDeleted = true;
        seeded.IsActive = false;
        seeded.IsDefault = false;
        await host.Db.SaveChangesAsync();

        await host.RunLifecycleSeedAsync();

        var rows = await host.PhaseRowsAsync(SeedDataProvider.TenantId, null);
        Assert.Single(rows);
        Assert.Equal(seeded.Id, rows[0].Id);
        AssertPreInitiationContract(rows[0], expectedOrganizationId: null);
    }

    [Fact]
    public async Task SeedProvisioning_RepairsLegacyPreInitiationAndCreatesNoOtherPhases()
    {
        await using var host = await TestHost.CreateAsync(SeedDataProvider.TenantId, seedPhaseCatalog: false);
        var legacyIds = await host.SeedLegacyPhaseCatalogAsync(SeedDataProvider.TenantId);
        var legacyPreInitiationId = legacyIds[CanonicalNormalizedCode];

        await host.RunLifecycleSeedAsync();

        var phases = await host.PhaseRowsAsync(SeedDataProvider.TenantId, null);
        Assert.Equal(LegacyPhaseSeeds.Length, phases.Count);
        var preInitiation = Assert.Single(phases, phase => phase.NormalizedCode == CanonicalNormalizedCode);
        Assert.Equal(legacyPreInitiationId, preInitiation.Id);
        AssertPreInitiationContract(preInitiation, expectedOrganizationId: null);

        // The other already-persisted legacy phases are not deleted or relabelled by the seed.
        foreach (var legacy in LegacyPhaseSeeds.Where(seed => seed.Code != CanonicalCode))
        {
            var persisted = Assert.Single(phases,
                phase => phase.NormalizedCode == legacy.Code.ToUpperInvariant());
            Assert.Equal(legacy.NameEn, persisted.NameEn);
            Assert.Equal(legacy.Order, persisted.DisplayOrder);
            Assert.False(persisted.IsDefault);
        }

        await host.RunLifecycleSeedAsync();
        Assert.Equal(LegacyPhaseSeeds.Length, (await host.PhaseRowsAsync(SeedDataProvider.TenantId, null)).Count);
    }

    [Fact]
    public async Task LifecycleOptions_ExposeOnePreInitiationOptionAndKeepItDefault()
    {
        await using var host = await TestHost.CreateAsync();
        var service = host.CreateLifecycleService();

        var result = await service.GetOptionsAsync(host.OrganizationId, projectId: null);

        Assert.True(result.IsSuccess, result.Error?.Message);

        // The Tenant-wide default is overridden by the exact Organization record, so the
        // two Pre-Initiation identities must collapse to a single visible option.
        var preInitiationOptions = result.Value!.Phases
            .Where(option => string.Equals(option.Code, CanonicalCode, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var option = Assert.Single(preInitiationOptions);
        Assert.True(option.IsDefault);
        Assert.Equal(CanonicalDisplayOrder, option.DisplayOrder);
        Assert.Equal(CanonicalNameEn, option.NameEn);
        Assert.Equal(CanonicalNameAr, option.NameAr);

        var organizationRows = await host.PhaseRowsAsync(host.TenantId, host.OrganizationId);
        var organizationPhase = Assert.Single(organizationRows);
        Assert.Equal(organizationPhase.Id, option.Id);

        // Tenant initiation/planning plus the Organization Pre-Initiation; the tenant-wide
        // Pre-Initiation is replaced rather than duplicated.
        Assert.Equal(3, result.Value.Phases.Count);
        Assert.Equal(1, result.Value.Phases.Count(candidate =>
            string.Equals(candidate.Code, CanonicalCode, StringComparison.OrdinalIgnoreCase)));
    }

    private static void AssertPreInitiationContract(ProjectPhaseCatalogEntry phase, Guid? expectedOrganizationId)
    {
        Assert.Equal(CanonicalCode, phase.Code);
        Assert.Equal(CanonicalNormalizedCode, phase.NormalizedCode);
        Assert.Equal(CanonicalNameEn, phase.NameEn);
        Assert.Equal(CanonicalNameAr, phase.NameAr);
        Assert.Equal(CanonicalDisplayOrder, phase.DisplayOrder);
        Assert.True(phase.IsActive);
        Assert.True(phase.IsDefault);
        Assert.False(phase.IsDeleted);
        Assert.False(phase.IsContinuousActivity);
        Assert.Equal(expectedOrganizationId, phase.OrganizationId);
    }

    /// <summary>
    /// Plain-field accessor: an <see cref="HttpContextAccessor"/> stores the context in an
    /// AsyncLocal, so a value assigned inside an async test-host factory never flows back
    /// to the caller. A plain property keeps the trusted test tenant visible for the
    /// lifetime of the host.
    /// </summary>
    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly FixedHttpContextAccessor _http;

        private TestHost(
            SqliteConnection connection,
            FixedHttpContextAccessor http,
            ServiceProvider provider,
            AppDbContext db,
            Guid tenantId,
            Guid actorId,
            Guid organizationId,
            Mock<IOrganizationSettingsAccessService> organizationAccess,
            PolicyAuthorizationService authorization,
            Mock<IAuditService> audit,
            List<AuditEntry> auditEntries)
        {
            _connection = connection;
            _http = http;
            Db = db;
            TenantId = tenantId;
            ActorId = actorId;
            OrganizationId = organizationId;
            OrganizationAccess = organizationAccess;
            Authorization = authorization;
            Audit = audit;
            AuditEntries = auditEntries;
            Provider = provider;
        }

        public AppDbContext Db { get; }
        public Guid TenantId { get; }
        public Guid ActorId { get; }
        public Guid OrganizationId { get; }
        public Mock<IOrganizationSettingsAccessService> OrganizationAccess { get; }
        internal PolicyAuthorizationService Authorization { get; }
        public Mock<IAuditService> Audit { get; }
        public List<AuditEntry> AuditEntries { get; }
        public ServiceProvider Provider { get; }

        public static async Task<TestHost> CreateAsync(Guid? tenantId = null, bool seedPhaseCatalog = true)
        {
            var resolvedTenantId = tenantId ?? Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var organizationId = Guid.NewGuid();
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var http = new FixedHttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { User = Principal(resolvedTenantId, actorId) }
            };
            AppDbContext.ConfigureTenantResolution(http);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);

            // Create only the required tables with SQLite-compatible schema
            // (the full EnsureCreatedAsync() fails because it includes SQL Server-specific types).
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS Tenants (
                    Id TEXT NOT NULL PRIMARY KEY,
                    Code TEXT NOT NULL,
                    DisplayName TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    CreatedBy TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    UpdatedBy TEXT,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8))
                );

                CREATE TABLE IF NOT EXISTS Organizations (
                    Id TEXT NOT NULL,
                    TenantId TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Code TEXT,
                    Description TEXT,
                    LogoUrl TEXT,
                    Website TEXT,
                    Email TEXT,
                    Phone TEXT,
                    AddressLine1 TEXT,
                    AddressLine2 TEXT,
                    City TEXT,
                    Country TEXT,
                    Postcode TEXT,
                    IsActive INTEGER NOT NULL,
                    CreatedBy TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    UpdatedBy TEXT,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    PRIMARY KEY (Id, TenantId)
                );

                CREATE TABLE IF NOT EXISTS ProjectPhaseCatalogEntries (
                    Id TEXT NOT NULL,
                    TenantId TEXT NOT NULL,
                    OrganizationId TEXT,
                    Code TEXT NOT NULL,
                    NormalizedCode TEXT NOT NULL,
                    NameEn TEXT NOT NULL,
                    NameAr TEXT NOT NULL,
                    DescriptionEn TEXT,
                    DescriptionAr TEXT,
                    DisplayOrder INTEGER NOT NULL,
                    IsDefault INTEGER NOT NULL DEFAULT 0,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    IsContinuousActivity INTEGER NOT NULL DEFAULT 0,
                    IsProtected INTEGER NOT NULL DEFAULT 0,
                    DocumentCategoryId TEXT,
                    CreatedBy TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    UpdatedBy TEXT,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    PRIMARY KEY (Id, TenantId)
                );

                CREATE TABLE IF NOT EXISTS ProjectStatusCatalogEntries (
                    Id TEXT NOT NULL,
                    TenantId TEXT NOT NULL,
                    OrganizationId TEXT,
                    Code TEXT NOT NULL,
                    NormalizedCode TEXT NOT NULL,
                    NameEn TEXT NOT NULL,
                    NameAr TEXT NOT NULL,
                    DescriptionEn TEXT,
                    DescriptionAr TEXT,
                    DisplayOrder INTEGER NOT NULL,
                    IsDefault INTEGER NOT NULL DEFAULT 0,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    ReportingCategory TEXT NOT NULL,
                    AttentionLevel TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    CreatedBy TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    UpdatedBy TEXT,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    PRIMARY KEY (Id, TenantId)
                );

                CREATE TABLE IF NOT EXISTS DocumentCategories (
                    Id TEXT NOT NULL,
                    TenantId TEXT NOT NULL,
                    OrganizationId TEXT,
                    NameEn TEXT NOT NULL,
                    NormalizedNameEn TEXT NOT NULL,
                    NameAr TEXT NOT NULL,
                    NormalizedNameAr TEXT NOT NULL,
                    IsActive INTEGER NOT NULL,
                    CreatedBy TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    UpdatedBy TEXT,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    PRIMARY KEY (Id, TenantId)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS AK_DocumentCategories_TenantId_Id
                    ON DocumentCategories(TenantId, Id);

                CREATE INDEX IF NOT EXISTS IX_ProjectPhaseCatalogEntries_TenantId_DocumentCategoryId
                    ON ProjectPhaseCatalogEntries(TenantId, DocumentCategoryId);

                -- Only the columns the Pre-Initiation recovery reference-count lookup reads.
                CREATE TABLE IF NOT EXISTS Projects (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TenantId TEXT NOT NULL,
                    ProjectPhaseId TEXT,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS AuditEntries (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TenantId TEXT NOT NULL,
                    EntityId TEXT NOT NULL,
                    EntityType TEXT NOT NULL,
                    Action TEXT NOT NULL,
                    UserId TEXT,
                    CreatedAt TEXT NOT NULL,
                    Changes TEXT,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8))
                );
                """);

            // Add tenant.
            db.Tenants.Add(new TenantEntity
            {
                Id = resolvedTenantId,
                Code = "TEST",
                DisplayName = "Test Tenant",
                Status = TenantLifecycleStatus.Active,
                CreatedBy = actorId.ToString()
            });

            // Add organization.
            db.Organizations.Add(new OrganizationEntity
            {
                Id = organizationId,
                TenantId = resolvedTenantId,
                Name = "Phase Protection Test Organization",
                IsActive = true,
                CreatedBy = actorId.ToString()
            });

            if (seedPhaseCatalog)
            {
                db.ProjectPhaseCatalogEntries.Add(new ProjectPhaseCatalogEntry
                {
                    TenantId = resolvedTenantId,
                    OrganizationId = null,
                    Code = CanonicalCode,
                    NormalizedCode = CanonicalNormalizedCode,
                    NameEn = CanonicalNameEn,
                    NameAr = CanonicalNameAr,
                    DisplayOrder = CanonicalDisplayOrder,
                    IsDefault = true,
                    IsActive = true,
                    CreatedBy = "System",
                    RowVersion = BitConverter.GetBytes(1)
                });

                // Legacy tenant-wide phases retained from the earlier six-phase migration.
                db.ProjectPhaseCatalogEntries.AddRange(
                    new ProjectPhaseCatalogEntry
                    {
                        TenantId = resolvedTenantId,
                        OrganizationId = null,
                        Code = "initiation",
                        NormalizedCode = "INITIATION",
                        NameEn = "Initiation",
                        NameAr = "البدء",
                        DisplayOrder = 20,
                        IsDefault = false,
                        IsActive = true,
                        CreatedBy = "System",
                        RowVersion = BitConverter.GetBytes(1)
                    },
                    new ProjectPhaseCatalogEntry
                    {
                        TenantId = resolvedTenantId,
                        OrganizationId = null,
                        Code = "planning",
                        NormalizedCode = "PLANNING",
                        NameEn = "Planning",
                        NameAr = "التخطيط",
                        DisplayOrder = 30,
                        IsDefault = false,
                        IsActive = true,
                        CreatedBy = "System",
                        RowVersion = BitConverter.GetBytes(1)
                    });
            }

            await db.SaveChangesAsync();

            var organizationAccess = new Mock<IOrganizationSettingsAccessService>();
            organizationAccess.Setup(service => service.HasAccessAsync(
                    It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid requestedOrganization, bool manage, CancellationToken _) =>
                    requestedOrganization == organizationId);

            var audit = new Mock<IAuditService>();
            var auditEntries = new List<AuditEntry>();
            audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
                .Callback<AuditEntry, CancellationToken>((entry, _) =>
                {
                    auditEntries.Add(entry);
                    db.AuditEntries.Add(entry);
                })
                .ReturnsAsync(Result.Success());

            audit.Setup(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());
            var authorization = new PolicyAuthorizationService();
            var services = new ServiceCollection();
            services.AddSingleton(db);
            services.AddPhaseGatePersistence();
            var provider = services.BuildServiceProvider();

            return new TestHost(connection, http, provider, db, resolvedTenantId, actorId,
                organizationId, organizationAccess, authorization, audit, auditEntries);
        }

        public async Task AddOrganizationAsync(Guid organizationId, Guid tenantId, string name)
        {
            Db.Organizations.Add(new OrganizationEntity
            {
                Id = organizationId,
                TenantId = tenantId,
                Name = name,
                IsActive = true,
                CreatedBy = ActorId.ToString()
            });
            await Db.SaveChangesAsync();
        }

        public Task<ProjectPhaseCatalogEntry?> FindTenantPhaseAsync() =>
            Db.ProjectPhaseCatalogEntries.IgnoreQueryFilters()
                .FirstOrDefaultAsync(phase => phase.TenantId == TenantId
                    && phase.OrganizationId == null
                    && phase.NormalizedCode == CanonicalNormalizedCode);

        public Task<List<ProjectPhaseCatalogEntry>> PhaseRowsAsync(Guid tenantId, Guid? organizationId) =>
            Db.ProjectPhaseCatalogEntries.IgnoreQueryFilters()
                .Where(phase => phase.TenantId == tenantId && phase.OrganizationId == organizationId)
                .OrderBy(phase => phase.Code)
                .ToListAsync();

        /// <summary>
        /// Seeds the legacy six-phase Tenant catalog exactly as the applied migration left it.
        /// Returns the created ids keyed by normalized code.
        /// </summary>
        public async Task<Dictionary<string, Guid>> SeedLegacyPhaseCatalogAsync(Guid tenantId)
        {
            var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var legacy in LegacyPhaseSeeds)
            {
                var phase = new ProjectPhaseCatalogEntry
                {
                    TenantId = tenantId,
                    OrganizationId = null,
                    Code = legacy.Code,
                    NormalizedCode = legacy.Code.ToUpperInvariant(),
                    NameEn = legacy.NameEn,
                    NameAr = legacy.NameAr,
                    DisplayOrder = legacy.Order,
                    IsDefault = legacy.Code == CanonicalCode,
                    IsActive = true,
                    IsContinuousActivity = legacy.Continuous,
                    CreatedBy = "System",
                    RowVersion = BitConverter.GetBytes(1)
                };
                Db.ProjectPhaseCatalogEntries.Add(phase);
                ids[phase.NormalizedCode] = phase.Id;
            }

            await Db.SaveChangesAsync();
            return ids;
        }

        /// <summary>
        /// Invokes the private seed helper so the phase-provisioning contract is verified
        /// without running the full identity/demo seed graph.
        /// </summary>
        public async Task RunLifecycleSeedAsync()
        {
            var method = typeof(SeedDataProvider).GetMethod(
                "EnsureProjectLifecycleCatalogAsync", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            await Assert.IsAssignableFrom<Task>(method!.Invoke(null, [Db]));
        }

        public ProjectLifecycleCatalogService CreateLifecycleService()
        {
            var scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>())
            {
                OrganizationIds = new HashSet<Guid> { OrganizationId },
                OrganizationReferences = [new ManagementEntityReference(OrganizationId, "Phase Protection Test Organization")]
            };
            return new ProjectLifecycleCatalogService(Db, _http, Audit.Object,
                NewManagementContextAccessor(ManagementPerspective.ProjectManager, scope));
        }

        private ManagementContextAccessor NewManagementContextAccessor(ManagementPerspective perspective, ManagementScope scope)
        {
            var management = new ManagementContextAccessor();
            management.Set(new ManagementContext(ActorId, TenantId, perspective, ManagementViewMode.Native,
                null, null, null, scope));
            return management;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
            Provider.Dispose();
        }

        private static ClaimsPrincipal Principal(Guid tenantId, Guid actorId) => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
            new Claim("tenantId", tenantId.ToString()),
            new Claim("permission", "Project.Create"),
            new Claim("permission", "ProjectLifecyclePhase.Edit"),
            new Claim("permission", "ProjectLifecyclePhase.Create"),
            new Claim("permission", "ProjectLifecyclePhase.ActivateDeactivate"),
            new Claim("permission", "ProjectLifecyclePhase.View"),
        ], "test"));

        internal sealed class PolicyAuthorizationService : IAuthorizationService
        {
            public void Deny(string permission) => DeniedPermissions.Add(permission);

            private HashSet<string> DeniedPermissions { get; } = new(StringComparer.Ordinal);

            public Task<AuthorizationResult> AuthorizeAsync(
                ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                Task.FromResult(AuthorizationResult.Failed());

            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            {
                var granted = user.HasClaim("permission", policyName);
                var denied = user.HasClaim("permission-deny", policyName) || DeniedPermissions.Contains(policyName);
                return Task.FromResult(granted && !denied
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());
            }
        }
    }
}
