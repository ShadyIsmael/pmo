using System.Security.Claims;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.Modules.Organization.Api;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class OrganizationAdminScopedConfigurationAuthorizationTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _foreignTenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _ordinaryActorId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _readOnlyOrganizationId = Guid.NewGuid();
    private readonly Guid _foreignOrganizationId = Guid.NewGuid();
    private readonly HttpContextAccessor _httpContextAccessor;
    private AppDbContext _context = null!;

    public OrganizationAdminScopedConfigurationAuthorizationTests()
    {
        _httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = Principal(_tenantId, _actorId) }
        };
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _context = new SqliteTestAppDbContext(options);
        AppDbContext.ConfigureTenantResolution(_httpContextAccessor);
        await _context.Database.EnsureCreatedAsync();

        using (var foreignTenantScope = TrustedTenantExecutionContext.Enter(_foreignTenantId))
        {
            _context.Tenants.Add(
                new NEXUS.Modules.Organization.Domain.Tenant { Id = _foreignTenantId, Code = $"ORG-{_foreignTenantId:N}", DisplayName = "Foreign test", CreatedBy = "test" });
            _context.Organizations.Add(
                NewOrganization(_foreignOrganizationId, _foreignTenantId, "Foreign organization", active: true));
            await _context.SaveChangesAsync();
        }

        using var tenantScope = TrustedTenantExecutionContext.Enter(_tenantId);
        _context.Tenants.Add(
            new NEXUS.Modules.Organization.Domain.Tenant { Id = _tenantId, Code = $"ORG-{_tenantId:N}", DisplayName = "Organization test", CreatedBy = "test" });

        var organizationAdmin = new Role
        {
            TenantId = _tenantId,
            Name = "Organization Administrator",
            Category = "Organization",
            IsSystem = true,
            CreatedBy = "test"
        };
        organizationAdmin.AssignSystemRoleKey(Role.OrganizationAdminSystemRoleKey, _tenantId);

        var organizationConfigurationView = NewPermission(_tenantId, "OrganizationConfiguration", "View");
        var organizationConfigurationManage = NewPermission(_tenantId, "OrganizationConfiguration", "Manage");
        organizationAdmin.Permissions.Add(organizationConfigurationView);
        organizationAdmin.Permissions.Add(organizationConfigurationManage);

        var fakeOrganizationAdmin = new Role
        {
            TenantId = _tenantId,
            Name = Role.OrganizationAdminRoleKey,
            Category = "Organization",
            IsSystem = false,
            CreatedBy = "test"
        };
        fakeOrganizationAdmin.AssignRoleKey("custom.organization-admin-name", _tenantId);

        var organizationAdminUser = NewUser(_actorId, _tenantId, "Scoped", "Organization Admin");
        organizationAdminUser.UserRoles.Add(new UserRole
        {
            TenantId = _tenantId,
            User = organizationAdminUser,
            Role = organizationAdmin,
            CreatedBy = "test"
        });
        var ordinaryUser = NewUser(_ordinaryActorId, _tenantId, "Ordinary", "User");
        ordinaryUser.UserRoles.Add(new UserRole
        {
            TenantId = _tenantId,
            User = ordinaryUser,
            Role = fakeOrganizationAdmin,
            CreatedBy = "test"
        });

        _context.Users.AddRange(organizationAdminUser, ordinaryUser);
        _context.Roles.AddRange(organizationAdmin, fakeOrganizationAdmin);
        _context.Permissions.AddRange(organizationConfigurationView, organizationConfigurationManage);
        _context.Organizations.AddRange(
            NewOrganization(_organizationId, _tenantId, "Assigned organization", active: true),
            NewOrganization(_readOnlyOrganizationId, _tenantId, "Read-only organization", active: true));
        _context.Set<OrganizationSettingsGrant>().AddRange(
            new OrganizationSettingsGrant
            {
                TenantId = _tenantId,
                OrganizationId = _organizationId,
                UserId = _actorId,
                CanRead = true,
                CanManage = true,
                CreatedBy = "test"
            },
            new OrganizationSettingsGrant
            {
                TenantId = _tenantId,
                OrganizationId = _readOnlyOrganizationId,
                UserId = _actorId,
                CanRead = true,
                CanManage = false,
                CreatedBy = "test"
            },
            new OrganizationSettingsGrant
            {
                TenantId = _tenantId,
                OrganizationId = _organizationId,
                UserId = _ordinaryActorId,
                CanRead = true,
                CanManage = true,
                CreatedBy = "test"
            });
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Organization_admin_scope_requires_canonical_identity_and_active_same_tenant_grant()
    {
        var service = CreateOrganizationAccessService();

        Assert.True(await service.HasScopedOrganizationAdminAuthorityAsync(_organizationId, manage: false));
        Assert.True(await service.HasScopedOrganizationAdminAuthorityAsync(_organizationId, manage: true));
        Assert.True(await service.HasScopedOrganizationAdminAuthorityAsync(_readOnlyOrganizationId, manage: false));
        Assert.False(await service.HasScopedOrganizationAdminAuthorityAsync(_readOnlyOrganizationId, manage: true));
        Assert.False(await service.HasScopedOrganizationAdminAuthorityAsync(Guid.NewGuid(), manage: true));
        Assert.False(await service.HasScopedOrganizationAdminAuthorityAsync(_foreignOrganizationId, manage: true));

        _httpContextAccessor.HttpContext!.User = Principal(_tenantId, _ordinaryActorId);
        Assert.False(await service.HasScopedOrganizationAdminAuthorityAsync(_organizationId, manage: true));
    }

    [Fact]
    public async Task Organization_profiles_revalidate_role_membership_and_active_denies_for_read_and_manage_flags()
    {
        var access = CreateOrganizationAccessService();
        var audit = new Mock<IAuditService>();
        var organizations = new OrganizationService(_context, _httpContextAccessor, audit.Object, access);

        var initialProfiles = await organizations.GetProfilesAsync();
        Assert.Contains(initialProfiles, profile => profile.Id == _organizationId && profile.CanManageSettings);
        Assert.Contains(initialProfiles, profile => profile.Id == _readOnlyOrganizationId && !profile.CanManageSettings);
        Assert.Contains(_organizationId, await access.GetManageableOrganizationIdsAsync());

        var organizationAdminRole = await _context.Roles.SingleAsync(role =>
            role.SystemRoleKey == Role.OrganizationAdminSystemRoleKey);
        var organizationAdminAssignment = await _context.UserRoles.SingleAsync(assignment =>
            assignment.UserId == _actorId && assignment.RoleId == organizationAdminRole.Id);
        organizationAdminAssignment.IsDeleted = true;
        await _context.SaveChangesAsync();

        Assert.Empty(await access.GetReadableOrganizationIdsAsync());
        Assert.Empty(await access.GetManageableOrganizationIdsAsync());
        Assert.Empty(await organizations.GetProfilesAsync());

        organizationAdminAssignment.IsDeleted = false;
        await _context.SaveChangesAsync();
        var viewPermission = await _context.Permissions.SingleAsync(permission =>
            permission.Resource == "OrganizationConfiguration" && permission.Action == "View");
        var viewDeny = new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = _actorId,
            PermissionId = viewPermission.Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        };
        _context.UserPermissionOverrides.Add(viewDeny);
        await _context.SaveChangesAsync();

        Assert.Empty(await access.GetReadableOrganizationIdsAsync());
        Assert.Empty(await access.GetManageableOrganizationIdsAsync());
        Assert.Empty(await organizations.GetProfilesAsync());

        viewDeny.IsDeleted = true;
        var managePermission = await _context.Permissions.SingleAsync(permission =>
            permission.Resource == "OrganizationConfiguration" && permission.Action == "Manage");
        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = _actorId,
            PermissionId = managePermission.Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();

        var viewOnlyProfiles = await organizations.GetProfilesAsync();
        Assert.Contains(viewOnlyProfiles, profile => profile.Id == _organizationId && !profile.CanManageSettings);
        Assert.DoesNotContain(_organizationId, await access.GetManageableOrganizationIdsAsync());
        Assert.False(await access.HasScopedOrganizationAdminAuthorityAsync(_organizationId, manage: true));
    }

    [Fact]
    public async Task Organization_profile_update_is_denied_in_effective_read_only_management_context()
    {
        var access = new Mock<IOrganizationSettingsAccessService>();
        access.Setup(service => service.HasAccessAsync(
                _organizationId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new OrganizationService(_context, _httpContextAccessor,
            new Mock<IAuditService>().Object, access.Object, ReadOnlyManagementContext());

        var result = await service.UpdateProfileAsync(_organizationId,
            new UpdateOrganizationProfileRequest { Name = "Must not be saved" });

        Assert.Equal(OrganizationProfileUpdateCode.AccessDenied, result.Code);
        access.Verify(item => item.HasAccessAsync(
            It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("Assigned organization", await _context.Organizations
            .Where(organization => organization.Id == _organizationId)
            .Select(organization => organization.Name)
            .SingleAsync());
    }

    [Fact]
    public async Task Organization_profile_update_requires_the_scoped_manage_branch()
    {
        var access = new Mock<IOrganizationSettingsAccessService>();
        access.Setup(service => service.HasScopedOrganizationAdminAuthorityAsync(
                It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, bool _, CancellationToken _) => id == _organizationId);
        var organizationService = new Mock<IOrganizationService>();
        var profile = new OrganizationProfileDto(_organizationId, "Updated", null, null, null, null, null, null,
            null, null, null, null, CanManageSettings: true);
        organizationService.Setup(service => service.UpdateProfileAsync(
                _organizationId, It.IsAny<UpdateOrganizationProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationProfileUpdateResult(OrganizationProfileUpdateCode.Updated, profile));
        var authorization = CreateAuthorizationService("OrganizationConfiguration.Manage");

        var controller = CreateOrganizationController(organizationService.Object, access.Object, authorization.Object,
            Principal(_tenantId, _actorId));
        var allowed = await controller.UpdateGeneralSettings(_organizationId,
            new UpdateOrganizationProfileRequest { Name = "Updated" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(allowed);
        access.Verify(service => service.HasScopedOrganizationAdminAuthorityAsync(
            _organizationId, true, It.IsAny<CancellationToken>()), Times.Once);
        organizationService.Verify(service => service.UpdateProfileAsync(
            _organizationId, It.IsAny<UpdateOrganizationProfileRequest>(), It.IsAny<CancellationToken>()), Times.Once);

        var unrelatedOrganization = Guid.NewGuid();
        var denied = await controller.UpdateGeneralSettings(unrelatedOrganization,
            new UpdateOrganizationProfileRequest { Name = "Out of scope" }, CancellationToken.None);
        Assert.IsType<ForbidResult>(denied);
        organizationService.Verify(service => service.UpdateProfileAsync(
            unrelatedOrganization, It.IsAny<UpdateOrganizationProfileRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Organization_configuration_permission_does_not_authorize_tenant_or_platform_settings()
    {
        var organizationAccess = new Mock<IOrganizationSettingsAccessService>();
        organizationAccess.Setup(service => service.HasScopedOrganizationAdminAuthorityAsync(
                _organizationId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        organizationAccess.Setup(service => service.HasScopedOrganizationAdminAuthorityAsync(
                _organizationId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var repository = new TestSettingsRepository();
        var settings = CreateConfigurationSettingsService(
            ["OrganizationConfiguration.View", "OrganizationConfiguration.Manage"],
            organizationAccess.Object, repository, new SettingDefinitionCatalogForTests(OrganizationSetting()));

        var read = await settings.GetOrganizationAsync(_organizationId);
        Assert.True(read.Succeeded);
        var organizationUpdate = await settings.UpdateOrganizationAsync(_organizationId, TestSettingKey,
            new UpdateSettingRequest { Value = JsonSerializer.SerializeToElement("organization value") });
        Assert.True(organizationUpdate.Succeeded, organizationUpdate.Error);
        Assert.Equal(SettingScope.Organization, repository.LastWriteIdentity?.Scope);
        Assert.Equal(_organizationId, repository.LastWriteIdentity?.OwnerId);

        var tenantUpdate = await settings.UpdateTenantAsync(TestSettingKey,
            new UpdateSettingRequest { Value = JsonSerializer.SerializeToElement("tenant value") });
        Assert.Equal(SettingsOperationCode.AccessDenied, tenantUpdate.Code);
        Assert.Equal(1, repository.WriteCount);
    }

    [Fact]
    public async Task Organization_admin_identity_and_scope_grant_do_not_bypass_missing_configuration_permissions()
    {
        var organizationAccess = CreateOrganizationAccessService();
        Assert.True(await organizationAccess.HasScopedOrganizationAdminAuthorityAsync(_organizationId, manage: false));
        Assert.True(await organizationAccess.HasScopedOrganizationAdminAuthorityAsync(_organizationId, manage: true));

        var readRepository = new TestSettingsRepository();
        var manageOnly = CreateConfigurationSettingsService(
            ["OrganizationConfiguration.Manage"], organizationAccess, readRepository,
            new SettingDefinitionCatalogForTests(OrganizationSetting()));
        var read = await manageOnly.GetOrganizationAsync(_organizationId);
        Assert.Equal(SettingsOperationCode.AccessDenied, read.Code);
        Assert.Equal(0, readRepository.WriteCount);

        var writeRepository = new TestSettingsRepository();
        var viewOnly = CreateConfigurationSettingsService(
            ["OrganizationConfiguration.View"], organizationAccess, writeRepository,
            new SettingDefinitionCatalogForTests(OrganizationSetting()));
        var update = await viewOnly.UpdateOrganizationAsync(_organizationId, TestSettingKey,
            new UpdateSettingRequest { Value = JsonSerializer.SerializeToElement("unauthorized override") });
        Assert.Equal(SettingsOperationCode.AccessDenied, update.Code);
        Assert.Equal(0, writeRepository.WriteCount);
    }

    [Fact]
    public void Tenant_and_platform_configuration_writes_keep_separate_authorization_policies()
    {
        var tenantAction = typeof(SettingsController).GetMethod(nameof(SettingsController.UpdateTenant));
        var platformAction = typeof(SettingsController).GetMethod(nameof(SettingsController.UpdatePlatform));
        Assert.NotNull(tenantAction);
        Assert.NotNull(platformAction);
        Assert.Equal("Settings.ManageTenantConfiguration",
            Assert.Single(tenantAction!.GetCustomAttributes<AuthorizeAttribute>(inherit: false)).Policy);
        Assert.Equal("PlatformSettings.Write",
            Assert.Single(platformAction!.GetCustomAttributes<AuthorizeAttribute>(inherit: false)).Policy);
    }

    [Fact]
    public async Task Inherit_only_organization_setting_is_denied_without_persistence()
    {
        var organizationAccess = new Mock<IOrganizationSettingsAccessService>();
        organizationAccess.Setup(service => service.HasScopedOrganizationAdminAuthorityAsync(
                It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var repository = new TestSettingsRepository();
        var inheritedOnly = new SettingDefinition(
            "test.inherited-only",
            SettingValueType.String,
            new HashSet<SettingScope> { SettingScope.Platform, SettingScope.Tenant, SettingScope.Organization },
            JsonSerializer.SerializeToElement("inherited"),
            "Must inherit from the parent scope.",
            SettingOverrideBehavior.InheritOnly,
            SettingSecurityClassification.Standard,
            _ => null);
        var settings = CreateConfigurationSettingsService(
            ["OrganizationConfiguration.Manage"], organizationAccess.Object, repository,
            new SettingDefinitionCatalogForTests(inheritedOnly));

        var result = await settings.UpdateOrganizationAsync(_organizationId, "test.inherited-only",
            new UpdateSettingRequest { Value = JsonSerializer.SerializeToElement("override") });

        Assert.Equal(SettingsOperationCode.ScopeNotAllowed, result.Code);
        Assert.Equal(0, repository.WriteCount);
        organizationAccess.Verify(service => service.HasScopedOrganizationAdminAuthorityAsync(
            It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Organization_configuration_update_and_reset_are_denied_in_effective_read_only_context()
    {
        var organizationAccess = new Mock<IOrganizationSettingsAccessService>();
        organizationAccess.Setup(service => service.HasScopedOrganizationAdminAuthorityAsync(
                _organizationId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var repository = new TestSettingsRepository();
        var settings = CreateConfigurationSettingsService(
            ["OrganizationConfiguration.View", "OrganizationConfiguration.Manage"],
            organizationAccess.Object, repository,
            new SettingDefinitionCatalogForTests(OrganizationSetting()),
            ReadOnlyManagementContext());

        var update = await settings.UpdateOrganizationAsync(_organizationId, TestSettingKey,
            new UpdateSettingRequest { Value = JsonSerializer.SerializeToElement("blocked") });
        var reset = await settings.ResetOrganizationAsync(_organizationId, TestSettingKey,
            new ResetSettingRequest());

        Assert.Equal(SettingsOperationCode.AccessDenied, update.Code);
        Assert.Equal(SettingsOperationCode.AccessDenied, reset.Code);
        Assert.Equal(0, repository.WriteCount);
        Assert.Equal(0, repository.ResetCount);
        organizationAccess.Verify(service => service.HasScopedOrganizationAdminAuthorityAsync(
            It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private OrganizationSettingsAccessService CreateOrganizationAccessService()
    {
        var audit = new Mock<IAuditService>();
        return new OrganizationSettingsAccessService(
            _context, _httpContextAccessor, audit.Object, new UserEffectivePermissionService(_context));
    }

    private static OrganizationController CreateOrganizationController(
        IOrganizationService organizationService,
        IOrganizationSettingsAccessService access,
        IAuthorizationService authorization,
        ClaimsPrincipal principal) => new(organizationService, access, authorization)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
    };

    private static Mock<IAuthorizationService> CreateAuthorizationService(params string[] grantedPolicies)
    {
        var granted = grantedPolicies.ToHashSet(StringComparer.Ordinal);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync((ClaimsPrincipal _, object? _, string policy) => granted.Contains(policy)
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        return authorization;
    }

    private ConfigurationSettingsService CreateConfigurationSettingsService(
        IReadOnlyCollection<string> permissions,
        IOrganizationSettingsAccessService organizationAccess,
        ISettingsRepository repository,
        ISettingDefinitionCatalog catalog,
        IManagementContextAccessor? managementContextAccessor = null)
    {
        var authorization = CreateAuthorizationService(permissions.ToArray());
        var audit = new Mock<IAuditService>();
        var currency = new Mock<ICurrencyService>();
        return new ConfigurationSettingsService(
            catalog,
            repository,
            new EffectiveConfigurationResolver(),
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { User = Principal(_tenantId, _actorId) }
            },
            authorization.Object,
            audit.Object,
            currency.Object,
            organizationAccess,
            managementContextAccessor: managementContextAccessor);
    }

    private static SettingDefinition OrganizationSetting() => new(
        TestSettingKey,
        SettingValueType.String,
        new HashSet<SettingScope> { SettingScope.Platform, SettingScope.Tenant, SettingScope.Organization },
        JsonSerializer.SerializeToElement("default"),
        "Any string value.",
        SettingOverrideBehavior.TenantAndOrganizationOverride,
        SettingSecurityClassification.Standard,
        _ => null);

    private static User NewUser(Guid id, Guid tenantId, string firstName, string lastName) => new()
    {
        Id = id,
        TenantId = tenantId,
        Email = $"{id:N}@example.test",
        FirstName = firstName,
        LastName = lastName,
        IsActive = true,
        CreatedBy = "test"
    };

    private static OrganizationEntity NewOrganization(Guid id, Guid tenantId, string name, bool active) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = name,
        IsActive = active,
        CreatedBy = "test"
    };

    private static Permission NewPermission(Guid tenantId, string resource, string action) => new()
    {
        TenantId = tenantId,
        Name = $"{resource}.{action}",
        Resource = resource,
        Action = action,
        CreatedBy = "test"
    };

    private static IManagementContextAccessor ReadOnlyManagementContext()
    {
        var accessor = new Mock<IManagementContextAccessor>();
        accessor.SetupGet(item => item.Current).Returns(new ManagementContext(
            Guid.NewGuid(), Guid.NewGuid(), ManagementPerspective.ProjectManager,
            ManagementViewMode.ReadOnlyViewAs, null, null, null, ManagementScope.Empty));
        return accessor.Object;
    }

    private static ClaimsPrincipal Principal(Guid tenantId, Guid actorId) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")),
        new Claim("tenantId", tenantId.ToString("D"))
    ], "OrganizationAdminTests"));

    private const string TestSettingKey = "test.organization.value";

    private sealed class SettingDefinitionCatalogForTests(params SettingDefinition[] definitions) : ISettingDefinitionCatalog
    {
        private readonly IReadOnlyDictionary<string, SettingDefinition> _definitions = definitions
            .ToDictionary(definition => definition.Key, StringComparer.Ordinal);

        public IReadOnlyCollection<SettingDefinition> All => _definitions.Values.ToArray();

        public bool TryGet(string key, out SettingDefinition definition) => _definitions.TryGetValue(key, out definition!);
    }

    private sealed class TestSettingsRepository : ISettingsRepository
    {
        public bool IsAvailable => true;
        public int WriteCount { get; private set; }
        public int ResetCount { get; private set; }
        public SettingsScopeIdentity? LastWriteIdentity { get; private set; }

        public Task<IReadOnlyDictionary<string, StoredConfigurationValue>> ReadAsync(
            SettingsScopeIdentity identity, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, StoredConfigurationValue>>(
                new Dictionary<string, StoredConfigurationValue>(StringComparer.Ordinal));

        public Task<SettingsStoreWriteResult> UpsertAsync(
            SettingsScopeIdentity identity,
            string key,
            JsonElement value,
            string? expectedVersion,
            string actorId,
            Func<StoredConfigurationValue?, StoredConfigurationValue, CancellationToken, Task> appendAudit,
            CancellationToken cancellationToken = default,
            Func<JsonElement?, JsonElement, CancellationToken, Task<string?>>? validateConfigurationChange = null)
        {
            WriteCount++;
            LastWriteIdentity = identity;
            return Task.FromResult(new SettingsStoreWriteResult(
                SettingsStoreWriteCode.Saved,
                new StoredConfigurationValue(Guid.NewGuid(), key, value.Clone(), "AQEBAQEBAQE=")));
        }

        public Task<SettingsStoreWriteResult> ResetAsync(
            SettingsScopeIdentity identity,
            string key,
            string? expectedVersion,
            string actorId,
            Func<StoredConfigurationValue, CancellationToken, Task> appendAudit,
            CancellationToken cancellationToken = default,
            Func<JsonElement?, JsonElement, CancellationToken, Task<string?>>? validateConfigurationChange = null)
        {
            ResetCount++;
            return Task.FromResult(new SettingsStoreWriteResult(SettingsStoreWriteCode.Reset));
        }
    }
}
