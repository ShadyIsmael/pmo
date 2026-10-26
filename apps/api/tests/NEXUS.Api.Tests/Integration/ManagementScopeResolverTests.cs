using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.Persistence.Management;
using NEXUS.SharedKernel.Management;
using Xunit;
using PortfolioProgram = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class ManagementScopeResolverTests : IDisposable
{
    private static readonly Guid TenantId = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("c1000000-0000-0000-0000-000000000010");
    private static readonly Guid PortfolioId = Guid.Parse("c1000000-0000-0000-0000-000000000020");
    private static readonly Guid OtherPortfolioId = Guid.Parse("c1000000-0000-0000-0000-000000000023");
    private static readonly Guid InheritedProgramId = Guid.Parse("c1000000-0000-0000-0000-000000000021");
    private static readonly Guid DirectProgramId = Guid.Parse("c1000000-0000-0000-0000-000000000022");
    private static readonly Guid InheritedProjectId = Guid.Parse("c1000000-0000-0000-0000-000000000030");
    private static readonly Guid DirectProjectId = Guid.Parse("c1000000-0000-0000-0000-000000000031");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly ManagementScopeResolver _resolver;

    public ManagementScopeResolverTests()
    {
        _connection.Open();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("tenantId", TenantId.ToString()) }, "tests"));
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor { HttpContext = httpContext });
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _db.Users.Add(new User
        {
            Id = UserId, TenantId = TenantId, Email = "manager@example.test", FirstName = "Scoped",
            LastName = "Manager", IsActive = true, PasswordHash = "test-hash", CreatedBy = "tests",
            RowVersion = new byte[8]
        });
        _db.Portfolios.AddRange(
            new Portfolio
            {
                Id = PortfolioId, TenantId = TenantId, Name = "Direct Portfolio", Code = "DIR-P",
                Currency = "USD", OwnerId = UserId, Status = "Active", CreatedBy = "tests",
                RowVersion = new byte[8]
            },
            new Portfolio
            {
                Id = OtherPortfolioId, TenantId = TenantId, Name = "Other Portfolio", Code = "OTH-P",
                Currency = "USD", Status = "Active", CreatedBy = "tests", RowVersion = new byte[8]
            });
        _db.Programs.AddRange(
            Program(InheritedProgramId, "Inherited Program", "INH-G", PortfolioId),
            Program(DirectProgramId, "Direct Program", "DIR-G", OtherPortfolioId, UserId));
        _db.Projects.AddRange(
            Project(InheritedProjectId, "Inherited Project", "INH-P", PortfolioId, InheritedProgramId),
            Project(DirectProjectId, "Direct Project", "DIR-PRJ", null, null));
        _db.SaveChanges();

        _resolver = new ManagementScopeResolver(_db);
    }

    [Fact]
    public async Task PerspectiveSnapshot_UnionsDirectManagerContextsWithInheritedPortfolioScope()
    {
        var snapshot = await _resolver.GetPerspectiveSnapshotAsync(UserId, TenantId);

        var programContexts = snapshot.AvailablePerspectives
            .Single(x => x.Perspective == ManagementPerspective.ProgramManager).Contexts;
        var projectContexts = snapshot.AvailablePerspectives
            .Single(x => x.Perspective == ManagementPerspective.ProjectManager).Contexts;

        Assert.Contains(programContexts, x => x.Id == DirectProgramId);
        Assert.Contains(programContexts, x => x.Id == InheritedProgramId);
        Assert.Contains(projectContexts, x => x.Id == DirectProjectId);
        Assert.Contains(projectContexts, x => x.Id == InheritedProjectId);
    }

    private static PortfolioProgram Program(
        Guid id, string name, string code, Guid portfolioId, Guid? managerId = null) => new()
    {
        Id = id, PublicId = Guid.NewGuid(), TenantId = TenantId, Name = name, Code = code,
        PortfolioId = portfolioId, ProgramManagerId = managerId, Status = "Active", CreatedBy = "tests",
        RowVersion = new byte[8]
    };

    private static Project Project(Guid id, string name, string code, Guid? portfolioId, Guid? programId) => new()
    {
        Id = id, PublicId = Guid.NewGuid(), TenantId = TenantId, Name = name, Code = code,
        PortfolioId = portfolioId, ProgramId = programId, ProjectManagerId = UserId,
        Status = "Active", CreatedBy = "tests", RowVersion = new byte[8]
    };

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
