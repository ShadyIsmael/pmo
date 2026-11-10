using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Api.Finance;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Finance;

public sealed class ProjectAnnualBudgetCandidateEligibilityTests
{
    [Fact]
    public async Task Active_management_and_system_team_members_and_eligible_sponsor_can_be_ordered_and_submitted()
    {
        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var managementUserId = Guid.NewGuid();
        var projectManagerId = Guid.NewGuid();
        var systemSponsorId = Guid.NewGuid();
        var multiRoleUserId = Guid.NewGuid();
        var businessUserId = Guid.NewGuid();
        var deniedUserId = Guid.NewGuid();
        var inactiveUserId = Guid.NewGuid();
        var foreignUserId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        await using var context = CreateContext(ignoreTransactionWarning: true);
        var managementRole = NewRole(tenantId, "Project Management", "Management");
        var systemRole = NewRole(tenantId, "Tenant Admin", "System", isSystem: true);
        systemRole.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, tenantId);
        var secondSystemRole = NewRole(tenantId, "System Reviewer", "System", isSystem: true);
        secondSystemRole.AssignSystemRoleKey("test.system.reviewer", tenantId);
        var businessRole = NewRole(tenantId, "Business", "Business");
        var foreignManagementRole = NewRole(foreignTenantId, "Foreign Management", "Management");

        var project = new Project
        {
            Id = projectId,
            TenantId = tenantId,
            Name = "Approval candidate project",
            Code = "CANDIDATES",
            Budget = 100_000m,
            Currency = "EGP",
            FinanceApprovalMode = "Both",
            ProjectManagerId = projectManagerId,
            SponsorId = systemSponsorId,
            RowVersion = [1]
        };
        var managementResource = Resource(tenantId, managementUserId, "Management member");
        var deniedResource = Resource(tenantId, deniedUserId, "Denied member");
        var systemResource = Resource(tenantId, multiRoleUserId, "System member");
        var businessResource = Resource(tenantId, businessUserId, "Business member");
        var inactiveResource = Resource(tenantId, inactiveUserId, "Inactive member");
        var foreignResource = Resource(foreignTenantId, foreignUserId, "Foreign member");
        var managementUser = User(tenantId, managementUserId, "Management", "Reviewer");
        var projectManager = User(tenantId, projectManagerId, "Project", "Manager");
        var sponsorUser = User(tenantId, systemSponsorId, "System", "Sponsor");
        var multiRoleUser = User(tenantId, multiRoleUserId, "Multi", "Role");
        var businessUser = User(tenantId, businessUserId, "Business", "Reviewer");
        var deniedUser = User(tenantId, deniedUserId, "Denied", "Reviewer");
        var inactiveUser = User(tenantId, inactiveUserId, "Inactive", "Reviewer");
        inactiveUser.IsActive = false;
        var foreignUser = User(foreignTenantId, foreignUserId, "Foreign", "Reviewer");

