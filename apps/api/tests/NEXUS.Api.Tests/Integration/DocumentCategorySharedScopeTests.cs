using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class DocumentCategorySharedScopeTests : IDisposable
{
    [Fact]
    public async Task Tenant_shared_seed_categories_are_listed_for_an_authorized_organization_and_can_be_edited_in_place()
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var foreignOrganizationId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sharedCategoryId = Guid.NewGuid();
        var organizationCategoryId = Guid.NewGuid();
        var outOfScopeCategoryId = Guid.NewGuid();
        var foreignTenantCategoryId = Guid.NewGuid();
        var http = CreateHttpContext(tenantId, userId);
        var originalHttpContext = http.HttpContext!;
        var databaseName = $"document-category-shared-{Guid.NewGuid():N}";
        AppDbContext.ConfigureTenantResolution(http);
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        var organization = new Organization { Id = organizationId, TenantId = tenantId, Name = "Authorized organization", CreatedBy = "test" };
        context.AddRange(
            organization,
            new Organization { Id = foreignOrganizationId, TenantId = tenantId, Name = "Other organization", CreatedBy = "test" },
            new DocumentCategory
            {
                Id = sharedCategoryId, TenantId = tenantId, OrganizationId = null, CreatedBy = "System",
                Code = "GOV", Name = "Governance", NormalizedNameEn = "GOVERNANCE", IsActive = true
            },
            new DocumentCategory
            {
                Id = organizationCategoryId, TenantId = tenantId, OrganizationId = organizationId, CreatedBy = "test",
                Name = "Organization", NormalizedNameEn = "ORGANIZATION", NameAr = "تنظيم", NormalizedNameAr = "تنظيم", IsActive = true
            },
            new DocumentCategory
            {
                Id = outOfScopeCategoryId, TenantId = tenantId, OrganizationId = foreignOrganizationId, CreatedBy = "test",
                Name = "Other", NormalizedNameEn = "OTHER", NameAr = "أخرى", NormalizedNameAr = "أخرى", IsActive = true
            });
        await context.SaveChangesAsync();

        var foreignHttp = CreateHttpContext(foreignTenantId, Guid.NewGuid());
        AppDbContext.ConfigureTenantResolution(foreignHttp);
        try
        {
            await using var foreignContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            foreignContext.Add(new DocumentCategory
            {
                Id = foreignTenantCategoryId, TenantId = foreignTenantId, OrganizationId = null, CreatedBy = "test",
                Name = "Foreign", NormalizedNameEn = "FOREIGN", IsActive = true
            });
            await foreignContext.SaveChangesAsync();
        }
        finally
        {
            http.HttpContext = originalHttpContext;
            AppDbContext.ConfigureTenantResolution(http);
        }

        var scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>())
        {
            OrganizationIds = new HashSet<Guid> { organizationId },
            OrganizationReferences = [new ManagementEntityReference(organizationId, organization.Name)]
        };
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            userId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, null, scope));
        var audit = new Mock<IAuditService>();
        audit.Setup(item => item.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        http.HttpContext!.User.Identity!.IsAuthenticated.Should().BeTrue();
        http.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(userId.ToString());
        http.HttpContext.User.FindFirstValue("tenantId").Should().Be(tenantId.ToString());
        management.Current.Should().NotBeNull();
        management.Current!.UserId.Should().Be(userId);
        management.Current.TenantId.Should().Be(tenantId);
        var service = new DocumentService(context, Mock.Of<IStorageProvider>(), http, audit.Object, management);

        var categories = await service.GetCategoriesAsync(organizationId);
        categories.IsSuccess.Should().BeTrue(categories.Error is { } categoryError
            ? $"{categoryError.StatusCode} {categoryError.Code}: {categoryError.Message}"
            : "category lookup returned failure");
        categories.Value!.Select(category => category.Id).Should().BeEquivalentTo(new[] { sharedCategoryId, organizationCategoryId });

        var templateOptions = await service.GetTemplateCategoryOptionsAsync(organizationId);
        templateOptions.IsSuccess.Should().BeTrue();
        templateOptions.Value!.Select(category => category.Id).Should().BeEquivalentTo(new[] { sharedCategoryId, organizationCategoryId });

        var managedCategories = await service.GetManagedCategoriesAsync(organizationId, null, includeInactive: true);
        managedCategories.IsSuccess.Should().BeTrue();
        managedCategories.Value!.Single(category => category.Id == sharedCategoryId).CanManage.Should().BeTrue();

        var update = await service.UpdateCategoryAsync(sharedCategoryId,
            new DocumentCategoryUpdateRequest("Governance Records", "سجلات الحوكمة", "Updated system category"), userId);
        update.IsSuccess.Should().BeTrue();
        update.Value!.Id.Should().Be(sharedCategoryId);
        update.Value.OrganizationId.Should().BeNull();
        update.Value.NameEn.Should().Be("Governance Records");
        update.Value.CanManage.Should().BeTrue();

        var category = await context.Set<DocumentCategory>().SingleAsync(item => item.Id == sharedCategoryId);
        category.CreatedBy.Should().Be("System");
        category.OrganizationId.Should().BeNull();
        category.Name.Should().Be("Governance Records");

        var status = await service.SetCategoryStatusAsync(sharedCategoryId, false, userId);
        status.IsSuccess.Should().BeTrue();
        status.Value!.IsActive.Should().BeFalse();

        var outOfScopeUpdate = await service.UpdateCategoryAsync(outOfScopeCategoryId,
            new DocumentCategoryUpdateRequest("Changed", "تم التغيير"), userId);
        outOfScopeUpdate.IsFailure.Should().BeTrue();
        outOfScopeUpdate.Error!.StatusCode.Should().Be(404);

        var foreignTenantUpdate = await service.UpdateCategoryAsync(foreignTenantCategoryId,
            new DocumentCategoryUpdateRequest("Changed", "تم التغيير"), userId);
        foreignTenantUpdate.IsFailure.Should().BeTrue();
        foreignTenantUpdate.Error!.StatusCode.Should().Be(404);
        audit.Verify(item => item.LogAsync(It.Is<AuditEntry>(entry => entry.EntityId == sharedCategoryId), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    public void Dispose() => AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());

    private static HttpContextAccessor CreateHttpContext(Guid tenantId, Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("tenantId", tenantId.ToString()),
            new("permission", DocumentPermissions.View),
            new("permission", DocumentPermissions.Create),
            new("permission", DocumentPermissions.TemplateManage),
            new("permission", DocumentCategoryPermissions.View),
            new("permission", DocumentCategoryPermissions.Edit),
            new("permission", DocumentCategoryPermissions.ActivateDeactivate)
        };
        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "document-category-test"))
            }
        };
    }
}
