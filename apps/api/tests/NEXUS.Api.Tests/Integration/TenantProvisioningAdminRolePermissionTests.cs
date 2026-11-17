using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class TenantProvisioningAdminRolePermissionTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _administratorId = Guid.NewGuid();
    private readonly HttpContextAccessor _httpContextAccessor;
    private AppDbContext _context = null!;

    public TenantProvisioningAdminRolePermissionTests()
    {
        _httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, _actorId.ToString("D")),
                    new Claim("tenantId", _tenantId.ToString("D"))
                ], "test"))
            }
        };
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new SqliteTestAppDbContext(options);
        AppDbContext.ConfigureTenantResolution(_httpContextAccessor);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task First_administrator_gets_all_initial_grants_while_core_stays_at_63_keys()
    {
        await SeedProvisioningTenantAsync();

        var result = await CreateService().AssignAdministratorAsync(_tenantId, _administratorId);

        Assert.Equal(TenantProvisioningCode.Activated, result.Code);
        var role = await GetProtectedAdminRoleAsync();
        var actualKeys = role.Permissions.Select(GetPermissionKey).ToHashSet(StringComparer.Ordinal);
        var expectedInitialKeys = PermissionCatalog.Entries
            .Where(entry => entry.IsActive
                && entry.IsAssignable
                && entry.Key != "Program.CreateAcrossPortfolios"
                && entry.Scope is not (PermissionScope.Platform or PermissionScope.CrossTenant or PermissionScope.SystemOwner))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
        var coreKeys = PermissionProtectionPolicy.Empty.ProtectedPermissionKeys;

        Assert.Equal(187, expectedInitialKeys.Count);
        Assert.Equal(63, coreKeys.Count);
        Assert.Equal(expectedInitialKeys.OrderBy(key => key, StringComparer.Ordinal),
            actualKeys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.All(coreKeys, key => Assert.Contains(key, actualKeys));
        Assert.Contains("Project.View", actualKeys);
        Assert.DoesNotContain("Project.View", coreKeys);
        Assert.DoesNotContain("Program.CreateAcrossPortfolios", actualKeys);
    }

    [Fact]
    public async Task Existing_admin_preserves_its_grants_and_only_appends_missing_core_keys()
    {
        await SeedProvisioningTenantAsync();
        var existingCoreGrant = NewPermission("Roles", "View");
        var existingPolicyManagedGrant = NewPermission("Project", "View");
        var adminRole = new Role
        {
            TenantId = _tenantId,
            Name = "admin",
            Description = "Existing tenant administrator",
            Category = "System",
            IsSystem = true,
            CreatedBy = "seed"
        };
        adminRole.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, _tenantId);
        adminRole.Permissions.Add(existingCoreGrant);
        adminRole.Permissions.Add(existingPolicyManagedGrant);
        _context.Roles.Add(adminRole);
        using (var tenantScope = TrustedTenantExecutionContext.Enter(_tenantId))
        {
            await _context.SaveChangesAsync();
        }

        var result = await CreateService().AssignAdministratorAsync(_tenantId, _administratorId);

        Assert.Equal(TenantProvisioningCode.Activated, result.Code);
        var reloadedRole = await GetProtectedAdminRoleAsync();
        var actualKeys = reloadedRole.Permissions.Select(GetPermissionKey).ToHashSet(StringComparer.Ordinal);
        var coreKeys = PermissionProtectionPolicy.Empty.ProtectedPermissionKeys;
        var expectedKeys = coreKeys.Append("Project.View").ToHashSet(StringComparer.Ordinal);

        Assert.Equal(63, coreKeys.Count);
        Assert.Contains("Roles.View", coreKeys);
        Assert.DoesNotContain("Project.View", coreKeys);
        Assert.Equal(expectedKeys.OrderBy(key => key, StringComparer.Ordinal),
            actualKeys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.Contains("Project.View", actualKeys);
    }

    private async Task SeedProvisioningTenantAsync()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(_tenantId);
        _context.Tenants.Add(new NEXUS.Modules.Organization.Domain.Tenant
        {
            Id = _tenantId,
            Code = $"PROV-{_tenantId:N}",
            DisplayName = "Provisioning tenant",
            Status = TenantLifecycleStatus.Provisioning,
            CreatedBy = "test"
        });
        _context.Organizations.Add(new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Initial organization",
            IsActive = true,
            CreatedBy = "test"
        });
        _context.Users.Add(new User
        {
            Id = _administratorId,
            TenantId = _tenantId,
            Email = $"admin-{_administratorId:N}@example.test",
            FirstName = "Tenant",
            LastName = "Administrator",
            IsActive = true,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();
    }

    private Permission NewPermission(string resource, string action) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        Name = $"{resource}.{action}",
        Resource = resource,
        Action = action,
        CreatedBy = "seed"
    };

    private async Task<Role> GetProtectedAdminRoleAsync() => await _context.Roles
        .IgnoreQueryFilters()
        .Include(role => role.Permissions)
        .SingleAsync(role => role.TenantId == _tenantId
            && role.SystemRoleKey == Role.TenantAdminSystemRoleKey
            && !role.IsDeleted);

    private TenantProvisioningService CreateService()
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return new TenantProvisioningService(
            _context,
            _httpContextAccessor,
            audit.Object,
            Array.Empty<IOrganizationLifecycleHandler>());
    }

    private static string GetPermissionKey(Permission permission) => $"{permission.Resource}.{permission.Action}";
}
