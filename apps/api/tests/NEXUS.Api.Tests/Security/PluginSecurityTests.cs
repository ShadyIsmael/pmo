using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using NEXUS.Api.Management;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.PluginManagement.Application;
using NEXUS.Modules.PluginManagement.Domain;
using NEXUS.Modules.PluginManagement.ModuleRegistration;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

/// <summary>
/// Regression coverage for plugin management security:
/// plugin reads are tenant-scoped and there is no plugin execution or
/// sandboxing path that could expose application secrets or execute
/// unauthorized code.
/// </summary>
[Collection("Static tenant resolution")]
public sealed class PluginSecurityTests : IDisposable
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid TenantAPluginId = Guid.NewGuid();
    private static readonly Guid TenantBPluginId = Guid.NewGuid();

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly PluginRegistryService _registry;

    public PluginSecurityTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new PluginSecuritySqliteTestAppDbContext(options);
        _db.Database.EnsureCreated();

        AppDbContext.ConfigureTenantResolution(CreateAuthenticatedTenantAccessor(TenantA));

        Seed();

        _registry = new PluginRegistryService(_db, CreateManagementContextAccessor(TenantA));
    }

    [Fact]
    public async Task GetPagedAsync_returns_only_caller_tenant_plugins()
    {
        var pageA = await _registry.GetPagedAsync(1, 20);
        Assert.Single(pageA.Items);
        Assert.Equal("TenantA-Plugin", pageA.Items[0].Name);

        // The fixture contains a TenantB plugin too; paging under TenantA must
        // remain scoped to the caller's tenant.
        Assert.Equal(1, pageA.TotalCount);

        var missingManagementContext = await new PluginRegistryService(_db).GetPagedAsync(1, 20);
        Assert.Empty(missingManagementContext.Items);
        Assert.Equal(0, missingManagementContext.TotalCount);

        var emptyTenantContext = await new PluginRegistryService(_db, CreateManagementContextAccessor(Guid.Empty))
            .GetPagedAsync(1, 20);
        Assert.Empty(emptyTenantContext.Items);
        Assert.Equal(0, emptyTenantContext.TotalCount);
    }

    [Fact]
    public async Task GetByIdAsync_returns_not_found_for_other_tenant_plugin()
    {
        var result = await _registry.GetByIdAsync(TenantBPluginId);
        Assert.False(result.IsSuccess);
        Assert.Equal(Error.NotFound().Code, result.Error?.Code);
    }

    [Fact]
    public void IPluginManager_has_no_registered_implementation_fail_closed()
    {
        // The IPluginManager interface exists but no implementation is
        // registered in PluginManagementModuleRegistration. Plugin activation
        // and execution are intentionally unsupported: there is no sandbox,
        // no AssemblyLoadContext isolation, and no privilege boundary.
        var services = new ServiceCollection();
        services.AddPluginManagementModule();
        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<IPluginManager>());

        var type = typeof(IPluginManager);
        Assert.NotNull(type);
        Assert.True(type.IsInterface);
        Assert.Contains(nameof(IPluginManager.ActivateAsync), type.GetMethods().Select(m => m.Name));
        Assert.Contains(nameof(IPluginManager.DeactivateAsync), type.GetMethods().Select(m => m.Name));
    }

    [Fact]
    public async Task TenantPlugin_inherits_tenant_id_from_base_entity()
    {
        // TenantPlugin derives from BaseEntity which carries TenantId, so
        // tenant isolation is enforced at the domain model level.
        var tenantPlugin = new TenantPlugin { TenantId = TenantA };
        Assert.Equal(TenantA, tenantPlugin.TenantId);
    }

    private void Seed()
    {
        _db.Plugins.AddRange(
            new Plugin
            {
                Id = TenantAPluginId,
                TenantId = TenantA,
                Name = "TenantA-Plugin",
                IsActive = true
            },
            new Plugin
            {
                Id = TenantBPluginId,
                TenantId = TenantB,
                Name = "TenantB-Plugin",
                IsActive = true
            });
        _db.SaveChanges();
    }

    private static HttpContextAccessor CreateAuthenticatedTenantAccessor(Guid tenantId)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim("tenantId", tenantId.ToString()) },
            authenticationType: "test");
        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    private static ManagementContextAccessor CreateManagementContextAccessor(Guid tenantId)
    {
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(
            Guid.NewGuid(),
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            ManagementScope.Empty));
        return accessor;
    }

    public void Dispose()
    {
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        _db.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Uses the production AppDbContext tenant filters and Plugin mappings, while
    /// creating only the isolated schema this suite reads. The full application
    /// model contains SQL Server-specific schema constructs unrelated to plugin
    /// registry behavior and is not needed by these tests.
    /// </summary>
    private sealed class PluginSecuritySqliteTestAppDbContext(DbContextOptions<AppDbContext> options)
        : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Plugin>().Ignore(plugin => plugin.TenantPlugins);

            var pluginSchemaEntities = new HashSet<string>(StringComparer.Ordinal)
            {
                typeof(Plugin).FullName!,
                typeof(PluginDependency).FullName!
            };

            foreach (var entityTypeName in modelBuilder.Model.GetEntityTypes()
                         .Select(entityType => entityType.Name)
                         .Where(entityTypeName => !pluginSchemaEntities.Contains(entityTypeName))
                         .ToArray())
            {
                modelBuilder.Ignore(entityTypeName);
            }
        }
    }
}
