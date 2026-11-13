using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class ProjectLifecycleDocumentCategoryScopeTests : IDisposable
{
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    public async Task Create_phase_rejects_category_outside_scope_or_unavailable(
        bool categoryUsesOtherOrganization,
        bool categoryIsActive,
        bool categoryIsDeleted)
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var categoryTenantId = tenantId;
        var categoryOrganizationId = categoryUsesOtherOrganization ? Guid.NewGuid() : organizationId;
        var (db, http, management) = CreateContext(tenantId, organizationId);
        await using var context = db;
        context.Add(new Organization { Id = organizationId, TenantId = tenantId, Name = "Phase organization", CreatedBy = "test" });
        if (categoryOrganizationId != organizationId)
            context.Add(new Organization { Id = categoryOrganizationId, TenantId = categoryTenantId, Name = "Category organization", CreatedBy = "test" });
        var categoryId = Guid.NewGuid();
        context.Add(new DocumentCategory
        {
            Id = categoryId, TenantId = categoryTenantId, OrganizationId = categoryOrganizationId,
            Name = "Reference", NormalizedNameEn = "REFERENCE", NameAr = "مرجع", NormalizedNameAr = "مرجع",
            IsActive = categoryIsActive, IsDeleted = categoryIsDeleted, CreatedBy = "test"
        });
        await context.SaveChangesAsync();

        var service = new ProjectLifecycleCatalogService(
            context, http, Mock.Of<IAuditService>(), management);
        var result = await service.CreatePhaseAsync(new ProjectLifecycleCatalogRequest(
            OrganizationId: organizationId,
            Code: "phase-reference",
            NameEn: "Reference phase",
            NameAr: "مرحلة مرجعية",
            DescriptionEn: null,
            DescriptionAr: null,
            DisplayOrder: 20,
            IsDefault: false,
            IsActive: true,
            DocumentCategoryId: categoryId), Guid.Parse(http.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!));

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(400);
        (await context.Set<NEXUS.Modules.ProjectManagement.Domain.ProjectPhaseCatalogEntry>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_phase_accepts_active_category_in_exact_organization_scope()
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var (db, http, management) = CreateContext(tenantId, organizationId);
        await using var context = db;
        context.AddRange(
            new Organization { Id = organizationId, TenantId = tenantId, Name = "Phase organization", CreatedBy = "test" },
            new DocumentCategory
            {
                Id = categoryId, TenantId = tenantId, OrganizationId = organizationId,
                Name = "Reference", NormalizedNameEn = "REFERENCE", NameAr = "مرجع", NormalizedNameAr = "مرجع",
                IsActive = true, CreatedBy = "test"
            });
        await context.SaveChangesAsync();
        var audit = new Mock<IAuditService>();
        audit.Setup(item => item.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var service = new ProjectLifecycleCatalogService(context, http, audit.Object, management);

        var result = await service.CreatePhaseAsync(new ProjectLifecycleCatalogRequest(
            OrganizationId: organizationId,
            Code: "phase-reference",
            NameEn: "Reference phase",
            NameAr: "مرحلة مرجعية",
            DescriptionEn: null,
            DescriptionAr: null,
            DisplayOrder: 20,
            IsDefault: false,
            IsActive: true,
            DocumentCategoryId: categoryId), Guid.Parse(http.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!));

        result.IsSuccess.Should().BeTrue();
        result.Value!.DocumentCategoryId.Should().Be(categoryId);
    }

    [Fact]
    public async Task Tenant_scoped_phase_rejects_category_from_another_tenant_even_when_both_scopes_are_tenant_wide()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = $"project-lifecycle-category-{Guid.NewGuid():N}";
        var (db, http, management) = CreateContext(tenantId, organizationId: null, databaseName);
        await using var context = db;
        var categoryId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var foreignUserId = Guid.NewGuid();
        var foreignHttp = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, foreignUserId.ToString()),
                    new Claim("tenantId", foreignTenantId.ToString())
                ], "foreign-category-seed"))
            }
        };
        AppDbContext.ConfigureTenantResolution(foreignHttp);
        try
        {
            await using var foreignContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            foreignContext.Add(new DocumentCategory
            {
                Id = categoryId, TenantId = foreignTenantId, OrganizationId = null,
                Name = "Foreign", NormalizedNameEn = "FOREIGN", IsActive = true, CreatedBy = "test"
            });
            await foreignContext.SaveChangesAsync();
        }
        finally
        {
            AppDbContext.ConfigureTenantResolution(http);
        }
        var service = new ProjectLifecycleCatalogService(
            context, http, Mock.Of<IAuditService>(), management);

        var result = await service.CreatePhaseAsync(new ProjectLifecycleCatalogRequest(
            OrganizationId: null,
            Code: "tenant-phase-reference",
            NameEn: "Tenant reference phase",
            NameAr: "مرحلة مرجعية للمستأجر",
            DescriptionEn: null,
            DescriptionAr: null,
            DisplayOrder: 20,
            IsDefault: false,
            IsActive: true,
            DocumentCategoryId: categoryId), Guid.Parse(http.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!));

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(400);
        (await context.Set<ProjectPhaseCatalogEntry>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Tenant_scoped_phase_accepts_only_tenant_scoped_category()
    {
        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var (db, http, management) = CreateContext(tenantId, organizationId: null);
        await using var context = db;
        context.Add(new DocumentCategory
        {
            Id = categoryId, TenantId = tenantId, OrganizationId = null,
            Name = "Legacy", NormalizedNameEn = "LEGACY", IsActive = true, CreatedBy = "test"
        });
        await context.SaveChangesAsync();
        var audit = new Mock<IAuditService>();
        audit.Setup(item => item.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var service = new ProjectLifecycleCatalogService(context, http, audit.Object, management);

        var result = await service.CreatePhaseAsync(new ProjectLifecycleCatalogRequest(
            OrganizationId: null,
            Code: "tenant-phase-reference",
            NameEn: "Tenant reference phase",
            NameAr: "مرحلة مرجعية للمستأجر",
            DescriptionEn: null,
            DescriptionAr: null,
            DisplayOrder: 20,
            IsDefault: false,
            IsActive: true,
            DocumentCategoryId: categoryId), Guid.Parse(http.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!));

        result.IsSuccess.Should().BeTrue();
        result.Value!.DocumentCategoryId.Should().Be(categoryId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Update_phase_can_preserve_an_existing_inactive_or_deleted_category(
        bool categoryIsActive, bool categoryIsDeleted)
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var phaseId = Guid.NewGuid();
        var (db, http, management) = CreateContext(tenantId, organizationId, includePhaseEdit: true);
        await using var context = db;
        var category = new DocumentCategory
        {
            Id = categoryId, TenantId = tenantId, OrganizationId = organizationId,
            Name = "Reference", NormalizedNameEn = "REFERENCE", NameAr = "مرجع", NormalizedNameAr = "مرجع",
            IsActive = categoryIsActive, IsDeleted = categoryIsDeleted, CreatedBy = "test"
        };
        var phase = new ProjectPhaseCatalogEntry
        {
            Id = phaseId, TenantId = tenantId, OrganizationId = organizationId,
            Code = "phase-reference", NormalizedCode = "PHASE-REFERENCE",
            NameEn = "Reference phase", NameAr = "مرحلة مرجعية", DisplayOrder = 20,
            IsActive = true, IsDefault = true, DocumentCategoryId = categoryId,
            RowVersion = [1], CreatedBy = "test"
        };
        context.AddRange(
            new Organization { Id = organizationId, TenantId = tenantId, Name = "Phase organization", CreatedBy = "test" },
            category,
            phase);
        await context.SaveChangesAsync();
        phase.RowVersion = [1];
        var audit = new Mock<IAuditService>();
        audit.Setup(item => item.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var service = new ProjectLifecycleCatalogService(context, http, audit.Object, management);

        var result = await service.UpdatePhaseAsync(phaseId, new ProjectLifecycleCatalogRequest(
            OrganizationId: organizationId,
            Code: phase.Code,
            NameEn: "Renamed phase",
            NameAr: "مرحلة معدلة",
            DescriptionEn: null,
            DescriptionAr: null,
            DisplayOrder: phase.DisplayOrder,
            IsDefault: phase.IsDefault,
            IsActive: true,
            DocumentCategoryId: categoryId,
            RowVersion: [1]), Guid.Parse(http.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!));

        result.IsSuccess.Should().BeTrue();
        result.Value!.DocumentCategoryId.Should().Be(categoryId);
    }

    [Fact]
    public async Task Update_phase_can_clear_an_existing_inactive_category()
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var phaseId = Guid.NewGuid();
        var (db, http, management) = CreateContext(tenantId, organizationId, includePhaseEdit: true);
        await using var context = db;
        var phase = new ProjectPhaseCatalogEntry
        {
            Id = phaseId, TenantId = tenantId, OrganizationId = organizationId,
            Code = "phase-reference", NormalizedCode = "PHASE-REFERENCE",
            NameEn = "Reference phase", NameAr = "مرحلة مرجعية", DisplayOrder = 20,
            IsActive = true, IsDefault = true, DocumentCategoryId = categoryId,
            RowVersion = [1], CreatedBy = "test"
        };
        context.AddRange(
            new Organization { Id = organizationId, TenantId = tenantId, Name = "Phase organization", CreatedBy = "test" },
            new DocumentCategory
            {
                Id = categoryId, TenantId = tenantId, OrganizationId = organizationId,
                Name = "Reference", NormalizedNameEn = "REFERENCE", IsActive = false, CreatedBy = "test"
            },
            phase);
        await context.SaveChangesAsync();
        phase.RowVersion = [1];
        var audit = new Mock<IAuditService>();
        audit.Setup(item => item.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var service = new ProjectLifecycleCatalogService(context, http, audit.Object, management);

        var result = await service.UpdatePhaseAsync(phaseId, new ProjectLifecycleCatalogRequest(
            OrganizationId: organizationId,
            Code: phase.Code,
            NameEn: phase.NameEn,
            NameAr: phase.NameAr,
            DescriptionEn: null,
            DescriptionAr: null,
            DisplayOrder: phase.DisplayOrder,
            IsDefault: phase.IsDefault,
            IsActive: true,
            DocumentCategoryId: null,
            RowVersion: [1]), Guid.Parse(http.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!));

        result.IsSuccess.Should().BeTrue();
        result.Value!.DocumentCategoryId.Should().BeNull();
    }

    public void Dispose() => AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());

    private static (AppDbContext Db, HttpContextAccessor Http, ManagementContextAccessor Management) CreateContext(
        Guid tenantId, Guid? organizationId, string? databaseName = null, bool includePhaseEdit = false)
    {
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("tenantId", tenantId.ToString()),
            new("permission", ProjectLifecyclePermissions.PhaseCreate)
        };
        if (includePhaseEdit)
            claims.Add(new Claim("permission", ProjectLifecyclePermissions.PhaseEdit));
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "lifecycle-category-test"))
            }
        };
        AppDbContext.ConfigureTenantResolution(http);
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"project-lifecycle-category-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        var organizationIds = organizationId.HasValue ? new HashSet<Guid> { organizationId.Value } : new HashSet<Guid>();
        var organizationReferences = organizationId.HasValue
            ? new[] { new ManagementEntityReference(organizationId.Value, "Phase organization") }
            : Array.Empty<ManagementEntityReference>();
        var scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>())
        {
            OrganizationIds = organizationIds,
            OrganizationReferences = organizationReferences
        };
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            userId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, null, scope));
        return (db, http, management);
    }
}
