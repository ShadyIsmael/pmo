using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NEXUS.Modules.Portfolio.Api;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.SharedKernel.Management;
using Xunit;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Api.Tests.Security;

public sealed class PortfolioProgramStatusScopeTests
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _organizationA = Guid.NewGuid();
    private readonly Guid _organizationB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _portfolioA = Guid.NewGuid();
    private readonly Guid _portfolioOtherOrg = Guid.NewGuid();
    private readonly Guid _portfolioB = Guid.NewGuid();
    private readonly Guid _programA = Guid.NewGuid();
    private readonly Guid _programOtherOrg = Guid.NewGuid();
    private readonly Guid _programB = Guid.NewGuid();

    [Fact]
    public async Task Portfolio_status_mutations_require_a_scoped_same_tenant_record()
    {
        await using var db = CreateDbContext();
        Seed(db);

        var withoutManagementContext = new PortfolioService(db, RequestAccessor(_tenantA, _userA));
        var missingScopeResult = await withoutManagementContext.ArchiveAsync(_portfolioA, _userA.ToString());
        Assert.True(missingScopeResult.IsFailure);
        Assert.Equal("Active", (await db.Portfolios.SingleAsync(item => item.Id == _portfolioA)).Status);

        var currentScope = new PortfolioService(
            db,
            RequestAccessor(_tenantA, _userA),
            ManagementAccessor(Context(_tenantA, new HashSet<Guid> { _portfolioA }, new HashSet<Guid>(), new HashSet<Guid>())));
        var foreignTenant = await currentScope.ArchiveAsync(_portfolioB, _userA.ToString());
        var otherOrganization = await currentScope.ArchiveAsync(_portfolioOtherOrg, _userA.ToString());

        Assert.True(foreignTenant.IsFailure);
        Assert.True(otherOrganization.IsFailure);
        Assert.Equal("Active", (await db.Portfolios.SingleAsync(item => item.Id == _portfolioB)).Status);
        Assert.Equal("Active", (await db.Portfolios.SingleAsync(item => item.Id == _portfolioOtherOrg)).Status);

        var allowed = await currentScope.ArchiveAsync(_portfolioA, _userA.ToString());
        Assert.True(allowed.IsSuccess);
        Assert.Equal("Archived", (await db.Portfolios.SingleAsync(item => item.Id == _portfolioA)).Status);
    }

    [Fact]
    public async Task Program_status_mutations_require_a_scoped_same_tenant_record()
    {
        await using var db = CreateDbContext();
        Seed(db);

        var withoutManagementContext = new ProgramService(db, RequestAccessor(_tenantA, _userA));
        var missingScopeResult = await withoutManagementContext.ArchiveAsync(_programA, _userA.ToString());
        Assert.True(missingScopeResult.IsFailure);
        Assert.Equal("Active", (await db.Programs.SingleAsync(item => item.Id == _programA)).Status);

        var currentScope = new ProgramService(
            db,
            RequestAccessor(_tenantA, _userA),
            ManagementAccessor(Context(_tenantA, new HashSet<Guid>(), new HashSet<Guid> { _programA }, new HashSet<Guid>())));
        var foreignTenant = await currentScope.ArchiveAsync(_programB, _userA.ToString());
        var otherOrganization = await currentScope.ArchiveAsync(_programOtherOrg, _userA.ToString());

        Assert.True(foreignTenant.IsFailure);
        Assert.True(otherOrganization.IsFailure);
        Assert.Equal("Active", (await db.Programs.SingleAsync(item => item.Id == _programB)).Status);
        Assert.Equal("Active", (await db.Programs.SingleAsync(item => item.Id == _programOtherOrg)).Status);

        var allowed = await currentScope.ArchiveAsync(_programA, _userA.ToString());
        Assert.True(allowed.IsSuccess);
        Assert.Equal("Archived", (await db.Programs.SingleAsync(item => item.Id == _programA)).Status);
    }

    [Theory]
    [InlineData(typeof(PortfolioController), nameof(PortfolioController.Archive), "Portfolio.Archive")]
    [InlineData(typeof(PortfolioController), nameof(PortfolioController.Reactivate), "Portfolio.Reactivate")]
    [InlineData(typeof(ProgramController), nameof(ProgramController.Archive), "Program.Archive")]
    [InlineData(typeof(ProgramController), nameof(ProgramController.Reactivate), "Program.Reactivate")]
    public async Task Direct_status_api_routes_require_their_canonical_permission(
        Type controllerType,
        string actionName,
        string requiredPermission)
    {
        var method = controllerType.GetMethod(actionName);
        Assert.NotNull(method);
        Assert.Equal(requiredPermission, method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(configure =>
            configure.AddPolicy(requiredPermission, policy => policy.RequireClaim("permission", requiredPermission)));
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var noPermission = new ClaimsPrincipal(new ClaimsIdentity([], "test"));
        var permitted = new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", requiredPermission)], "test"));
        Assert.False((await authorization.AuthorizeAsync(noPermission, requiredPermission)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(permitted, requiredPermission)).Succeeded);
    }

    private StatusTestDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<StatusTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private void Seed(StatusTestDbContext db)
    {
        db.Portfolios.AddRange(
            new PortfolioEntity { Id = _portfolioA, TenantId = _tenantA, OrganizationId = _organizationA, Name = "Tenant A / Org A", Status = "Active", CreatedBy = "test" },
            new PortfolioEntity { Id = _portfolioOtherOrg, TenantId = _tenantA, OrganizationId = _organizationB, Name = "Tenant A / Org B", Status = "Active", CreatedBy = "test" },
            new PortfolioEntity { Id = _portfolioB, TenantId = _tenantB, OrganizationId = Guid.NewGuid(), Name = "Tenant B", Status = "Active", CreatedBy = "test" });
        db.Programs.AddRange(
            new ProgramEntity { Id = _programA, TenantId = _tenantA, PortfolioId = _portfolioA, Name = "Tenant A / Org A", Status = "Active", CreatedBy = "test" },
            new ProgramEntity { Id = _programOtherOrg, TenantId = _tenantA, PortfolioId = _portfolioOtherOrg, Name = "Tenant A / Org B", Status = "Active", CreatedBy = "test" },
            new ProgramEntity { Id = _programB, TenantId = _tenantB, PortfolioId = _portfolioB, Name = "Tenant B", Status = "Active", CreatedBy = "test" });
        db.SaveChanges();
    }

    private ManagementContext Context(
        Guid tenantId,
        IReadOnlySet<Guid> portfolioIds,
        IReadOnlySet<Guid> programIds,
        IReadOnlySet<Guid> projectIds) => new(
        _userA,
        tenantId,
        ManagementPerspective.ProjectManager,
        ManagementViewMode.Native,
        null,
        null,
        null,
        new ManagementScope(portfolioIds, programIds, projectIds)
        {
            OrganizationIds = new HashSet<Guid> { _organizationA }
        });

    private static IHttpContextAccessor RequestAccessor(Guid tenantId, Guid userId) => new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenantId", tenantId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            ], "test"))
        }
    };

    private static IManagementContextAccessor ManagementAccessor(ManagementContext context)
    {
        var accessor = new Mock<IManagementContextAccessor>();
        accessor.Setup(item => item.Current).Returns(context);
        return accessor.Object;
    }

    private sealed class StatusTestDbContext(DbContextOptions<StatusTestDbContext> options) : DbContext(options)
    {
        public DbSet<PortfolioEntity> Portfolios => Set<PortfolioEntity>();
        public DbSet<ProgramEntity> Programs => Set<ProgramEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PortfolioEntity>().Ignore(portfolio => portfolio.Programs);
            modelBuilder.Entity<ProgramEntity>().Ignore(program => program.Portfolio);
        }
    }
}
