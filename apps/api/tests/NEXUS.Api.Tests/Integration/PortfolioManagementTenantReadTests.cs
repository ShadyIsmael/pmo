using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.PortfolioManagement;
using NEXUS.Api.ProgramManagement;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Persistence;
using NEXUS.Persistence.Management;
using NEXUS.SharedKernel.Management;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class PortfolioManagementTenantReadTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _ownerA = Guid.NewGuid();
    private readonly Guid _ownerB = Guid.NewGuid();
    private readonly Guid _portfolioA = Guid.NewGuid();
    private readonly Guid _portfolioA2 = Guid.NewGuid();
    private readonly Guid _portfolioB = Guid.NewGuid();
    private readonly Guid _programA = Guid.NewGuid();
    private readonly Guid _programA2 = Guid.NewGuid();
    private readonly Guid _programB = Guid.NewGuid();

    public PortfolioManagementTenantReadTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Database.EnsureCreated();
        SetTenant(_tenantA);

        _db.Users.AddRange(
            new User { Id = _ownerA, TenantId = _tenantA, Email = "manager-a@example.test", FirstName = "Portfolio", LastName = "Owner A", PasswordHash = "test", CreatedBy = "test" },
            new User { Id = _ownerB, TenantId = _tenantB, Email = "manager-b@example.test", FirstName = "Portfolio", LastName = "Owner B", PasswordHash = "test", CreatedBy = "test" });
        _db.Portfolios.AddRange(
            new Portfolio { Id = _portfolioA, TenantId = _tenantA, Name = "Tenant A Portfolio", Code = "PF-A", OwnerId = _ownerA, Status = "Active", Currency = "USD", CreatedBy = "test" },
            new Portfolio { Id = _portfolioA2, TenantId = _tenantA, Name = "Tenant A Other Portfolio", Code = "PF-A2", OwnerId = _ownerA, Status = "Active", Currency = "EUR", CreatedBy = "test" },
            new Portfolio { Id = _portfolioB, TenantId = _tenantB, Name = "Tenant B Portfolio", Code = "PF-B", OwnerId = _ownerB, Status = "Active", Currency = "GBP", CreatedBy = "test" });
        _db.Programs.AddRange(
            new ProgramEntity { Id = _programA, TenantId = _tenantA, PortfolioId = _portfolioA, Name = "Tenant A Program", Code = "PG-A", Status = "Active", CreatedBy = "test" },
            new ProgramEntity { Id = _programA2, TenantId = _tenantA, PortfolioId = _portfolioA2, Name = "Tenant A Other Program", Code = "PG-A2", Status = "Active", CreatedBy = "test" },
            new ProgramEntity { Id = _programB, TenantId = _tenantB, PortfolioId = _portfolioB, Name = "Tenant B Program", Code = "PG-B", Status = "Active", CreatedBy = "test" });
        _db.SaveChanges();
    }

    [Fact]
    public async Task List_without_management_context_returns_populated_portfolios_in_current_tenant()
    {
        var result = await CreateService(null).GetPortfoliosAsync(1, 50, null);

        Assert.Equal(2, result.Items.Count);
        var item = Assert.Single(result.Items, portfolio => portfolio.Id == _portfolioA);
        Assert.Equal(_portfolioA, item.Id);
        Assert.Equal("Tenant A Portfolio", item.Name);
        Assert.Equal("PF-A", item.Code);
        // Owner identity is deliberately unavailable without an authorized management context.
        Assert.Null(item.OwnerName);
        Assert.Equal("Active", item.Status);
        Assert.Equal("USD", item.Currency);
        Assert.Contains(result.Items, portfolio => portfolio.Id == _portfolioA2);
        Assert.DoesNotContain(result.Items, portfolio => portfolio.Id == _portfolioB);
    }

    [Fact]
    public async Task List_with_portfolio_manager_context_keeps_the_existing_narrow_scope()
    {
        var scope = new ManagementScope(
            new HashSet<Guid> { _portfolioA },
            new HashSet<Guid>(),
            new HashSet<Guid>());
        var context = new ManagementContext(
            _ownerA,
            _tenantA,
            ManagementPerspective.PortfolioManager,
            ManagementViewMode.Native,
            _portfolioA,
            null,
            null,
            scope);

        var result = await CreateService(context).GetPortfoliosAsync(1, 50, null);

        var item = Assert.Single(result.Items);
        Assert.Equal(_portfolioA, item.Id);
        Assert.DoesNotContain(result.Items, portfolio => portfolio.Id == _portfolioA2);
    }

    [Fact]
    public async Task Tenant_admin_read_scope_sees_all_portfolios_but_never_another_tenant()
    {
        var context = TenantAdminReadContext();
        var result = await CreateService(context).GetPortfoliosAsync(1, 50, null);

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, item => item.Id == _portfolioA);
        Assert.Contains(result.Items, item => item.Id == _portfolioA2);
        Assert.DoesNotContain(result.Items, item => item.Id == _portfolioB);
    }

    [Fact]
    public async Task Tenant_admin_read_scope_can_open_unassigned_portfolio_inside_tenant_only()
    {
        var service = CreateService(TenantAdminReadContext());

        var visible = await service.GetOverviewAsync(_portfolioA2, 1, 1, 25);
        var outsideTenant = await service.GetOverviewAsync(_portfolioB, 1, 1, 25);

        Assert.True(visible.IsSuccess);
        Assert.False(outsideTenant.IsSuccess);
    }

    [Fact]
    public async Task Tenant_admin_portfolio_write_scope_cannot_update_another_tenant_portfolio()
    {
        var service = CreatePortfolioService(TenantAdminWriteContext());

        var result = await service.UpdateAsync(_portfolioB, new SavePortfolioDto
        {
            Name = "Unauthorized cross-tenant update",
            OwnerId = _ownerA,
            Status = "Active",
            Currency = "USD"
        }, _ownerA.ToString());

        Assert.True(result.IsFailure);
        var unchanged = await _db.Portfolios.IgnoreQueryFilters().SingleAsync(item => item.Id == _portfolioB);
        Assert.Equal("Tenant B Portfolio", unchanged.Name);
    }

    [Fact]
    public async Task Tenant_admin_read_scope_sees_all_programs_but_never_another_tenant()
    {
        var management = new Mock<IManagementContextAccessor>();
        management.Setup(accessor => accessor.Current).Returns(TenantAdminReadContext());
        var service = new ProgramManagementService(_db, management.Object, Mock.Of<IManagementScopeResolver>());

        var result = await service.GetProgramsAsync(1, 50, null);

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, item => item.Id == _programA);
        Assert.Contains(result.Items, item => item.Id == _programA2);
        Assert.DoesNotContain(result.Items, item => item.Id == _programB);
    }

    [Fact]
    public async Task Tenant_admin_read_scope_can_open_unassigned_program_inside_tenant_only()
    {
        var management = new Mock<IManagementContextAccessor>();
        management.Setup(accessor => accessor.Current).Returns(TenantAdminReadContext());
        var service = new ProgramManagementService(_db, management.Object, Mock.Of<IManagementScopeResolver>());

        var visible = await service.GetOverviewAsync(_programA2, 1, 25);
        var outsideTenant = await service.GetOverviewAsync(_programB, 1, 25);

        Assert.True(visible.IsSuccess);
        Assert.False(outsideTenant.IsSuccess);
    }

    [Fact]
    public async Task Only_active_tenant_system_admin_role_enables_tenant_read_scope()
    {
        var adminRole = new Role { Id = Guid.NewGuid(), TenantId = _tenantA, Name = "admin", IsSystem = true, CreatedBy = "System" };
        adminRole.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, _tenantA);
        _db.Roles.Add(adminRole);
        _db.UserRoles.Add(new UserRole { TenantId = _tenantA, UserId = _ownerA, RoleId = adminRole.Id, CreatedBy = "test" });
        _db.SaveChanges();

        var resolver = new ManagementScopeResolver(_db, new NEXUS.Modules.Identity.Infrastructure.UserEffectivePermissionService(_db));

        Assert.True(await resolver.IsTenantAdministratorAsync(_ownerA, _tenantA));
        Assert.False(await resolver.IsTenantAdministratorAsync(_ownerB, _tenantA));
    }

    [Fact]
    public async Task Overview_without_management_context_reads_tenant_record_but_not_another_tenant()
    {
        var service = CreateService(null);

        var visible = await service.GetOverviewAsync(_portfolioA, 1, 1, 25);
        var outsideTenant = await service.GetOverviewAsync(_portfolioB, 1, 1, 25);

        Assert.True(visible.IsSuccess);
        Assert.Equal("Tenant A Portfolio", visible.Value!.Portfolio.Name);
        Assert.False(outsideTenant.IsSuccess);
    }

    [Fact]
    public async Task Overview_with_management_context_denies_portfolio_outside_user_scope()
    {
        var scope = new ManagementScope(
            new HashSet<Guid> { _portfolioA },
            new HashSet<Guid>(),
            new HashSet<Guid>());
        var context = new ManagementContext(
            _ownerA,
            _tenantA,
            ManagementPerspective.PortfolioManager,
            ManagementViewMode.Native,
            _portfolioA,
            null,
            null,
            scope);

        var result = await CreateService(context).GetOverviewAsync(_portfolioA2, 1, 1, 25);

        Assert.False(result.IsSuccess);
    }

    private PortfolioManagementService CreateService(ManagementContext? context)
    {
        var management = new Mock<IManagementContextAccessor>();
        management.Setup(accessor => accessor.Current).Returns(context);
        return new PortfolioManagementService(_db, management.Object);
    }

    private PortfolioService CreatePortfolioService(ManagementContext? context)
    {
        var management = new Mock<IManagementContextAccessor>();
        management.Setup(accessor => accessor.Current).Returns(context);
        return new PortfolioService(_db, _http.Object, management.Object);
    }

    private ManagementContext TenantAdminReadContext() => new(
        _ownerA,
        _tenantA,
        ManagementPerspective.ProjectManager,
        ManagementViewMode.Native,
        null,
        null,
        null,
        ManagementScope.Empty)
    {
        CanReadAllTenantPortfolioProgramData = true
    };

    private ManagementContext TenantAdminWriteContext() => new(
        _ownerA,
        _tenantA,
        ManagementPerspective.ProjectManager,
        ManagementViewMode.Native,
        null,
        null,
        null,
        ManagementScope.Empty)
    {
        HasTenantAdministratorPortfolioWriteScope = true
    };

    private void SetTenant(Guid tenantId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenantId", tenantId.ToString()) }, "test"));
        _http.Setup(accessor => accessor.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_http.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