        context.AddRange(
            project,
            managementRole,
            systemRole,
            secondSystemRole,
            businessRole,
            foreignManagementRole,
            managementResource,
            deniedResource,
            systemResource,
            businessResource,
            inactiveResource,
            foreignResource,
            managementUser,
            sponsorUser,
            multiRoleUser,
            businessUser,
            inactiveUser,
            foreignUser,
            Membership(tenantId, projectId, managementResource.Id, today),
            Membership(tenantId, projectId, deniedResource.Id, today),
            Membership(tenantId, projectId, systemResource.Id, today),
            Membership(tenantId, projectId, businessResource.Id, today),
            Membership(tenantId, projectId, inactiveResource.Id, today),
            Membership(tenantId, projectId, foreignResource.Id, today),
            UserRole(tenantId, managementUser, managementRole),
            UserRole(tenantId, projectManager, managementRole),
            UserRole(tenantId, sponsorUser, systemRole),
            UserRole(tenantId, multiRoleUser, managementRole),
            UserRole(tenantId, multiRoleUser, secondSystemRole),
            UserRole(tenantId, businessUser, businessRole),
            UserRole(tenantId, deniedUser, managementRole),
            UserRole(tenantId, inactiveUser, managementRole),
            UserRole(foreignTenantId, foreignUser, foreignManagementRole));
        await context.SaveChangesAsync();

        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(item => item.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Permissions("ProjectSettings.View", "ProjectSettings.Edit", "ProjectAnnualBudget.View",
                "ProjectAnnualBudget.ManageApprovalConfiguration", "ProjectAnnualBudget.SubmitForReview"));
        foreach (var userId in new[] { managementUserId, projectManagerId, systemSponsorId, multiRoleUserId, businessUserId, inactiveUserId })
            permissions.Setup(item => item.GetEffectivePermissionsAsync(userId, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Permissions("ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"));
        permissions.Setup(item => item.GetEffectivePermissionsAsync(deniedUserId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EffectivePermissionResolver.Resolve(
                ["ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"],
                [("ProjectAnnualBudget.Approve", PermissionOverrideEffect.Deny)]));
        permissions.Setup(item => item.GetEffectivePermissionsAsync(foreignUserId, foreignTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Permissions("ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"));

        var workflow = CreateWorkflow(context, tenantId, projectId, actorId, permissions.Object);
        var configuration = await workflow.GetApprovalConfigurationAsync(tenantId, projectId, actorId, CancellationToken.None);

        Assert.True(configuration.IsSuccess, configuration.Error?.Message);
        var candidates = configuration.Value!.EligibleApprovers;
        Assert.Contains(candidates, candidate => candidate.UserId == managementUserId);
        Assert.Contains(candidates, candidate => candidate.UserId == projectManagerId);
        Assert.Contains(candidates, candidate => candidate.UserId == systemSponsorId);
        Assert.Contains(candidates, candidate => candidate.UserId == multiRoleUserId);
        Assert.Equal(["Management"], candidates.Single(candidate => candidate.UserId == managementUserId).EligibleRoleCategories);
        Assert.Equal(["Management"], candidates.Single(candidate => candidate.UserId == projectManagerId).EligibleRoleCategories);
        Assert.Equal(["System"], candidates.Single(candidate => candidate.UserId == systemSponsorId).EligibleRoleCategories);
        Assert.Equal(["Management", "System"], candidates.Single(candidate => candidate.UserId == multiRoleUserId).EligibleRoleCategories);
        Assert.Equal(1, candidates.Count(candidate => candidate.UserId == multiRoleUserId));
        Assert.Contains(candidates, candidate => candidate.UserId == businessUserId);
        Assert.Equal(["Business"], candidates.Single(candidate => candidate.UserId == businessUserId).EligibleRoleCategories);
        Assert.DoesNotContain(candidates, candidate => candidate.UserId == deniedUserId);
        Assert.DoesNotContain(candidates, candidate => candidate.UserId == inactiveUserId);
        Assert.DoesNotContain(candidates, candidate => candidate.UserId == foreignUserId);

        var saved = await workflow.SaveApprovalConfigurationAsync(
            tenantId,
            projectId,
            actorId,
            "Project Settings Admin",
            new SaveProjectAnnualBudgetApprovalConfigurationDto(
                "Sequential", [managementUserId, projectManagerId, systemSponsorId], null, project.RowVersion),
            CancellationToken.None);

        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.Equal([managementUserId, projectManagerId, systemSponsorId], saved.Value!.ConfiguredApprovers.Select(item => item.UserId));
        Assert.Equal([1, 2, 3], saved.Value.ConfiguredApprovers.Select(item => item.Position));

        var storedConfiguration = await context.Set<ProjectAnnualBudgetApprovalConfiguration>()
            .IgnoreQueryFilters().SingleAsync(item => item.ProjectId == projectId);
        storedConfiguration.RowVersion = [2];
        var budget = new ProjectAnnualBudget
        {
            TenantId = tenantId,
            ProjectId = projectId,
            FiscalYear = today.Year,
            FiscalYearStartDate = new DateOnly(today.Year, 1, 1),
            FiscalYearEndDate = new DateOnly(today.Year, 12, 31),
            FinancialReference = "CANDIDATE-FY-DRAFT",
            Currency = "EGP",
            NewApprovedFunding = 20_000m,
            Status = ProjectAnnualBudgetStatuses.Draft,
            RowVersion = [3]
        };
        context.ProjectAnnualBudgets.Add(budget);
        await context.SaveChangesAsync();

        var reloaded = await workflow.GetApprovalConfigurationAsync(tenantId, projectId, actorId, CancellationToken.None);
        Assert.True(reloaded.IsSuccess, reloaded.Error?.Message);
        var submitted = await workflow.SubmitForReviewAsync(
            tenantId,
            projectId,
            budget.Id,
            actorId,
            "Project Settings Admin",
            new SubmitProjectAnnualBudgetReviewDto(
                null,
                null,
                budget.RowVersion,
                ApprovalConfigurationRowVersion: reloaded.Value!.RowVersion,
                ProjectSettingsRowVersion: reloaded.Value.ProjectSettingsRowVersion),
            CancellationToken.None);

        Assert.True(submitted.IsSuccess, submitted.Error?.Message);
        var approval = await context.ApprovalRequests.IgnoreQueryFilters()
            .SingleAsync(item => item.EntityId == budget.Id && item.Type == "ProjectAnnualBudgetReview");
        Assert.Equal(managementUserId, approval.CurrentApproverId);
        var assignments = await context.PaymentRequestApproverAssignments.IgnoreQueryFilters()
            .Where(item => item.ApprovalRequestId == approval.Id)
            .OrderBy(item => item.Position)
            .ToArrayAsync();
        Assert.Equal([managementUserId, projectManagerId, systemSponsorId], assignments.Select(item => item.ApproverUserId));
        Assert.Equal([1, 2, 3], assignments.Select(item => item.Position));
        Assert.Equal(actorId, approval.RequestedBy);
    }

    [Fact]
    public async Task Candidate_role_membership_does_not_replace_the_exact_approval_permission()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        await using var context = CreateContext();
        var project = new Project
        {
            Id = projectId,
            TenantId = tenantId,
            Name = "Approval permission project",
            Code = "APPROVAL-AUTH",
            FinanceApprovalMode = "Sequential",
            Currency = "EGP",
            Budget = 50_000m,
            RowVersion = [1]
        };
        var reviewer = User(tenantId, reviewerId, "Role", "Only");
        var managementRole = NewRole(tenantId, "Management Reviewer", "Management");
        var resource = Resource(tenantId, reviewerId, "Management reviewer");
        context.AddRange(project, reviewer, managementRole, resource,
            Membership(tenantId, projectId, resource.Id, today),
            UserRole(tenantId, reviewer, managementRole));
        await context.SaveChangesAsync();

        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(item => item.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Permissions("ProjectSettings.View", "ProjectSettings.Edit", "ProjectAnnualBudget.View",
                "ProjectAnnualBudget.ManageApprovalConfiguration", "ProjectAnnualBudget.SubmitForReview"));
        permissions.Setup(item => item.GetEffectivePermissionsAsync(reviewerId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Permissions());
        var workflow = CreateWorkflow(context, tenantId, projectId, actorId, permissions.Object);

        var candidates = await workflow.GetApprovalConfigurationAsync(tenantId, projectId, actorId, CancellationToken.None);

        Assert.True(candidates.IsSuccess, candidates.Error?.Message);
        Assert.DoesNotContain(candidates.Value!.EligibleApprovers, candidate => candidate.UserId == reviewerId);
    }

    private static AppDbContext CreateContext(bool ignoreTransactionWarning = false)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());
        if (ignoreTransactionWarning)
            options.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        return new AppDbContext(options.Options);
    }

