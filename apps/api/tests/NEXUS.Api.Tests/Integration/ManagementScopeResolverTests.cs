using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Modules.Audit.Application;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Application;
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
    private static readonly Guid OrganizationRoleUserId = Guid.Parse("c1000000-0000-0000-0000-000000000011");
    private static readonly Guid PortfolioId = Guid.Parse("c1000000-0000-0000-0000-000000000020");
    private static readonly Guid OtherPortfolioId = Guid.Parse("c1000000-0000-0000-0000-000000000023");
    private static readonly Guid InheritedProgramId = Guid.Parse("c1000000-0000-0000-0000-000000000021");
    private static readonly Guid DirectProgramId = Guid.Parse("c1000000-0000-0000-0000-000000000022");
    private static readonly Guid InheritedProjectId = Guid.Parse("c1000000-0000-0000-0000-000000000030");
    private static readonly Guid DirectProjectId = Guid.Parse("c1000000-0000-0000-0000-000000000031");
    private static readonly Guid SponsorOnlyProjectId = Guid.Parse("c1000000-0000-0000-0000-000000000032");
    private static readonly Guid ProgramOnlyProjectId = Guid.Parse("c1000000-0000-0000-0000-000000000033");
    private static readonly Guid OrganizationId = Guid.Parse("c1000000-0000-0000-0000-000000000040");
    private static readonly Guid DepartmentOrganizationId = Guid.Parse("c1000000-0000-0000-0000-000000000041");
    private static readonly Guid DepartmentBusinessUnitId = Guid.Parse("c1000000-0000-0000-0000-000000000042");
    private static readonly Guid DepartmentId = Guid.Parse("c1000000-0000-0000-0000-000000000043");
    private static readonly Guid DepartmentProjectId = Guid.Parse("c1000000-0000-0000-0000-000000000044");
    private static readonly Guid CrossTenantSponsorProjectId = Guid.Parse("c2000000-0000-0000-0000-000000000032");
    private static readonly Guid OtherTenantId = Guid.Parse("c2000000-0000-0000-0000-000000000001");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly ManagementScopeResolver _resolver;

    public ManagementScopeResolverTests()
    {
        _connection.Open();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("tenantId", TenantId.ToString()) }, "tests"));
        var tenantAccessor = new HttpContextAccessor { HttpContext = httpContext };
        AppDbContext.ConfigureTenantResolution(tenantAccessor);
        _db = new SqliteTestAppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _db.Users.Add(new User
        {
            Id = UserId, TenantId = TenantId, Email = "manager@example.test", FirstName = "Scoped",
            LastName = "Manager", IsActive = true, PasswordHash = "test-hash", CreatedBy = "tests",
            RowVersion = new byte[8]
        });
        _db.Organizations.AddRange(
            new Organization
            {
                Id = OrganizationId, TenantId = TenantId, Name = "Project Scope Organization", IsActive = true, CreatedBy = "tests"
            },
            new Organization
            {
                Id = DepartmentOrganizationId, TenantId = TenantId, Name = "Department Scope Organization", IsActive = true, CreatedBy = "tests"
            });
        _db.Portfolios.AddRange(
            new Portfolio
            {
                Id = PortfolioId, TenantId = TenantId, Name = "Direct Portfolio", Code = "DIR-P",
                Currency = "USD", OwnerId = UserId, OrganizationId = OrganizationId, Status = "Active", CreatedBy = "tests",
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
        var departmentProject = Project(DepartmentProjectId, "Department Project", "DEP-PRJ", null, null);
        departmentProject.DepartmentId = DepartmentId;
        _db.Projects.AddRange(
            Project(InheritedProjectId, "Inherited Project", "INH-P", PortfolioId, InheritedProgramId),
            Project(DirectProjectId, "Direct Project", "DIR-PRJ", null, null),
            Project(ProgramOnlyProjectId, "Program Only Project", "PRG-ONLY", null, InheritedProgramId),
            departmentProject,
            SponsorProject(SponsorOnlyProjectId, TenantId, "Sponsor Only Project", "SPN-PRJ"));
        var departmentBusinessUnit = new BusinessUnit
        {
            Id = DepartmentBusinessUnitId, TenantId = TenantId, OrganizationId = DepartmentOrganizationId,
            Name = "Department Scope Unit", IsActive = true, CreatedBy = "tests"
        };
        _db.BusinessUnits.Add(departmentBusinessUnit);
        _db.Departments.Add(new Department
        {
            Id = DepartmentId, TenantId = TenantId, BusinessUnitId = DepartmentBusinessUnitId,
            Name = "Department Scope Department", IsActive = true, CreatedBy = "tests"
        });
        _db.SaveChanges();

        var otherTenantHttpContext = new DefaultHttpContext();
        otherTenantHttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("tenantId", OtherTenantId.ToString()) }, "tests"));
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor { HttpContext = otherTenantHttpContext });
        _db.Projects.Add(new Project
        {
            Id = CrossTenantSponsorProjectId, PublicId = Guid.NewGuid(), TenantId = OtherTenantId,
            Name = "Cross Tenant Sponsor Project", Code = "X-TEN-SPN", Status = "Active",
            CreatedBy = "tests", RowVersion = new byte[8]
        });
        _db.SaveChanges();
        AppDbContext.ConfigureTenantResolution(tenantAccessor);

        _resolver = new ManagementScopeResolver(_db, new NEXUS.Modules.Identity.Infrastructure.UserEffectivePermissionService(_db), new NEXUS.Persistence.ProjectOrganizationLookup(_db));
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

    [Fact]
    public async Task SponsorAssignment_GrantsOnlyItsTenantProjectScopeWithoutAResourceMembership()
    {
        var snapshot = await _resolver.GetPerspectiveSnapshotAsync(UserId, TenantId);
        var projectContexts = snapshot.AvailablePerspectives
            .Single(x => x.Perspective == ManagementPerspective.ProjectManager).Contexts;

        Assert.Contains(projectContexts, x => x.Id == SponsorOnlyProjectId);
        Assert.Contains(SponsorOnlyProjectId, snapshot.Scope.ProjectIds);
        Assert.True(await _resolver.CanAccessProjectAsync(UserId, TenantId, SponsorOnlyProjectId));
        Assert.False(await _resolver.CanAccessProjectAsync(UserId, TenantId, CrossTenantSponsorProjectId));

        var context = await _resolver.ResolveAsync(
            UserId, TenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            portfolioId: null, programId: null, projectId: SponsorOnlyProjectId);
        Assert.NotNull(context);
        Assert.Contains(SponsorOnlyProjectId, context!.Scope.ProjectIds);

        var managementContextAccessor = new ManagementContextAccessor();
        managementContextAccessor.Set(context!);
        var projectHttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim("tenantId", TenantId.ToString()),
                    new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
                    new Claim("permission", "Project.View")
                }, "tests"))
        };
        var projectService = new ProjectService(
            _db,
            Mock.Of<IAuditService>(),
            new HttpContextAccessor { HttpContext = projectHttpContext },
            new ProjectManagerMembershipSynchronizer(_db),
            managementContextAccessor);

        var page = await projectService.GetPagedAsync(1, 20);
        var shell = await projectService.GetShellAsync(SponsorOnlyProjectId);

        Assert.Contains(page.Items, project => project.Id == SponsorOnlyProjectId);
        Assert.True(shell.IsSuccess);
        Assert.Equal(SponsorOnlyProjectId, shell.Value!.Id);
        Assert.Empty(await _db.Set<NEXUS.Modules.ResourceManagement.Domain.Resource>()
            .Where(resource => resource.UserId == UserId).ToListAsync());
    }

    [Fact]
    public async Task Project_context_inherits_organization_from_program_portfolio_when_project_portfolio_is_null()
    {
        var context = await _resolver.ResolveAsync(
            UserId, TenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            portfolioId: null, programId: null, projectId: ProgramOnlyProjectId);

        Assert.NotNull(context);
        Assert.Equal(PortfolioId, Assert.Single(context!.Scope.PortfolioIds));
        Assert.Equal(OrganizationId, context.Scope.ProjectOrganizationIds[ProgramOnlyProjectId]);
        Assert.Contains(OrganizationId, context.Scope.OrganizationIds);
        var organizationReference = Assert.Single(context.Scope.OrganizationReferences);
        Assert.Equal(OrganizationId, organizationReference.Id);
        Assert.Equal("Project Scope Organization", organizationReference.Name);
    }

    [Fact]
    public async Task Direct_department_project_assignment_contributes_its_organization_to_scope()
    {
        var snapshot = await _resolver.GetPerspectiveSnapshotAsync(UserId, TenantId);

        Assert.Contains(DepartmentOrganizationId, snapshot.Scope.OrganizationIds);
        Assert.Contains(snapshot.Scope.OrganizationReferences, item => item.Id == DepartmentOrganizationId);

        var context = await _resolver.ResolveAsync(
            UserId, TenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            portfolioId: null, programId: null, projectId: DepartmentProjectId);

        Assert.NotNull(context);
        Assert.Contains(DepartmentOrganizationId, context!.Scope.OrganizationIds);
        Assert.Equal(DepartmentOrganizationId, context.Scope.ProjectOrganizationIds[DepartmentProjectId]);
    }

    [Fact]
    public async Task Organization_role_project_view_exposes_only_projects_owned_by_its_organization()
    {
        var user = new User
        {
            Id = OrganizationRoleUserId,
            TenantId = TenantId,
            Email = "organization-role-user@example.test",
            FirstName = "Organization",
            LastName = "Viewer",
            IsActive = true,
            PasswordHash = "test-hash",
            CreatedBy = "tests",
            RowVersion = new byte[8]
        };
        var permission = new Permission
        {
            TenantId = TenantId,
            Name = "Project.View",
            Resource = "Project",
            Action = "View",
            CreatedBy = "tests"
        };
        var organizationRole = new Role
        {
            TenantId = TenantId,
            Name = "NEXUS Group Project Viewer",
            CreatedBy = "tests"
        };
        organizationRole.AssignOrganization(OrganizationId);
        organizationRole.Permissions.Add(permission);
        user.UserRoles.Add(new UserRole
        {
            TenantId = TenantId,
            User = user,
            Role = organizationRole,
            CreatedBy = "tests"
        });
        _db.Users.Add(user);
        _db.Roles.Add(organizationRole);
        _db.Permissions.Add(permission);
        await _db.SaveChangesAsync();

        var snapshot = await _resolver.GetPerspectiveSnapshotAsync(OrganizationRoleUserId, TenantId);
        Assert.Contains(ManagementPerspective.ProjectManager, snapshot.NativePerspectives);
        Assert.Contains(InheritedProjectId, snapshot.Scope.ProjectIds);
        Assert.Contains(ProgramOnlyProjectId, snapshot.Scope.ProjectIds);
        Assert.DoesNotContain(DepartmentProjectId, snapshot.Scope.ProjectIds);
        Assert.DoesNotContain(DirectProjectId, snapshot.Scope.ProjectIds);
        Assert.Contains(OrganizationId, snapshot.Scope.OrganizationIds);
        Assert.DoesNotContain(DepartmentOrganizationId, snapshot.Scope.OrganizationIds);

        var context = await _resolver.ResolveAsync(
            OrganizationRoleUserId, TenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            portfolioId: null, programId: null, projectId: InheritedProjectId);
        Assert.NotNull(context);
        var managementContext = new ManagementContextAccessor();
        managementContext.Set(context!);
        var httpContext = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                new[]
                {
                    new System.Security.Claims.Claim("tenantId", TenantId.ToString()),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, OrganizationRoleUserId.ToString()),
                    new System.Security.Claims.Claim("permission", "Project.View")
                }, "tests"))
        };
        var projectService = new ProjectService(
            _db,
            Mock.Of<IAuditService>(),
            new HttpContextAccessor { HttpContext = httpContext },
            new ProjectManagerMembershipSynchronizer(_db),
            managementContext,
            projectOrganizationLookup: new NEXUS.Persistence.ProjectOrganizationLookup(_db));

        var page = await projectService.GetPagedAsync(1, 20);
        Assert.Contains(page.Items, project => project.Id == InheritedProjectId);
        Assert.Contains(page.Items, project => project.Id == ProgramOnlyProjectId);
        Assert.DoesNotContain(page.Items, project => project.Id == DepartmentProjectId);
        Assert.True((await projectService.GetShellAsync(InheritedProjectId)).IsSuccess);
        Assert.True((await projectService.GetShellAsync(DepartmentProjectId)).IsFailure);
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

    private static Project SponsorProject(Guid id, Guid tenantId, string name, string code) => new()
    {
        Id = id, PublicId = Guid.NewGuid(), TenantId = tenantId, Name = name, Code = code,
        SponsorId = UserId, Status = "Active", CreatedBy = "tests", RowVersion = new byte[8]
    };

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
