using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.Persistence;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class UserRepositoryManagerCandidateTests
{
    [Fact]
    public async Task Tenant_scoped_user_queries_return_only_active_same_tenant_system_users()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var activeId = Guid.NewGuid();
        var inactiveId = Guid.NewGuid();
        var crossTenantId = Guid.NewGuid();
        var managerRole = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "portfolio.manager",
            RowVersion = new byte[8]
        };

        await using var context = new AppDbContext(options);
        context.Roles.Add(managerRole);
        context.Users.AddRange(
            new User
            {
                Id = activeId, TenantId = tenantId, Email = "active@example.test",
                PasswordHash = "test-hash", FirstName = "Active", LastName = "Manager",
                IsActive = true, RowVersion = new byte[8]
            },
            new User
            {
                Id = inactiveId, TenantId = tenantId, Email = "inactive@example.test",
                PasswordHash = "test-hash", FirstName = "Inactive", LastName = "Manager",
                IsActive = false, RowVersion = new byte[8]
            },
            new User
            {
                Id = crossTenantId, TenantId = otherTenantId, Email = "other-tenant@example.test",
                PasswordHash = "test-hash", FirstName = "Other", LastName = "Tenant",
                IsActive = true, RowVersion = new byte[8]
            });
        context.UserRoles.AddRange(
            new UserRole { Id = Guid.NewGuid(), TenantId = tenantId, UserId = activeId, RoleId = managerRole.Id, RowVersion = new byte[8] },
            new UserRole { Id = Guid.NewGuid(), TenantId = tenantId, UserId = inactiveId, RoleId = managerRole.Id, RowVersion = new byte[8] });
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);

        var activeTenantUsers = await repository.GetActiveInTenantAsync(tenantId);
        var portfolioManagers = await repository.GetByRoleNameInTenantAsync("portfolio.manager", tenantId);

        Assert.Equal([activeId], activeTenantUsers.Select(user => user.Id));
        Assert.Equal([activeId], portfolioManagers.Select(user => user.Id));
        Assert.DoesNotContain(inactiveId, activeTenantUsers.Select(user => user.Id));
        Assert.DoesNotContain(crossTenantId, activeTenantUsers.Select(user => user.Id));
        Assert.DoesNotContain(inactiveId, portfolioManagers.Select(user => user.Id));
        Assert.DoesNotContain(crossTenantId, portfolioManagers.Select(user => user.Id));
    }

    [Fact]
    public async Task Role_category_query_returns_only_active_same_tenant_users_with_active_management_roles()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var eligibleId = Guid.NewGuid();
        var wrongCategoryId = Guid.NewGuid();
        var deletedRoleId = Guid.NewGuid();
        var otherTenantIdUser = Guid.NewGuid();
        var managementRole = new Role
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "delivery.lead", Category = "Management", RowVersion = new byte[8]
        };
        var wrongCategoryRole = new Role
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "delivery.lead", Category = "Operation", RowVersion = new byte[8]
        };
        var deletedManagementRole = new Role
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "former.lead", Category = "Management", IsDeleted = true, RowVersion = new byte[8]
        };
        var otherTenantManagementRole = new Role
        {
            Id = Guid.NewGuid(), TenantId = otherTenantId, Name = "delivery.lead", Category = "Management", RowVersion = new byte[8]
        };

        await using var context = new AppDbContext(options);
        context.Roles.AddRange(managementRole, wrongCategoryRole, deletedManagementRole, otherTenantManagementRole);
        context.Users.AddRange(
            new User { Id = eligibleId, TenantId = tenantId, Email = "eligible@example.test", FirstName = "Eligible", LastName = "Lead", IsActive = true, RowVersion = new byte[8] },
            new User { Id = wrongCategoryId, TenantId = tenantId, Email = "wrong@example.test", FirstName = "Wrong", LastName = "Category", IsActive = true, RowVersion = new byte[8] },
            new User { Id = deletedRoleId, TenantId = tenantId, Email = "deleted@example.test", FirstName = "Deleted", LastName = "Role", IsActive = true, RowVersion = new byte[8] },
            new User { Id = otherTenantIdUser, TenantId = otherTenantId, Email = "other@example.test", FirstName = "Other", LastName = "Tenant", IsActive = true, RowVersion = new byte[8] });
        context.UserRoles.AddRange(
            new UserRole { Id = Guid.NewGuid(), TenantId = tenantId, UserId = eligibleId, RoleId = managementRole.Id, RowVersion = new byte[8] },
            new UserRole { Id = Guid.NewGuid(), TenantId = tenantId, UserId = wrongCategoryId, RoleId = wrongCategoryRole.Id, RowVersion = new byte[8] },
            new UserRole { Id = Guid.NewGuid(), TenantId = tenantId, UserId = deletedRoleId, RoleId = deletedManagementRole.Id, RowVersion = new byte[8] },
            new UserRole { Id = Guid.NewGuid(), TenantId = otherTenantId, UserId = otherTenantIdUser, RoleId = otherTenantManagementRole.Id, RowVersion = new byte[8] });
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);
        var candidates = await repository.GetByRoleCategoryInTenantAsync("Management", tenantId);

        Assert.Equal([eligibleId], candidates.Select(user => user.Id));
    }
}
