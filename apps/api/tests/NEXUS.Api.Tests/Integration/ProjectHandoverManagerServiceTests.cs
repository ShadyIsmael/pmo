using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using PortfolioProgram = NEXUS.Modules.Portfolio.Domain.Program;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class ProjectHandoverManagerServiceTests : IDisposable
{
    private static readonly Guid TenantA = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ProjectId = Guid.Parse("10000000-0000-0000-0000-000000000010");
    private static readonly Guid CurrentManagerId = Guid.Parse("10000000-0000-0000-0000-000000000011");
    private static readonly Guid ProjectManagerId = Guid.Parse("10000000-0000-0000-0000-000000000012");
    private static readonly Guid PortfolioManagerId = Guid.Parse("10000000-0000-0000-0000-000000000013");
    private static readonly Guid ProgramManagerId = Guid.Parse("10000000-0000-0000-0000-000000000014");
    private static readonly Guid BothManagerId = Guid.Parse("10000000-0000-0000-0000-000000000015");
    private static readonly Guid OtherTenantManagerId = Guid.Parse("20000000-0000-0000-0000-000000000016");
    private static readonly Guid InactiveManagerId = Guid.Parse("10000000-0000-0000-0000-000000000017");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HandoverTestDbContext _context;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly ProjectHandoverRequestService _service;

    public ProjectHandoverManagerServiceTests()
    {
        _connection.Open();
        _context = new HandoverTestDbContext(new DbContextOptionsBuilder<HandoverTestDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        SeedData();

        _service = new ProjectHandoverRequestService(
            _context,
            Mock.Of<IProjectSnapshotService>(),
            Mock.Of<INotificationService>(),
            _httpContextAccessor.Object);
    }

    [Fact]
    public async Task GetEligibleManagersAsync_ReturnsTenantProjectPortfolioAndProgramManagers_WithoutDuplicatesOrCurrentManager()
    {
        SetTenant(TenantA);

        var result = await _service.GetEligibleManagersAsync(ProjectId);

        Assert.True(result.IsSuccess);
        var managers = result.Value!;
        Assert.Equal(new[] { BothManagerId, PortfolioManagerId, ProgramManagerId, ProjectManagerId }, managers.Select(manager => manager.Id));
        Assert.Equal(new[] { "ProjectManager" }, managers.Single(manager => manager.Id == ProjectManagerId).ManagerTypes);
        Assert.Equal(new[] { "PortfolioManager" }, managers.Single(manager => manager.Id == PortfolioManagerId).ManagerTypes);
        Assert.Equal(new[] { "ProgramManager" }, managers.Single(manager => manager.Id == ProgramManagerId).ManagerTypes);
        Assert.Equal(new[] { "ProjectManager", "ProgramManager" }, managers.Single(manager => manager.Id == BothManagerId).ManagerTypes);
        Assert.DoesNotContain(managers, manager => manager.Id == CurrentManagerId || manager.Id == OtherTenantManagerId || manager.Id == InactiveManagerId);
    }

    [Fact]
    public async Task GetEligibleManagersAsync_DoesNotReturnProjectFromAnotherTenant()
    {
        SetTenant(TenantB);

        var result = await _service.GetEligibleManagersAsync(ProjectId);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    private void SeedData()
    {
        var users = new[]
        {
            NewUser(CurrentManagerId, TenantA, "Current", "Manager"),
            NewUser(ProjectManagerId, TenantA, "Project", "Manager"),
            NewUser(PortfolioManagerId, TenantA, "Portfolio", "Manager"),
            NewUser(ProgramManagerId, TenantA, "Program", "Manager"),
            NewUser(BothManagerId, TenantA, "Dual", "Manager"),
            NewUser(OtherTenantManagerId, TenantB, "Other", "Tenant"),
            NewUser(InactiveManagerId, TenantA, "Inactive", "Manager", isActive: false),
        };
        var projectRole = new Role { Id = Guid.NewGuid(), TenantId = TenantA, Name = "project.manager", RowVersion = new byte[8] };
        var portfolioRole = new Role { Id = Guid.NewGuid(), TenantId = TenantA, Name = "portfolio.manager", RowVersion = new byte[8] };
        var inactiveRole = new Role { Id = Guid.NewGuid(), TenantId = TenantA, Name = "project.manager", RowVersion = new byte[8] };
        var projectAssignments = new[]
        {
            NewUserRole(ProjectManagerId, projectRole.Id),
            NewUserRole(BothManagerId, projectRole.Id),
            NewUserRole(PortfolioManagerId, portfolioRole.Id),
            NewUserRole(InactiveManagerId, inactiveRole.Id),
        };
        var portfolio = new Portfolio { Id = Guid.NewGuid(), TenantId = TenantA, Name = "Portfolio A", Currency = "USD", RowVersion = new byte[8] };
        var program = new PortfolioProgram { Id = Guid.NewGuid(), TenantId = TenantA, Name = "Program A", PortfolioId = portfolio.Id, ProgramManagerId = ProgramManagerId, RowVersion = new byte[8] };
        var assignments = new[]
        {
            new ProgramManagerAssignment { Id = Guid.NewGuid(), TenantId = TenantA, ProgramId = program.Id, ProgramManagerId = ProgramManagerId, IsCurrent = true, StartDate = DateTime.UtcNow, RowVersion = new byte[8] },
            new ProgramManagerAssignment { Id = Guid.NewGuid(), TenantId = TenantA, ProgramId = program.Id, ProgramManagerId = BothManagerId, IsCurrent = true, StartDate = DateTime.UtcNow, RowVersion = new byte[8] },
            new ProgramManagerAssignment { Id = Guid.NewGuid(), TenantId = TenantA, ProgramId = program.Id, ProgramManagerId = OtherTenantManagerId, IsCurrent = true, StartDate = DateTime.UtcNow, RowVersion = new byte[8] },
        };

        _context.Users.AddRange(users);
        _context.Roles.AddRange(projectRole, portfolioRole, inactiveRole);
        _context.UserRoles.AddRange(projectAssignments);
        _context.Portfolios.Add(portfolio);
        _context.Programs.Add(program);
        _context.ProgramManagerAssignments.AddRange(assignments);
        _context.Projects.Add(new Project
        {
            Id = ProjectId,
            TenantId = TenantA,
            Name = "Project A",
            Code = "PA",
            ProjectManagerId = CurrentManagerId,
            CreatedBy = CurrentManagerId.ToString(),
            RowVersion = new byte[8]
        });
        _context.SaveChanges();
    }

    private static User NewUser(Guid id, Guid tenantId, string firstName, string lastName, bool isActive = true) => new()
    {
        Id = id,
        TenantId = tenantId,
        Email = $"{firstName.ToLowerInvariant()}@example.test",
        PasswordHash = "hash",
        FirstName = firstName,
        LastName = lastName,
        IsActive = isActive,
        RowVersion = new byte[8]
    };

    private static UserRole NewUserRole(Guid userId, Guid roleId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantA,
        UserId = userId,
        RoleId = roleId,
        RowVersion = new byte[8]
    };

    private void SetTenant(Guid tenantId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenantId", tenantId.ToString()) }, "test"));
        _httpContextAccessor.Setup(accessor => accessor.HttpContext).Returns(httpContext);
    }

    private sealed class HandoverTestDbContext(DbContextOptions<HandoverTestDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();
        public DbSet<Portfolio> Portfolios => Set<Portfolio>();
        public DbSet<PortfolioProgram> Programs => Set<PortfolioProgram>();
        public DbSet<ProgramManagerAssignment> ProgramManagerAssignments => Set<ProgramManagerAssignment>();
        public DbSet<Project> Projects => Set<Project>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().Ignore(user => user.UserRoles).Ignore(user => user.Sessions);
            modelBuilder.Entity<Role>().Ignore(role => role.UserRoles).Ignore(role => role.Permissions);
            modelBuilder.Entity<UserRole>().Ignore(userRole => userRole.User).Ignore(userRole => userRole.Role);
            modelBuilder.Entity<Portfolio>()
                .Ignore(portfolio => portfolio.Programs)
                .Ignore(portfolio => portfolio.HealthHistory)
                .Ignore(portfolio => portfolio.Budgets);
            modelBuilder.Entity<PortfolioProgram>().Ignore(program => program.Portfolio);
            modelBuilder.Entity<ProgramManagerAssignment>().Ignore(assignment => assignment.Program);
            modelBuilder.Entity<Project>()
                .Ignore(project => project.Members)
                .Ignore(project => project.Phases)
                .Ignore(project => project.Stakeholders)
                .Ignore(project => project.Milestones)
                .Ignore(project => project.Deliverables)
                .Ignore(project => project.Baselines)
                .Ignore(project => project.StatusReports);
        }
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
