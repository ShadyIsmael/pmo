using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class TenantEntityWriteGuardTests : IDisposable
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HttpContextAccessor _http = new();
    private readonly AppDbContext _db;

    public TenantEntityWriteGuardTests()
    {
        _connection.Open();
        AppDbContext.ConfigureTenantResolution(_http);
        _db = new SqliteTestAppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task SaveChanges_rejects_added_tenant_entity_when_request_has_no_trusted_tenant()
    {
        _http.HttpContext = null;
        _db.Projects.Add(NewProject(_tenantA, "NO-CONTEXT"));

        var exception = await Record.ExceptionAsync(() => _db.SaveChangesAsync());

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(await _db.Projects.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task SaveChanges_rejects_added_entity_for_a_different_request_tenant()
    {
        SetAuthenticatedTenant(_tenantA);
        _db.Projects.Add(NewProject(_tenantB, "FOREIGN-TENANT"));

        var exception = await Record.ExceptionAsync(() => _db.SaveChangesAsync());

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(await _db.Projects.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task SaveChanges_allows_added_entity_for_the_authenticated_tenant()
    {
        SetAuthenticatedTenant(_tenantA);
        var project = NewProject(_tenantA, "OWN-TENANT");
        _db.Projects.Add(project);

        var saved = await _db.SaveChangesAsync();

        Assert.Equal(1, saved);
        var persisted = await _db.Projects.SingleAsync();
        Assert.Equal(project.Id, persisted.Id);
        Assert.Equal(_tenantA, persisted.TenantId);
    }

    [Fact]
    public async Task SaveChanges_rejects_added_non_base_tenant_entity_for_another_request_tenant()
    {
        SetAuthenticatedTenant(_tenantA);
        _db.Set<EntityCodeSequence>().Add(new EntityCodeSequence
        {
            TenantId = _tenantB,
            EntityType = "Project",
            NextValue = 1
        });

        var exception = await Record.ExceptionAsync(() => _db.SaveChangesAsync());

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(await _db.Set<EntityCodeSequence>().IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task SaveChanges_rejects_modified_entity_from_another_request_tenant()
    {
        SetAuthenticatedTenant(_tenantA);
        var project = NewProject(_tenantA, "OWN-TENANT");
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        SetAuthenticatedTenant(_tenantB);
        project.Name = "Cross-tenant update";
        var exception = await Record.ExceptionAsync(() => _db.SaveChangesAsync());

        Assert.IsType<InvalidOperationException>(exception);
        var persisted = await _db.Projects.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal("OWN-TENANT", persisted.Name);
        Assert.Equal(_tenantA, persisted.TenantId);
    }

    [Fact]
    public async Task SaveChanges_rejects_deleting_entity_from_another_request_tenant()
    {
        SetAuthenticatedTenant(_tenantA);
        var project = NewProject(_tenantA, "OWN-TENANT");
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        SetAuthenticatedTenant(_tenantB);
        _db.Projects.Remove(project);
        var exception = await Record.ExceptionAsync(() => _db.SaveChangesAsync());

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Single(await _db.Projects.IgnoreQueryFilters().AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SaveChanges_rejects_changing_the_tenant_id_of_an_existing_entity()
    {
        SetAuthenticatedTenant(_tenantA);
        var project = NewProject(_tenantA, "OWN-TENANT");
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        project.TenantId = _tenantB;
        var exception = await Record.ExceptionAsync(() => _db.SaveChangesAsync());

        Assert.IsType<InvalidOperationException>(exception);
        var persisted = await _db.Projects.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal(_tenantA, persisted.TenantId);
    }

    [Fact]
    public async Task SaveChanges_allows_added_entity_inside_matching_trusted_background_scope()
    {
        _http.HttpContext = null;
        using (TrustedTenantExecutionContext.Enter(_tenantA))
        {
            _db.Projects.Add(NewProject(_tenantA, "TRUSTED-BACKGROUND"));
            Assert.Equal(1, await _db.SaveChangesAsync());
        }

        var persisted = await _db.Projects.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal(_tenantA, persisted.TenantId);
    }

    [Fact]
    public async Task SaveChanges_allows_verified_pre_authentication_tenant_only_on_anonymous_endpoint()
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(
            null,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "POST /api/auth/login"));
        _http.HttpContext = context;

        using (PreAuthenticationTenantExecutionContext.EnterAfterCredentialValidation(_tenantA))
        {
            _db.Projects.Add(NewProject(_tenantA, "VERIFIED-LOGIN-TENANT"));
            Assert.Equal(1, await _db.SaveChangesAsync());
        }

        var persisted = await _db.Projects.SingleAsync();
        Assert.Equal(_tenantA, persisted.TenantId);
    }

    [Fact]
    public async Task SaveChanges_does_not_allow_pre_authentication_scope_on_non_anonymous_endpoint()
    {
        _http.HttpContext = new DefaultHttpContext();
        using var tenantScope = PreAuthenticationTenantExecutionContext.EnterAfterCredentialValidation(_tenantA);
        _db.Projects.Add(NewProject(_tenantA, "NON-ANONYMOUS-SCOPE"));

        var exception = await Record.ExceptionAsync(() => _db.SaveChangesAsync());

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(await _db.Projects.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task SaveChanges_does_not_treat_pre_authentication_scope_as_background_tenant_context()
    {
        _http.HttpContext = null;
        using var tenantScope = PreAuthenticationTenantExecutionContext.EnterAfterCredentialValidation(_tenantA);
        _db.Projects.Add(NewProject(_tenantA, "NO-HTTP-REQUEST"));

        var exception = await Record.ExceptionAsync(() => _db.SaveChangesAsync());

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(await _db.Projects.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task SaveChanges_authenticated_claim_takes_precedence_over_pre_authentication_scope()
    {
        SetAuthenticatedTenant(_tenantA);
        using var tenantScope = PreAuthenticationTenantExecutionContext.EnterAfterCredentialValidation(_tenantB);
        _db.Projects.Add(NewProject(_tenantB, "PREAUTH-CANNOT-OVERRIDE-CLAIM"));

        var exception = await Record.ExceptionAsync(() => _db.SaveChangesAsync());

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(await _db.Projects.IgnoreQueryFilters().ToListAsync());
    }

    private void SetAuthenticatedTenant(Guid tenantId)
    {
        _http.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenantId", tenantId.ToString("D"))],
                authenticationType: "test"))
        };
    }

    private static Project NewProject(Guid tenantId, string code) => new()
    {
        TenantId = tenantId,
        Name = code,
        Code = code,
        Description = "Tenant write guard test",
        Status = "Active",
        Currency = "USD",
        CreatedBy = "tenant-write-guard-test",
        RowVersion = new byte[8]
    };

    public void Dispose()
    {
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        _db.Dispose();
        _connection.Dispose();
    }
}
