using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class OrganizationPhaseGateConfigurationTests
{
    private static readonly string SourcePhase = "test-source";
    private static readonly string TargetPhase = "test-target";

    [Fact]
    public async Task Save_appends_immutable_versions_with_exact_template_version_and_audit()
    {
        await using var host = await TestHost.CreateAsync();
        var template = await host.AddTemplateAsync(host.OrganizationId, host.TenantId, "Initiation Brief", active: true);
        var secondTemplate = await host.AddTemplateAsync(host.OrganizationId, host.TenantId, "Risk Register", active: true);

        var first = await host.SaveAsync(
            [new SavePhaseGateRequirementRequest
            {
                TemplateId = template.TemplateId,
                TemplateVersionId = template.VersionId,
                RequirementType = PhaseGateRequirementType.Mandatory
            }]);

        Assert.Equal(PhaseGateConfigurationOperationCode.Success, first.Code);
        Assert.Equal(1, first.Configuration!.ConfigurationVersion);
        Assert.NotNull(first.Configuration.ConfigurationVersionId);
        Assert.NotNull(first.Configuration.VersionToken);
        Assert.Equal(PhaseGateRequirementType.Mandatory, Assert.Single(first.Configuration.Requirements).RequirementType);

        var second = await host.SaveAsync(
            [
                new SavePhaseGateRequirementRequest
                {
                    TemplateId = template.TemplateId,
                    TemplateVersionId = template.VersionId,
                    RequirementType = PhaseGateRequirementType.Optional
                },
                new SavePhaseGateRequirementRequest
                {
                    TemplateId = secondTemplate.TemplateId,
                    TemplateVersionId = secondTemplate.VersionId,
                    RequirementType = PhaseGateRequirementType.Mandatory
                }
            ], first.Configuration.VersionToken);

        Assert.Equal(PhaseGateConfigurationOperationCode.Success, second.Code);
        Assert.Equal(first.Configuration.ConfigurationId, second.Configuration!.ConfigurationId);
        Assert.Equal(2, second.Configuration.ConfigurationVersion);
        Assert.Contains(second.Configuration.Requirements, item =>
            item.TemplateId == template.TemplateId
            && item.TemplateVersionId == template.VersionId
            && item.RequirementType == PhaseGateRequirementType.Optional);

        var history = await host.Service.GetHistoryAsync(host.OrganizationId, host.SourcePhaseId, host.TargetPhaseId);
        Assert.Equal(PhaseGateConfigurationOperationCode.Success, history.Code);
        var versions = history.History!.Versions.OrderBy(item => item.ConfigurationVersion).ToArray();
        Assert.Equal(new[] { 1, 2 }, versions.Select(item => item.ConfigurationVersion));
        var original = Assert.Single(versions[0].Requirements);
        Assert.Equal(template.TemplateId, original.TemplateId);
        Assert.Equal(template.VersionId, original.TemplateVersionId);
        Assert.Equal("Initiation Brief", original.TemplateName);
        Assert.Equal("1", original.Version);
        Assert.Equal(PhaseGateRequirementType.Mandatory, original.RequirementType);

        Assert.Equal(2, host.AuditEntries.Count);
        Assert.Equal(2, await host.Db.AuditEntries.IgnoreQueryFilters().CountAsync());
        Assert.All(host.AuditEntries, entry =>
        {
            Assert.Equal("OrganizationPhaseGateConfiguration", entry.EntityType);
            Assert.Equal("VersionCreated", entry.Action);
            Assert.Equal(host.ActorId, entry.UserId);
        });
    }

    [Fact]
    public async Task Phase_gate_pairs_do_not_require_catalog_transition_rules()
    {
        await using var host = await TestHost.CreateAsync();

        var phasePairs = await host.Service.GetPhasePairsAsync(host.OrganizationId);
        var unsupported = await host.Service.GetConfigurationAsync(
            host.OrganizationId, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(PhaseGateConfigurationOperationCode.Success, phasePairs.Code);
        var pair = Assert.Single(phasePairs.PhasePairs);
        Assert.Equal(SourcePhase, pair.SourcePhase);
        Assert.Equal(TargetPhase, pair.TargetPhase);
        Assert.Equal(host.SourcePhaseId, pair.SourcePhaseId);
        Assert.Equal(host.TargetPhaseId, pair.TargetPhaseId);
        Assert.Equal(PhaseGateConfigurationOperationCode.ValidationFailed, unsupported.Code);
        Assert.Empty(await host.Db.OrganizationPhaseGateConfigurations.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Duplicate_template_in_one_transition_is_rejected_without_persistence()
    {
        await using var host = await TestHost.CreateAsync();
        var template = await host.AddTemplateAsync(host.OrganizationId, host.TenantId, "Duplicate", active: true);

        var result = await host.SaveAsync(
            [
                new SavePhaseGateRequirementRequest
                {
                    TemplateId = template.TemplateId,
                    TemplateVersionId = template.VersionId,
                    RequirementType = PhaseGateRequirementType.Mandatory
                },
                new SavePhaseGateRequirementRequest
                {
                    TemplateId = template.TemplateId,
                    TemplateVersionId = Guid.NewGuid(),
                    RequirementType = PhaseGateRequirementType.Optional
                }
            ]);

        Assert.Equal(PhaseGateConfigurationOperationCode.ValidationFailed, result.Code);
        Assert.Contains("only once", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(host.AuditEntries);
        Assert.Empty(await host.Db.OrganizationPhaseGateConfigurations.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task New_configuration_rejects_archived_or_foreign_organization_template_versions()
    {
        await using var host = await TestHost.CreateAsync();
        var archived = await host.AddTemplateAsync(host.OrganizationId, host.TenantId, "Archived", active: false);
        var otherOrganizationId = Guid.NewGuid();
        await host.AddOrganizationAsync(otherOrganizationId, host.TenantId, "Another organization");
        var otherOrganizationTemplate = await host.AddTemplateAsync(
            otherOrganizationId, host.TenantId, "Other organization template", active: true);
        var otherTenantId = Guid.NewGuid();
        var foreignOrganizationId = Guid.NewGuid();
        host.SetTenant(otherTenantId);
        await host.AddOrganizationAsync(foreignOrganizationId, otherTenantId, "Foreign organization");
        var foreignTenantTemplate = await host.AddTemplateAsync(
            foreignOrganizationId, otherTenantId, "Foreign tenant template", active: true);
        host.SetTenant(host.TenantId);

        var archivedResult = await host.SaveAsync(
            [new SavePhaseGateRequirementRequest
            {
                TemplateId = archived.TemplateId,
                TemplateVersionId = archived.VersionId,
                RequirementType = PhaseGateRequirementType.Mandatory
            }]);
        var foreignOrganizationResult = await host.SaveAsync(
            [new SavePhaseGateRequirementRequest
            {
                TemplateId = otherOrganizationTemplate.TemplateId,
                TemplateVersionId = otherOrganizationTemplate.VersionId,
                RequirementType = PhaseGateRequirementType.Optional
            }]);
        var foreignTenantResult = await host.SaveAsync(
            [new SavePhaseGateRequirementRequest
            {
                TemplateId = foreignTenantTemplate.TemplateId,
                TemplateVersionId = foreignTenantTemplate.VersionId,
                RequirementType = PhaseGateRequirementType.Optional
            }]);

        Assert.Equal(PhaseGateConfigurationOperationCode.ValidationFailed, archivedResult.Code);
        Assert.Equal(PhaseGateConfigurationOperationCode.ValidationFailed, foreignOrganizationResult.Code);
        Assert.Equal(PhaseGateConfigurationOperationCode.ValidationFailed, foreignTenantResult.Code);
        Assert.Empty(await host.Db.OrganizationPhaseGateConfigurations.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Existing_pinned_template_reference_remains_readable_after_template_archive()
    {
        await using var host = await TestHost.CreateAsync();
        var template = await host.AddTemplateAsync(host.OrganizationId, host.TenantId, "Pinned template", active: true);
        var first = await host.SaveAsync(
            [new SavePhaseGateRequirementRequest
            {
                TemplateId = template.TemplateId,
                TemplateVersionId = template.VersionId,
                RequirementType = PhaseGateRequirementType.Mandatory
            }]);
        Assert.Equal(PhaseGateConfigurationOperationCode.Success, first.Code);

        var document = await host.Db.Documents.IgnoreQueryFilters().SingleAsync(item => item.Id == template.TemplateId);
        document.IsArchived = true;
        document.Status = "Archived";
        document.Name = "Renamed after archive";
        await host.Db.SaveChangesAsync();

        var next = await host.SaveAsync(
            [new SavePhaseGateRequirementRequest
            {
                TemplateId = template.TemplateId,
                TemplateVersionId = template.VersionId,
                RequirementType = PhaseGateRequirementType.Optional
            }], first.Configuration!.VersionToken);

        Assert.Equal(PhaseGateConfigurationOperationCode.Success, next.Code);
        var history = await host.Service.GetHistoryAsync(host.OrganizationId, host.SourcePhaseId, host.TargetPhaseId);
        var versions = history.History!.Versions.OrderBy(item => item.ConfigurationVersion).ToArray();
        Assert.Equal("Pinned template", Assert.Single(versions[0].Requirements).TemplateName);
        Assert.Equal(PhaseGateRequirementType.Mandatory, versions[0].Requirements[0].RequirementType);
        Assert.Equal("Pinned template", Assert.Single(versions[1].Requirements).TemplateName);
        Assert.Equal(PhaseGateRequirementType.Optional, versions[1].Requirements[0].RequirementType);
    }

    [Fact]
    public async Task Foreign_organization_scope_is_denied_before_configuration_is_read_or_changed()
    {
        await using var host = await TestHost.CreateAsync();
        var outsideOrganization = Guid.NewGuid();
        host.OrganizationAccess.Setup(service => service.HasAccessAsync(
                outsideOrganization, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var read = await host.Service.GetConfigurationAsync(outsideOrganization, host.SourcePhaseId, host.TargetPhaseId);
        var save = await host.Service.SaveConfigurationAsync(
            outsideOrganization, host.SourcePhaseId, host.TargetPhaseId,
            new SavePhaseGateConfigurationRequest());

        Assert.Equal(PhaseGateConfigurationOperationCode.AccessDenied, read.Code);
        Assert.Null(read.Configuration);
        Assert.Equal(PhaseGateConfigurationOperationCode.AccessDenied, save.Code);
        Assert.Empty(await host.Db.OrganizationPhaseGateConfigurations.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Organization_read_grant_does_not_allow_configuration_changes()
    {
        await using var host = await TestHost.CreateAsync();
        host.OrganizationAccess.Setup(service => service.HasAccessAsync(
                host.OrganizationId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var read = await host.Service.GetConfigurationAsync(host.OrganizationId, host.SourcePhaseId, host.TargetPhaseId);
        var save = await host.SaveAsync(Array.Empty<SavePhaseGateRequirementRequest>());

        Assert.Equal(PhaseGateConfigurationOperationCode.Success, read.Code);
        Assert.Equal(PhaseGateConfigurationOperationCode.AccessDenied, save.Code);
        Assert.Empty(await host.Db.OrganizationPhaseGateConfigurations.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Read_only_view_as_can_read_phase_gate_configuration_but_cannot_save_it()
    {
        await using var host = await TestHost.CreateAsync();
        host.Management.Set(host.Management.Current! with { ViewMode = ManagementViewMode.ReadOnlyViewAs });

        var read = await host.Service.GetConfigurationAsync(host.OrganizationId, host.SourcePhaseId, host.TargetPhaseId);
        var save = await host.SaveAsync(Array.Empty<SavePhaseGateRequirementRequest>());

        Assert.Equal(PhaseGateConfigurationOperationCode.Success, read.Code);
        Assert.Equal(PhaseGateConfigurationOperationCode.AccessDenied, save.Code);
        Assert.Empty(host.AuditEntries);
        Assert.Empty(await host.Db.OrganizationPhaseGateConfigurations.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await host.Db.OrganizationPhaseGateConfigurationVersions.IgnoreQueryFilters().ToListAsync());
    }

    [Theory]
    [InlineData("Settings.ManageTenantConfiguration")]
    [InlineData("Document.View")]
    public async Task Active_deny_on_required_permission_blocks_configuration_write(string deniedPermission)
    {
        await using var host = await TestHost.CreateAsync();
        var template = await host.AddTemplateAsync(host.OrganizationId, host.TenantId, "Permission check", active: true);
        host.Authorization.Deny(deniedPermission);

        var result = await host.SaveAsync(
            [new SavePhaseGateRequirementRequest
            {
                TemplateId = template.TemplateId,
                TemplateVersionId = template.VersionId,
                RequirementType = PhaseGateRequirementType.Mandatory
            }]);

        Assert.Equal(PhaseGateConfigurationOperationCode.AccessDenied, result.Code);
        Assert.Empty(host.AuditEntries);
        Assert.Empty(await host.Db.OrganizationPhaseGateConfigurations.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Stale_configuration_version_token_is_rejected_without_appending_history()
    {
        await using var host = await TestHost.CreateAsync();
        var template = await host.AddTemplateAsync(host.OrganizationId, host.TenantId, "Concurrency", active: true);
        var first = await host.SaveAsync(
            [new SavePhaseGateRequirementRequest
            {
                TemplateId = template.TemplateId,
                TemplateVersionId = template.VersionId,
                RequirementType = PhaseGateRequirementType.Mandatory
            }]);
        Assert.Equal(PhaseGateConfigurationOperationCode.Success, first.Code);
        var staleToken = DifferentToken(first.Configuration!.VersionToken!);

        var conflict = await host.SaveAsync(
            [new SavePhaseGateRequirementRequest
            {
                TemplateId = template.TemplateId,
                TemplateVersionId = template.VersionId,
                RequirementType = PhaseGateRequirementType.Optional
            }], staleToken);

        Assert.Equal(PhaseGateConfigurationOperationCode.ConcurrencyConflict, conflict.Code);
        var history = await host.Service.GetHistoryAsync(host.OrganizationId, host.SourcePhaseId, host.TargetPhaseId);
        Assert.Single(history.History!.Versions);
        Assert.Single(host.AuditEntries);
    }

    [Fact]
    public async Task Failed_audit_rolls_back_configuration_and_version_rows()
    {
        await using var host = await TestHost.CreateAsync();
        var template = await host.AddTemplateAsync(host.OrganizationId, host.TenantId, "Audit rollback", active: true);
        host.Audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(new Error("AUDIT_FAILED", "Audit storage unavailable.")));

        var result = await host.SaveAsync(
            [new SavePhaseGateRequirementRequest
            {
                TemplateId = template.TemplateId,
                TemplateVersionId = template.VersionId,
                RequirementType = PhaseGateRequirementType.Mandatory
            }]);

        Assert.Equal(PhaseGateConfigurationOperationCode.AuditFailed, result.Code);
        Assert.Empty(await host.Db.OrganizationPhaseGateConfigurations.IgnoreQueryFilters().AsNoTracking().ToListAsync());
        Assert.Empty(await host.Db.OrganizationPhaseGateConfigurationVersions.IgnoreQueryFilters().AsNoTracking().ToListAsync());
        Assert.Empty(await host.Db.OrganizationPhaseGateRequirements.IgnoreQueryFilters().AsNoTracking().ToListAsync());
    }

    private static string DifferentToken(string token)
    {
        var bytes = Convert.FromBase64String(token);
        Assert.Equal(8, bytes.Length);
        bytes[0] ^= 0x01;
        return Convert.ToBase64String(bytes);
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly HttpContextAccessor _httpContextAccessor;

        private TestHost(
            SqliteConnection connection,
            ServiceProvider provider,
            HttpContextAccessor httpContextAccessor,
            AppDbContext db,
            Guid tenantId,
            Guid actorId,
            Guid organizationId,
            Mock<IOrganizationSettingsAccessService> organizationAccess,
            PolicyAuthorizationService authorization,
            Mock<IAuditService> audit,
            List<AuditEntry> auditEntries,
            ManagementContextAccessor management)
        {
            _connection = connection;
            _provider = provider;
            _httpContextAccessor = httpContextAccessor;
            Db = db;
            TenantId = tenantId;
            ActorId = actorId;
            OrganizationId = organizationId;
            OrganizationAccess = organizationAccess;
            Authorization = authorization;
            Audit = audit;
            AuditEntries = auditEntries;
            Management = management;
            Service = new PhaseGateConfigurationService(
                Db,
                _httpContextAccessor,
                Authorization,
                OrganizationAccess.Object,
                _provider.GetRequiredService<IProjectPhaseGatePhasePairCatalog>(),
                _provider.GetRequiredService<IApprovedTemplateCatalog>(),
                Audit.Object,
                Management);
        }

        public AppDbContext Db { get; }
        public Guid TenantId { get; }
        public Guid ActorId { get; }
        public Guid OrganizationId { get; }
        public Guid SourcePhaseId { get; private set; }
        public Guid TargetPhaseId { get; private set; }
        public Mock<IOrganizationSettingsAccessService> OrganizationAccess { get; }
        public PolicyAuthorizationService Authorization { get; }
        public Mock<IAuditService> Audit { get; }
        public List<AuditEntry> AuditEntries { get; }
        public ManagementContextAccessor Management { get; }
        public IPhaseGateConfigurationService Service { get; }

        public static async Task<TestHost> CreateAsync()
        {
            var tenantId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var organizationId = Guid.NewGuid();
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var httpContextAccessor = new HttpContextAccessor();
            httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                User = Principal(tenantId, actorId)
            };
            var management = new ManagementContextAccessor();
            management.Set(new ManagementContext(
                actorId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
                null, null, null, ManagementScope.Empty));
            AppDbContext.ConfigureTenantResolution(httpContextAccessor);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Organizations.Add(new Organization
            {
                Id = organizationId,
                TenantId = tenantId,
                Name = "Phase Gate Test Organization",
                IsActive = true,
                CreatedBy = actorId.ToString()
            });
            var sourcePhaseId = Guid.NewGuid();
            var targetPhaseId = Guid.NewGuid();
            db.ProjectPhaseCatalogEntries.AddRange(
                new ProjectPhaseCatalogEntry
                {
                    Id = sourcePhaseId,
                    TenantId = tenantId,
                    OrganizationId = organizationId,
                    Code = SourcePhase,
                    NormalizedCode = SourcePhase.ToUpperInvariant(),
                    NameEn = "Source phase",
                    NameAr = "مرحلة البداية",
                    DisplayOrder = 10,
                    IsActive = true,
                    CreatedBy = actorId.ToString()
                },
                new ProjectPhaseCatalogEntry
                {
                    Id = targetPhaseId,
                    TenantId = tenantId,
                    OrganizationId = organizationId,
                    Code = TargetPhase,
                    NormalizedCode = TargetPhase.ToUpperInvariant(),
                    NameEn = "Target phase",
                    NameAr = "المرحلة المستهدفة",
                    DisplayOrder = 20,
                    IsActive = true,
                    CreatedBy = actorId.ToString()
                });
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
            var authorization = new PolicyAuthorizationService();
            var services = new ServiceCollection();
            services.AddSingleton(db);
            services.AddPhaseGatePersistence();
            var provider = services.BuildServiceProvider();

            var host = new TestHost(connection, provider, httpContextAccessor, db, tenantId, actorId,
                organizationId, organizationAccess, authorization, audit, auditEntries, management);
            host.SourcePhaseId = sourcePhaseId;
            host.TargetPhaseId = targetPhaseId;
            return host;
        }

        public async Task AddOrganizationAsync(Guid organizationId, Guid tenantId, string name)
        {
            Db.Organizations.Add(new Organization
            {
                Id = organizationId,
                TenantId = tenantId,
                Name = name,
                IsActive = true,
                CreatedBy = ActorId.ToString()
            });
            await Db.SaveChangesAsync();
        }

        public async Task<(Guid TemplateId, Guid VersionId)> AddTemplateAsync(
            Guid organizationId, Guid tenantId, string name, bool active)
        {
            var templateId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var document = new Document
            {
                Id = templateId,
                TenantId = tenantId,
                OrganizationId = organizationId,
                Name = name,
                Status = active ? "Active" : "Archived",
                Version = 1,
                IsTemplate = true,
                IsArchived = !active,
                CreatedBy = ActorId.ToString()
            };
            document.Versions.Add(new DocumentVersion
            {
                Id = versionId,
                TenantId = tenantId,
                DocumentId = templateId,
                VersionNumber = 1,
                FileName = $"{name}.docx",
                FileSize = 100,
                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                UploadedBy = ActorId,
                UploadedAt = DateTime.UtcNow,
                IsCurrent = true,
                CreatedBy = ActorId.ToString()
            });
            Db.Documents.Add(document);
            await Db.SaveChangesAsync();
            return (templateId, versionId);
        }

        public async Task<PhaseGateConfigurationOperationResult> SaveAsync(
            IReadOnlyList<SavePhaseGateRequirementRequest> requirements,
            string? expectedVersionToken = null) =>
            await Service.SaveConfigurationAsync(OrganizationId, SourcePhaseId, TargetPhaseId,
                new SavePhaseGateConfigurationRequest
                {
                    ExpectedVersionToken = expectedVersionToken,
                    Requirements = requirements
                });

        public void SetTenant(Guid tenantId) => _httpContextAccessor.HttpContext!.User = Principal(tenantId, ActorId);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static ClaimsPrincipal Principal(Guid tenantId, Guid actorId) => new(new ClaimsIdentity(
        [
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
            new Claim("permission", "Settings.View"),
            new Claim("permission", "Settings.ManageTenantConfiguration"),
            new Claim("permission", "Document.View")
        ], authenticationType: "PhaseGateTest"));
    }

    private sealed class PolicyAuthorizationService : IAuthorizationService
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
