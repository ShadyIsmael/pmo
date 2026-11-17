using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using PortfolioProgram = NEXUS.Modules.Portfolio.Domain.Program;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class ProjectHandoverManagerServiceTests : IDisposable
{
    private static readonly Guid TenantA = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid OrganizationA = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid OrganizationB = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid ProjectId = Guid.Parse("10000000-0000-0000-0000-000000000010");
    private static readonly Guid OtherProjectId = Guid.Parse("10000000-0000-0000-0000-000000000022");
    private static readonly Guid CurrentManagerId = Guid.Parse("10000000-0000-0000-0000-000000000011");
    private static readonly Guid ProjectManagerId = Guid.Parse("10000000-0000-0000-0000-000000000012");
    private static readonly Guid PortfolioManagerId = Guid.Parse("10000000-0000-0000-0000-000000000013");
    private static readonly Guid ProgramManagerId = Guid.Parse("10000000-0000-0000-0000-000000000014");
    private static readonly Guid BothManagerId = Guid.Parse("10000000-0000-0000-0000-000000000015");
    private static readonly Guid OtherTenantManagerId = Guid.Parse("20000000-0000-0000-0000-000000000016");
    private static readonly Guid InactiveManagerId = Guid.Parse("10000000-0000-0000-0000-000000000017");
    private static readonly Guid AdminId = Guid.Parse("10000000-0000-0000-0000-000000000018");
    private static readonly Guid NoApproveManagerId = Guid.Parse("10000000-0000-0000-0000-000000000019");
    private static readonly Guid UnassignedManagementCandidateId = Guid.Parse("10000000-0000-0000-0000-000000000020");
    private static readonly Guid NonManagementCandidateId = Guid.Parse("10000000-0000-0000-0000-000000000021");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HandoverTestDbContext _context;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IUserEffectivePermissionService> _effectivePermissions = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly ProjectHandoverRequestService _service;

    public ProjectHandoverManagerServiceTests()
    {
        _connection.Open();
        _context = new HandoverTestDbContext(new DbContextOptionsBuilder<HandoverTestDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        SeedData();
        _effectivePermissions
            .Setup(service => service.GetEffectivePermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userId, Guid _, CancellationToken _) =>
            {
                if (userId == AdminId) return new HashSet<string>(StringComparer.Ordinal) { "Handover.AdminInitiate" };
                if (userId == CurrentManagerId || userId == ProjectManagerId)
                    return new HashSet<string>(StringComparer.Ordinal) { "Handover.Manage", "Handover.Approve" };
                if (userId == PortfolioManagerId || userId == ProgramManagerId || userId == BothManagerId
                    || userId == OtherTenantManagerId || userId == InactiveManagerId
                    || userId == UnassignedManagementCandidateId || userId == NonManagementCandidateId)
                    return new HashSet<string>(StringComparer.Ordinal) { "Handover.Approve" };
                return new HashSet<string>(StringComparer.Ordinal);
            });
        _notifications
            .Setup(service => service.SendAsync(It.IsAny<NEXUS.Modules.Notifications.Domain.Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _audit
            .Setup(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _service = new ProjectHandoverRequestService(
            _context,
            new ProjectSnapshotService(_context, _audit.Object, _httpContextAccessor.Object, new ProjectManagerMembershipSynchronizer(_context)),
            _notifications.Object,
            _httpContextAccessor.Object,
            _effectivePermissions.Object);
    }

    [Fact]
    public async Task GetEligibleManagersAsync_ReturnsTenantProjectPortfolioAndProgramManagers_WithoutDuplicatesOrCurrentManager()
    {
        SetTenant(TenantA);

        var result = await _service.GetEligibleManagersAsync(ProjectId);

        Assert.True(result.IsSuccess);
        var managers = result.Value!;
        Assert.Equal(new[] { BothManagerId, PortfolioManagerId, ProgramManagerId, ProjectManagerId, UnassignedManagementCandidateId }, managers.Select(manager => manager.Id));
        Assert.Equal(new[] { "ProjectManager" }, managers.Single(manager => manager.Id == ProjectManagerId).ManagerTypes);
        Assert.Equal(new[] { "ProjectManager" }, managers.Single(manager => manager.Id == PortfolioManagerId).ManagerTypes);
        Assert.Equal(new[] { "ProjectManager" }, managers.Single(manager => manager.Id == ProgramManagerId).ManagerTypes);
        Assert.Equal(new[] { "ProjectManager" }, managers.Single(manager => manager.Id == BothManagerId).ManagerTypes);
        Assert.Equal(new[] { "ProjectManager" }, managers.Single(manager => manager.Id == UnassignedManagementCandidateId).ManagerTypes);
        Assert.DoesNotContain(managers, manager => manager.Id == CurrentManagerId || manager.Id == OtherTenantManagerId || manager.Id == InactiveManagerId || manager.Id == NoApproveManagerId);
        Assert.DoesNotContain(managers, manager => manager.Id == NonManagementCandidateId);
    }

    [Fact]
    public async Task GetEligibleManagersAsync_DoesNotReturnProjectFromAnotherTenant()
    {
        SetTenant(TenantB);

        var result = await _service.GetEligibleManagersAsync(ProjectId);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    [Fact]
    public async Task AdministratorCanRequestHandover_WithoutChangingCurrentManagerBeforeAcceptance()
    {
        SetTenant(TenantA, AdminId);

        var result = await _service.CreateAdminInitiatedAsync(ProjectId, new NEXUS.Modules.ProjectManagement.Application.DTOs.CreateProjectHandoverRequestDto
        {
            NewProjectManagerId = ProjectManagerId,
            EffectiveHandoverDate = DateTime.UtcNow.Date.AddDays(7),
            Reason = "Administrative reassignment"
        }, AdminId.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal("AdminInitiated", result.Value!.InitiationType);
        Assert.Equal(AdminId, result.Value.RequestedBy);
        Assert.Equal(CurrentManagerId, result.Value.CurrentProjectManagerId);
        Assert.Equal(CurrentManagerId, (await _context.Projects.SingleAsync(project => project.Id == ProjectId)).ProjectManagerId);
        Assert.Equal(ProjectHandoverRequest.Statuses.Pending, result.Value.Status);
    }

    [Fact]
    public async Task OrganizationRoleHandoverPermissions_AuthorizeWithinProjectOrganizationOnly()
    {
        var otherProject = new Project
        {
            Id = OtherProjectId,
            TenantId = TenantA,
            Name = "Project B",
            Code = "PB",
            ProjectManagerId = CurrentManagerId,
            CreatedBy = AdminId.ToString(),
            RowVersion = new byte[8]
        };
        _context.Projects.Add(otherProject);
        await _context.SaveChangesAsync();

        var projectOrganizations = new Mock<IProjectOrganizationLookup>();
        projectOrganizations
            .Setup(lookup => lookup.ResolveOrganizationIdAsync(TenantA, ProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrganizationA);
        projectOrganizations
            .Setup(lookup => lookup.ResolveOrganizationIdAsync(TenantA, OtherProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrganizationB);

        var scopedPermissions = new Mock<IUserEffectivePermissionService>();
        scopedPermissions
            .Setup(service => service.GetEffectivePermissionsForOrganizationAsync(
                It.IsAny<Guid>(), TenantA, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userId, Guid _, Guid organizationId, CancellationToken _) =>
            {
                if (organizationId != OrganizationA) return new HashSet<string>(StringComparer.Ordinal);
                if (userId == AdminId) return new HashSet<string>(StringComparer.Ordinal) { "Handover.View", "Handover.AdminInitiate" };
                if (userId == ProjectManagerId) return new HashSet<string>(StringComparer.Ordinal) { "Handover.View", "Handover.Approve" };
                if (userId == NoApproveManagerId) return new HashSet<string>(StringComparer.Ordinal) { "Handover.View" };
                if (userId == UnassignedManagementCandidateId) return new HashSet<string>(StringComparer.Ordinal) { "Handover.AdminInitiate" };
                return new HashSet<string>(StringComparer.Ordinal);
            });

        var scopedService = new ProjectHandoverRequestService(
            _context,
            new ProjectSnapshotService(_context, _audit.Object, _httpContextAccessor.Object, new ProjectManagerMembershipSynchronizer(_context)),
            _notifications.Object,
            _httpContextAccessor.Object,
            scopedPermissions.Object,
            projectOrganizationLookup: projectOrganizations.Object);

        SetTenant(TenantA, AdminId);
        var created = await scopedService.CreateAdminInitiatedAsync(ProjectId, new NEXUS.Modules.ProjectManagement.Application.DTOs.CreateProjectHandoverRequestDto
        {
            NewProjectManagerId = ProjectManagerId,
            EffectiveHandoverDate = DateTime.UtcNow.Date.AddDays(7),
            Reason = "Organization scoped transition"
        }, AdminId.ToString());
        Assert.True(created.IsSuccess, created.Error?.Message);

        SetTenant(TenantA, NoApproveManagerId);
        var viewOnlyList = await scopedService.GetForProjectAsync(ProjectId, NoApproveManagerId.ToString());
        Assert.True(viewOnlyList.IsSuccess, viewOnlyList.Error?.Message);
        Assert.Single(viewOnlyList.Value!);

        SetTenant(TenantA, UnassignedManagementCandidateId);
        var initiateWithoutView = await scopedService.GetForProjectAsync(ProjectId, UnassignedManagementCandidateId.ToString());
        Assert.True(initiateWithoutView.IsFailure);
        Assert.Equal(403, initiateWithoutView.Error?.StatusCode);

        SetTenant(TenantA, AdminId);
        var crossOrganizationList = await scopedService.GetForProjectAsync(OtherProjectId, AdminId.ToString());
        Assert.True(crossOrganizationList.IsFailure);
        Assert.Equal(403, crossOrganizationList.Error?.StatusCode);
        var crossOrganizationInitiation = await scopedService.CreateAdminInitiatedAsync(OtherProjectId, new NEXUS.Modules.ProjectManagement.Application.DTOs.CreateProjectHandoverRequestDto
        {
            NewProjectManagerId = ProjectManagerId,
            EffectiveHandoverDate = DateTime.UtcNow.Date.AddDays(7),
            Reason = "Should remain organization restricted"
        }, AdminId.ToString());
        Assert.True(crossOrganizationInitiation.IsFailure);
        Assert.Equal(403, crossOrganizationInitiation.Error?.StatusCode);

        SetTenant(TenantA, CurrentManagerId);
        var deniedInitiation = await scopedService.CreateAdminInitiatedAsync(ProjectId, new NEXUS.Modules.ProjectManagement.Application.DTOs.CreateProjectHandoverRequestDto
        {
            NewProjectManagerId = ProjectManagerId,
            EffectiveHandoverDate = DateTime.UtcNow.Date.AddDays(7),
            Reason = "No initiate grant"
        }, CurrentManagerId.ToString());
        Assert.True(deniedInitiation.IsFailure);
        Assert.Equal(403, deniedInitiation.Error?.StatusCode);
    }

    [Fact]
    public async Task AdministratorInitialAssignment_StaysUnassignedUntilIncomingManagerAccepts()
    {
        SetTenant(TenantA, AdminId);
        var project = await _context.Projects.SingleAsync(item => item.Id == ProjectId);
        project.ProjectManagerId = null;
        await _context.SaveChangesAsync();

        var result = await _service.CreateInitialAssignmentAsync(ProjectId, new NEXUS.Modules.ProjectManagement.Application.DTOs.CreateProjectHandoverRequestDto
        {
            NewProjectManagerId = ProjectManagerId,
            EffectiveHandoverDate = DateTime.UtcNow.Date,
            Reason = "Initial manager assignment"
        }, AdminId.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal("InitialAssignment", result.Value!.InitiationType);
        Assert.Null(result.Value.CurrentProjectManagerId);
        Assert.Null(project.ProjectManagerId);
        Assert.Null(result.Value.SnapshotId);
        Assert.Equal(ProjectHandoverRequest.Statuses.Pending, result.Value.Status);

        var accepted = await _service.AcceptAsync(result.Value.Id, ProjectManagerId.ToString());

        Assert.True(accepted.IsSuccess);
        Assert.Equal(ProjectHandoverRequest.Statuses.Accepted, accepted.Value!.Status);
        Assert.Equal(ProjectManagerId, (await _context.Projects.SingleAsync(item => item.Id == ProjectId)).ProjectManagerId);
        Assert.Null(accepted.Value.SnapshotId);
        var assignment = await _context.ProjectManagerAssignments.SingleAsync(item => item.ProjectId == ProjectId && item.IsCurrent);
        Assert.Equal(ProjectManagerId, assignment.ProjectManagerId);
        Assert.Null(assignment.HandoverFromProjectManagerId);
    }

    [Fact]
    public async Task AcceptAsync_RequiresIncomingManagerToRetainManagementCategoryRole()
    {
        SetTenant(TenantA, AdminId);
        var created = await _service.CreateAdminInitiatedAsync(ProjectId, new NEXUS.Modules.ProjectManagement.Application.DTOs.CreateProjectHandoverRequestDto
        {
            NewProjectManagerId = ProjectManagerId,
            EffectiveHandoverDate = DateTime.UtcNow.Date,
            Reason = "Administrative reassignment"
        }, AdminId.ToString());
        Assert.True(created.IsSuccess);

        var role = await _context.Roles.SingleAsync(item => item.Name == "delivery.lead" && item.TenantId == TenantA && !item.IsDeleted);
        role.Category = "Operation";
        await _context.SaveChangesAsync();

        var accepted = await _service.AcceptAsync(created.Value!.Id, ProjectManagerId.ToString());

        Assert.True(accepted.IsFailure);
        Assert.Equal(403, accepted.Error?.StatusCode);
        Assert.Equal(CurrentManagerId, (await _context.Projects.SingleAsync(item => item.Id == ProjectId)).ProjectManagerId);
    }

    [Fact]
    public async Task ManagerCannotInitiateHandoverForProjectTheyDoNotOwn()
    {
        SetTenant(TenantA, ProjectManagerId);

        var result = await _service.CreateAsync(ProjectId, new NEXUS.Modules.ProjectManagement.Application.DTOs.CreateProjectHandoverRequestDto
        {
            NewProjectManagerId = PortfolioManagerId,
            EffectiveHandoverDate = DateTime.UtcNow.Date.AddDays(7),
            Reason = "Unauthorized reassignment"
        }, ProjectManagerId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.Empty(await _context.Set<ProjectHandoverRequest>().ToListAsync());
    }

    [Fact]
    public async Task EligibleManagerMustHaveEffectiveApprovePermission()
    {
        SetTenant(TenantA);

        var result = await _service.GetEligibleManagersAsync(ProjectId);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value!, manager => manager.Id == NoApproveManagerId);
    }

    [Fact]
    public async Task AssignedIncomingProjectManager_CanReadOnlyReviewProjectHandover()
    {
        SetTenant(TenantA, CurrentManagerId);
        var created = await _service.CreateAsync(ProjectId, new NEXUS.Modules.ProjectManagement.Application.DTOs.CreateProjectHandoverRequestDto
        {
            NewProjectManagerId = ProjectManagerId,
            EffectiveHandoverDate = DateTime.UtcNow.Date.AddDays(7),
            Reason = "Transfer project ownership",
            Notes = "Review current delivery context"
        }, CurrentManagerId.ToString());
        Assert.True(created.IsSuccess);

        var review = await _service.GetApprovalReviewAsync(created.Value!.Id, ProjectManagerId.ToString());
        var unrelated = await _service.GetApprovalReviewAsync(created.Value.Id, PortfolioManagerId.ToString());

        Assert.True(review.IsSuccess);
        Assert.Equal("Project A", review.Value!.BusinessObjectName);
        Assert.Equal("Transfer project ownership", review.Value.Reason);
        Assert.Equal("Current Manager", review.Value.CurrentAssigneeName);
        Assert.Equal("Project Manager", review.Value.ProposedAssigneeName);
        Assert.True(review.Value.IsReadOnly);
        Assert.True(review.Value.CanDecide);
        Assert.True(unrelated.IsFailure);
        Assert.Equal(404, unrelated.Error?.StatusCode);
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
            NewUser(AdminId, TenantA, "Tenant", "Administrator"),
            NewUser(NoApproveManagerId, TenantA, "No", "Approver"),
            NewUser(UnassignedManagementCandidateId, TenantA, "Unassigned", "Lead"),
            NewUser(NonManagementCandidateId, TenantA, "Operations", "Lead"),
        };
        var managementRole = new Role { Id = Guid.NewGuid(), TenantId = TenantA, Name = "delivery.lead", Category = "Management", RowVersion = new byte[8] };
        var deletedManagementRole = new Role { Id = Guid.NewGuid(), TenantId = TenantA, Name = "former.lead", Category = "Management", IsDeleted = true, RowVersion = new byte[8] };
        var nonManagementRole = new Role { Id = Guid.NewGuid(), TenantId = TenantA, Name = "operations.lead", Category = "Operation", RowVersion = new byte[8] };
        var otherTenantManagementRole = new Role { Id = Guid.NewGuid(), TenantId = TenantB, Name = "delivery.lead", Category = "Management", RowVersion = new byte[8] };
        var projectAssignments = new[]
        {
            NewUserRole(ProjectManagerId, managementRole.Id),
            NewUserRole(BothManagerId, managementRole.Id),
            NewUserRole(PortfolioManagerId, managementRole.Id),
            NewUserRole(ProgramManagerId, managementRole.Id),
            NewUserRole(InactiveManagerId, deletedManagementRole.Id),
            NewUserRole(NoApproveManagerId, managementRole.Id),
            NewUserRole(UnassignedManagementCandidateId, managementRole.Id),
            NewUserRole(NonManagementCandidateId, nonManagementRole.Id),
            NewUserRole(OtherTenantManagerId, otherTenantManagementRole.Id, TenantB),
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
        _context.Roles.AddRange(managementRole, deletedManagementRole, nonManagementRole, otherTenantManagementRole);
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

    private static UserRole NewUserRole(Guid userId, Guid roleId, Guid? tenantId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId ?? TenantA,
        UserId = userId,
        RoleId = roleId,
        RowVersion = new byte[8]
    };

    private void SetTenant(Guid tenantId, Guid? userId = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, (userId ?? CurrentManagerId).ToString())
        }, "test"));
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
        public DbSet<ProjectManagerAssignment> ProjectManagerAssignments => Set<ProjectManagerAssignment>();
        public DbSet<ProjectStakeholder> ProjectStakeholders => Set<ProjectStakeholder>();
        public DbSet<ProjectHandoverRequest> ProjectHandoverRequests => Set<ProjectHandoverRequest>();
        public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
        public DbSet<Resource> Resources => Set<Resource>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().Ignore(user => user.UserRoles).Ignore(user => user.Sessions).Ignore(user => user.PermissionOverrides);
            modelBuilder.Entity<Role>().Ignore(role => role.UserRoles).Ignore(role => role.Permissions);
            modelBuilder.Entity<NEXUS.Modules.Identity.Domain.Permission>().Ignore(permission => permission.Roles).Ignore(permission => permission.UserOverrides);
            modelBuilder.Entity<UserPermissionOverride>().Ignore(item => item.User).Ignore(item => item.Permission);
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
            modelBuilder.Entity<ProjectHandoverRequest>()
                .Ignore(request => request.Project)
                .Ignore(request => request.Snapshot)
                .Property(request => request.RowVersion)
                .IsConcurrencyToken();
            modelBuilder.Entity<ProjectStakeholder>()
                .Ignore(stakeholder => stakeholder.Project)
                .Property(stakeholder => stakeholder.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
            modelBuilder.Entity<Resource>()
                .Ignore(resource => resource.Vendor)
                .Ignore(resource => resource.BaseCalendar)
                .Ignore(resource => resource.Skills)
                .Ignore(resource => resource.Allocations)
                .Ignore(resource => resource.CostProfiles);
            modelBuilder.Entity<ProjectManagerAssignment>()
                .Ignore(assignment => assignment.Project)
                .Property(assignment => assignment.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
            modelBuilder.Entity<AuditEntry>()
                .Ignore(entry => entry.Activities)
                .Property(entry => entry.RowVersion)
                .IsConcurrencyToken();
        }
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