    private static ProjectAnnualBudgetWorkflow CreateWorkflow(
        AppDbContext context,
        Guid tenantId,
        Guid projectId,
        Guid actorId,
        IUserEffectivePermissionService permissions)
    {
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            actorId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            projectId,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));

        var fiscalYear = new Mock<ITenantFiscalYearService>();
        var now = DateTimeOffset.UtcNow;
        fiscalYear.Setup(item => item.ResolveForTimestampAsync(tenantId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiscalYearCalendarService().ForFiscalYear(now.Year, 1));
        fiscalYear.Setup(item => item.GetStartMonthAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        return new ProjectAnnualBudgetWorkflow(
            context,
            fiscalYear.Object,
            new FiscalYearCalendarService(),
            permissions,
            management,
            Mock.Of<IAuditService>(),
            new NoopProjectAnnualBudgetApprovalNotificationPublisher(),
            Mock.Of<IManagementScopeResolver>());
    }

    private static User User(Guid tenantId, Guid userId, string firstName, string lastName) => new()
    {
        Id = userId,
        TenantId = tenantId,
        Email = $"{userId:N}@example.test",
        FirstName = firstName,
        LastName = lastName,
        IsActive = true
    };

    private static Role NewRole(Guid tenantId, string name, string category, bool isSystem = false) => new()
    {
        TenantId = tenantId,
        Name = name,
        Category = category,
        IsSystem = isSystem
    };

    private static UserRole UserRole(Guid tenantId, User user, Role role) => new()
    {
        TenantId = tenantId,
        UserId = user.Id,
        User = user,
        RoleId = role.Id,
        Role = role
    };

    private static Resource Resource(Guid tenantId, Guid userId, string name) => new()
    {
        TenantId = tenantId,
        UserId = userId,
        Name = name,
        Type = "Employee",
        Status = "Active",
        IsActive = true
    };

    private static ProjectMember Membership(Guid tenantId, Guid projectId, Guid resourceId, DateTime today) => new()
    {
        TenantId = tenantId,
        ProjectId = projectId,
        ResourceId = resourceId,
        Role = "Finance Reviewer",
        Status = "Active",
        StartDate = today.AddDays(-1),
        EndDate = today.AddDays(1)
    };

    private static HashSet<string> Permissions(params string[] permissions) =>
        permissions.ToHashSet(StringComparer.Ordinal);
}
