using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class ProjectServiceTests
{
    [Fact]
    public async Task GetPagedAsync_ReturnsLightweightSummariesWithoutLoadingProjectCollections()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var projectId = Guid.NewGuid();
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.Projects.Add(new Project
            {
                Id = projectId,
                Name = "List Project",
                Code = "PRJ-LIST",
                TenantId = tenantId,
                Members = new List<ProjectMember>
                {
                    new() { Id = Guid.NewGuid(), TenantId = tenantId, ResourceId = Guid.NewGuid(), Role = "TeamMember" }
                }
            });
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);
        var service = CreateProjectService(
            context, tenantId, Mock.Of<IAuditService>(), projectIds: new[] { projectId });

        var result = await service.GetPagedAsync(1, 20);

        result.Items.Should().ContainSingle().Which.Name.Should().Be("List Project");
        result.Items.Single().Should().BeOfType<ProjectSummaryDto>();
    }

    [Fact]
    public async Task GetShellAsync_DoesNotLoadProjectCollections()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var projectId = Guid.NewGuid();
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Shell Project",
                Code = "PRJ-SHELL",
                TenantId = tenantId,
                Members = new List<ProjectMember>
                {
                    new() { Id = Guid.NewGuid(), TenantId = tenantId, ResourceId = Guid.NewGuid(), Role = "TeamMember" }
                }
            });
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);
        var service = CreateProjectService(
            context, tenantId, Mock.Of<IAuditService>(), projectIds: new[] { projectId });

        var result = await service.GetShellAsync(projectId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Shell Project");
        result.Value.Members.Should().BeEmpty();
        context.ChangeTracker.Entries<ProjectMember>().Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_GeneratesUniqueProjectCode_WhenCodeIsNotProvided()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);
        context.Currencies.Add(new Currency { Code = "GBP", Name = "British Pound", Symbol = "£", IsBase = true, TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111") });
        context.Projects.Add(new Project { Id = Guid.NewGuid(), Name = "Existing Project", Code = "PRJ-1001", TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111") });
        await context.SaveChangesAsync();

        var auditService = new Mock<IAuditService>();
        auditService.Setup(service => service.LogAsync(It.IsAny<NEXUS.Modules.Audit.Domain.AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var service = CreateProjectService(context, tenantId, auditService.Object);

        var result = await service.CreateAsync(new CreateProjectDto
        {
            Name = "New Project",
            Currency = "GBP"
        }, "user-1");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("PRJ-1002");
        result.Value.ProjectManagerId.Should().BeNull();
        (await context.ProjectManagerAssignments.Where(assignment => assignment.ProjectId == result.Value.Id && !assignment.IsDeleted).ToListAsync())
            .Should().BeEmpty();
        (await context.ProjectMembers.Where(member => member.ProjectId == result.Value.Id && !member.IsDeleted).ToListAsync())
            .Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_PersistsSelectedInitialManagerThroughAssignmentHistory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111211");
        using var trustedTenantContext = TrustedTenantExecutionContext.Enter(tenantId);
        var managerId = Guid.Parse("11111111-1111-1111-1111-111111111212");
        var managerRole = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Name = "delivery.lead", Category = "Management", RowVersion = new byte[8] };
        context.Currencies.Add(new Currency { Code = "GBP", Name = "British Pound", Symbol = "£", IsBase = true, TenantId = tenantId });
        context.Users.Add(new User
        {
            Id = managerId,
            TenantId = tenantId,
            Email = "manager@example.test",
            PasswordHash = "test-hash",
            FirstName = "Project",
            LastName = "Manager",
            IsActive = true,
            RowVersion = new byte[8]
        });
        context.Roles.Add(managerRole);
        context.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = managerId,
            RoleId = managerRole.Id,
            RowVersion = new byte[8]
        });
        await context.SaveChangesAsync();

        var auditService = new Mock<IAuditService>();
        auditService.Setup(service => service.LogAsync(It.IsAny<NEXUS.Modules.Audit.Domain.AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var effectivePermissions = new Mock<IUserEffectivePermissionService>();
        effectivePermissions
            .Setup(service => service.GetEffectivePermissionsAsync(managerId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { "Handover.Approve" });
        var service = CreateProjectService(context, tenantId, auditService.Object, effectivePermissions.Object);

        var result = await service.CreateAsync(new CreateProjectDto
        {
            Name = "Initially Assigned Project",
            Currency = "GBP",
            ProjectManagerId = managerId
        }, "creator");

        result.IsSuccess.Should().BeTrue(result.Error?.Message ?? "Expected initial assignment create to succeed.");
        result.Value!.ProjectManagerId.Should().Be(managerId);
        var persistedProject = await context.Projects.SingleAsync(project => project.Id == result.Value.Id);
        persistedProject.ProjectManagerId.Should().Be(managerId);
        var assignment = await context.ProjectManagerAssignments.SingleAsync(item =>
            item.ProjectId == result.Value.Id && item.IsCurrent && !item.IsDeleted);
        assignment.ProjectManagerId.Should().Be(managerId);
        (await context.ProjectManagerAssignments.CountAsync(item => item.ProjectId == result.Value.Id && item.IsCurrent && !item.IsDeleted))
            .Should().Be(1);
        (await context.ProjectMembers.Where(member => member.ProjectId == result.Value.Id && !member.IsDeleted).ToListAsync())
            .Should().BeEmpty();
        (await context.Resources.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_RejectsActiveSameTenantUserWhoIsNotAnEligibleManager()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111213");
        var ineligibleUserId = Guid.Parse("11111111-1111-1111-1111-111111111214");
        context.Currencies.Add(new Currency { Code = "GBP", Name = "British Pound", Symbol = "£", IsBase = true, TenantId = tenantId });
        context.Users.Add(new User
        {
            Id = ineligibleUserId,
            TenantId = tenantId,
            Email = "staff@example.test",
            PasswordHash = "test-hash",
            FirstName = "Active",
            LastName = "Staff",
            IsActive = true,
            RowVersion = new byte[8]
        });
        await context.SaveChangesAsync();
        var effectivePermissions = new Mock<IUserEffectivePermissionService>();
        effectivePermissions
            .Setup(service => service.GetEffectivePermissionsAsync(ineligibleUserId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { "Handover.Approve" });
        var service = CreateProjectService(context, tenantId, Mock.Of<IAuditService>(), effectivePermissions.Object);

        var result = await service.CreateAsync(new CreateProjectDto
        {
            Name = "Invalid Initial Assignment",
            Currency = "GBP",
            ProjectManagerId = ineligibleUserId
        }, "creator");

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(400);
        (await context.Projects.ToListAsync()).Should().BeEmpty();
        (await context.ProjectManagerAssignments.ToListAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateAsync_RejectsCraftedManagerChangeForAssignedAndUnassignedProjects(bool hasCurrentManager)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111221");
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        var currentManagerId = Guid.Parse("11111111-1111-1111-1111-111111111222");
        var requestedManagerId = Guid.Parse("11111111-1111-1111-1111-111111111223");
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Governed Project",
            Code = "PRJ-GOV",
            ProjectManagerId = hasCurrentManager ? currentManagerId : null
        };
        context.Projects.Add(project);
        if (hasCurrentManager)
        {
            context.ProjectManagerAssignments.Add(new ProjectManagerAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = project.Id,
                ProjectManagerId = currentManagerId,
                StartDate = new DateTime(2026, 1, 1),
                IsCurrent = true,
                RowVersion = new byte[8]
            });
        }
        await context.SaveChangesAsync();
        var service = CreateProjectService(context, tenantId, Mock.Of<IAuditService>(), projectIds: new[] { project.Id });

        var result = await service.UpdateAsync(project.Id, new UpdateProjectDto
        {
            Name = "Attempted edit with manager replacement",
            ProjectManagerId = requestedManagerId
        }, "editor");

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(409);
        var persistedProject = await context.Projects.SingleAsync(item => item.Id == project.Id);
        persistedProject.ProjectManagerId.Should().Be(hasCurrentManager ? currentManagerId : null);
        persistedProject.Name.Should().Be("Governed Project");
        var persistedManagerAssignments = await context.ProjectManagerAssignments
            .Where(item => item.ProjectId == project.Id && !item.IsDeleted)
            .Select(item => item.ProjectManagerId)
            .ToArrayAsync();
        persistedManagerAssignments.Should().Equal(hasCurrentManager ? new[] { currentManagerId } : Array.Empty<Guid>());
    }

    [Fact]
    public async Task UpdateAsync_RejectsExplicitNullManagerClear()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111224");
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        var currentManagerId = Guid.Parse("11111111-1111-1111-1111-111111111225");
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Governed Project",
            Code = "PRJ-GOV-CLEAR",
            ProjectManagerId = currentManagerId
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        var service = CreateProjectService(context, tenantId, Mock.Of<IAuditService>(), projectIds: new[] { project.Id });

        var result = await service.UpdateAsync(project.Id, new UpdateProjectDto
        {
            Name = "Attempted manager clear",
            ProjectManagerId = null
        }, "editor");

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(409);
        var persistedProject = await context.Projects.SingleAsync(item => item.Id == project.Id);
        persistedProject.ProjectManagerId.Should().Be(currentManagerId);
        persistedProject.Name.Should().Be("Governed Project");
    }

    [Fact]
    public async Task UpdateAsync_RejectsLegacyHandoverPayload()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111231");
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        var currentManagerId = Guid.Parse("11111111-1111-1111-1111-111111111232");
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Governed Project",
            Code = "PRJ-GOV",
            ProjectManagerId = currentManagerId
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        var service = CreateProjectService(context, tenantId, Mock.Of<IAuditService>(), projectIds: new[] { project.Id });

        var result = await service.UpdateAsync(project.Id, new UpdateProjectDto
        {
            Name = "Attempted legacy handover",
            Handover = new ProjectHandoverDto { HandoverDate = DateTime.UtcNow.Date, HandoverReason = "bypass" }
        }, "editor");

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(409);
        (await context.Projects.SingleAsync(item => item.Id == project.Id)).ProjectManagerId.Should().Be(currentManagerId);
        (await context.Projects.SingleAsync(item => item.Id == project.Id)).Name.Should().Be("Governed Project");
    }

    [Fact]
    public async Task ProjectService_RejectsSameTenantProjectsOutsideManagementScope()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111241");
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await using var context = new AppDbContext(options);
        var visibleProjectId = Guid.NewGuid();
        var hiddenProjectId = Guid.NewGuid();
        var hiddenMemberId = Guid.NewGuid();
        var hiddenTeamId = Guid.NewGuid();
        context.Projects.AddRange(
            new Project { Id = visibleProjectId, TenantId = tenantId, Name = "Visible project", Code = "PRJ-VIS" },
            new Project { Id = hiddenProjectId, TenantId = tenantId, Name = "Hidden project", Code = "PRJ-HID" });
        context.Teams.Add(new Team { Id = hiddenTeamId, TenantId = tenantId, Name = "Hidden team", IsActive = true, RowVersion = new byte[8] });
        context.ProjectMembers.Add(new ProjectMember
        {
            Id = hiddenMemberId,
            TenantId = tenantId,
            ProjectId = hiddenProjectId,
            TeamId = hiddenTeamId,
            Role = "TeamMember",
            AllocationPercentage = 60m,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Status = "Active",
            RowVersion = new byte[8]
        });
        await context.SaveChangesAsync();
        var service = CreateProjectService(
            context, tenantId, Mock.Of<IAuditService>(), projectIds: new[] { visibleProjectId });

        var update = await service.UpdateAsync(hiddenProjectId, new UpdateProjectDto { Name = "Changed" }, "editor");
        var delete = await service.DeleteAsync(hiddenProjectId, "editor");
        var team = await service.GetTeamAsync(hiddenProjectId);
        var history = await service.GetManagerHistoryAsync(hiddenProjectId);
        var groupAssignment = await service.IsTeamGroupAssignmentAsync(hiddenProjectId, hiddenMemberId);
        var addMember = await service.AddTeamMemberAsync(hiddenProjectId, new CreateProjectMemberDto
        {
            ResourceId = Guid.NewGuid(),
            Role = "TeamMember",
            AllocationPercentage = 20m,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31)
        }, "editor");
        var updateMember = await service.UpdateTeamMemberAsync(hiddenProjectId, hiddenMemberId,
            new UpdateProjectMemberDto { AllocationPercentage = 10m }, "editor");
        var removeMember = await service.RemoveTeamMemberAsync(hiddenProjectId, hiddenMemberId, "editor");
        var conflicts = await service.CheckResourceConflictsAsync(Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 20m, null,
            projectId: hiddenProjectId);

        update.Error!.StatusCode.Should().Be(404);
        delete.Error!.StatusCode.Should().Be(404);
        team.Error!.StatusCode.Should().Be(404);
        history.Error!.StatusCode.Should().Be(404);
        groupAssignment.Should().BeFalse();
        addMember.Error!.StatusCode.Should().Be(404);
        updateMember.Error!.StatusCode.Should().Be(404);
        removeMember.Error!.StatusCode.Should().Be(404);
        conflicts.Error!.StatusCode.Should().Be(404);

        var persistedProject = await context.Projects.SingleAsync(project => project.Id == hiddenProjectId);
        persistedProject.Name.Should().Be("Hidden project");
        persistedProject.IsDeleted.Should().BeFalse();
        var persistedMember = await context.ProjectMembers.SingleAsync(member => member.Id == hiddenMemberId);
        persistedMember.AllocationPercentage.Should().Be(60m);
        persistedMember.IsDeleted.Should().BeFalse();
        (await context.ProjectMembers.CountAsync(member => member.ProjectId == hiddenProjectId)).Should().Be(1);
    }

    [Fact]
    public async Task CheckResourceConflictsAsync_RedactsAllocationsOutsideManagementScope()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111242");
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await using var context = new AppDbContext(options);
        var visibleProjectId = Guid.NewGuid();
        var hiddenProjectId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        context.Projects.AddRange(
            new Project { Id = visibleProjectId, TenantId = tenantId, Name = "Visible project", Code = "PRJ-VIS" },
            new Project { Id = hiddenProjectId, TenantId = tenantId, Name = "Confidential project", Code = "PRJ-SECRET" });
        context.Resources.Add(new Resource
        {
            Id = resourceId,
            TenantId = tenantId,
            Name = "Confidential resource",
            Type = "Employee",
            Status = "Active",
            IsActive = true,
            RowVersion = new byte[8]
        });
        context.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = hiddenProjectId,
            ResourceId = resourceId,
            Role = "Confidential role",
            AllocationPercentage = 70m,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Status = "Active",
            Notes = "Confidential allocation",
            RowVersion = new byte[8]
        });
        await context.SaveChangesAsync();
        var service = CreateProjectService(
            context, tenantId, Mock.Of<IAuditService>(), projectIds: new[] { visibleProjectId });

        var result = await service.CheckResourceConflictsAsync(resourceId,
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31), 40m, null,
            projectId: visibleProjectId);
        var addWithHiddenConflict = await service.AddTeamMemberAsync(visibleProjectId, new CreateProjectMemberDto
        {
            ResourceId = resourceId,
            Role = "TeamMember",
            AllocationPercentage = 40m,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 31)
        }, "editor");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().ContainSingle();
        var restricted = result.Value!.Single();
        restricted.IsRestrictedConflict.Should().BeTrue();
        restricted.AllocationId.Should().Be(Guid.Empty);
        restricted.ResourceName.Should().BeEmpty();
        restricted.ProjectName.Should().BeEmpty();
        restricted.Role.Should().BeEmpty();
        restricted.AllocationPercentage.Should().Be(0m);
        restricted.Notes.Should().BeNull();
        addWithHiddenConflict.IsFailure.Should().BeTrue();
        var addFailureMessage = addWithHiddenConflict.Error!.Message;
        addFailureMessage.Should().Contain("another project");
        addFailureMessage.Should().NotContain("Confidential project");
        addFailureMessage.Should().NotContain("Confidential resource");
        addFailureMessage.Should().NotContain("Confidential role");
    }

    private static ProjectService CreateProjectService(
        AppDbContext context,
        Guid tenantId,
        IAuditService auditService,
        IUserEffectivePermissionService? effectivePermissions = null,
        IReadOnlyCollection<Guid>? projectIds = null)
    {
        var actorId = Guid.Parse("11111111-1111-1111-1111-111111111299");
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("tenantId", tenantId.ToString()),
                    new Claim(ClaimTypes.NameIdentifier, actorId.ToString())
                }, "Test"))
            }
        };
        var management = new Mock<IManagementContextAccessor>();
        management.SetupGet(accessor => accessor.Current).Returns(new ManagementContext(
            actorId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), projectIds?.ToHashSet() ?? new HashSet<Guid>())));
        return new ProjectService(
            context,
            auditService,
            httpContextAccessor,
            new ProjectManagerMembershipSynchronizer(context),
            management.Object,
            effectivePermissions: effectivePermissions);
    }
}
