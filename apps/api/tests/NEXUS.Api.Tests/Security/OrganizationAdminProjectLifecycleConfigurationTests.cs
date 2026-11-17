using NEXUS.Api.Management;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class OrganizationAdminProjectLifecycleConfigurationTests : IDisposable
{
    [Fact]
    public async Task Organization_configuration_permissions_manage_lifecycle_catalog_only_in_the_granted_organization()
    {
        var fixture = CreateFixture();
        await using var db = fixture.Db;
        await SeedOrganizationsAndPreInitiationAsync(fixture);

        var phases = await fixture.Service.GetPhasesAsync(
            fixture.OrganizationId, includeInactive: false, search: null);
        Assert.True(phases.IsSuccess, phases.Error?.Message);
        Assert.Contains(phases.Value!, phase => phase.OrganizationId == fixture.OrganizationId
            && phase.Code == "pre-initiation");
        Assert.DoesNotContain(phases.Value!, phase => phase.OrganizationId == fixture.OtherOrganizationId);

        var firstStatus = await fixture.Service.CreateStatusAsync(
            Request(fixture.OrganizationId, "org-status-default", "Organization default", isDefault: true),
            fixture.ActorId);
        Assert.True(firstStatus.IsSuccess, firstStatus.Error?.Message);
        Assert.Equal(fixture.OrganizationId, firstStatus.Value!.OrganizationId);
        Assert.True(firstStatus.Value.IsDefault);

        var secondStatus = await fixture.Service.CreateStatusAsync(
            Request(fixture.OrganizationId, "org-status-next", "Organization next", isDefault: false),
            fixture.ActorId);
        Assert.True(secondStatus.IsSuccess, secondStatus.Error?.Message);
        Assert.Equal(fixture.OrganizationId, secondStatus.Value!.OrganizationId);

        var secondEntity = await db.ProjectStatusCatalogEntries.SingleAsync(item => item.Id == secondStatus.Value.Id);
        secondEntity.RowVersion = [1];
        var updated = await fixture.Service.UpdateStatusAsync(secondEntity.Id,
            Request(fixture.OrganizationId, secondEntity.Code, "Organization next updated", isDefault: false) with
            {
                RowVersion = [1]
            }, fixture.ActorId);
        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.Equal("Organization next updated", updated.Value!.NameEn);
        Assert.Equal(fixture.OrganizationId, updated.Value.OrganizationId);

        secondEntity.RowVersion = [1];
        var selectedDefault = await fixture.Service.SetStatusDefaultAsync(
            secondEntity.Id, [1], fixture.ActorId);
        Assert.True(selectedDefault.IsSuccess, selectedDefault.Error?.Message);
        Assert.True((await db.ProjectStatusCatalogEntries.SingleAsync(item => item.Id == secondEntity.Id)).IsDefault);
        Assert.False((await db.ProjectStatusCatalogEntries.SingleAsync(item => item.Id == firstStatus.Value.Id)).IsDefault);

        var options = await fixture.Service.GetOptionsAsync(fixture.OrganizationId, projectId: null);
        Assert.True(options.IsFailure);
        Assert.Equal(403, options.Error!.StatusCode);
    }

    [Fact]
    public async Task Organization_lifecycle_mutations_deny_other_organizations_and_tenant_scope()
    {
        var fixture = CreateFixture();
        await using var db = fixture.Db;
        await SeedOrganizationsAndPreInitiationAsync(fixture);
        var foreignOrganizationStatus = new ProjectStatusCatalogEntry
        {
            TenantId = fixture.TenantId,
            OrganizationId = fixture.OtherOrganizationId,
            Code = "other-org-status",
            NormalizedCode = "OTHER-ORG-STATUS",
            NameEn = "Other organization status",
            NameAr = "حالة منظمة أخرى",
            IsActive = true,
            CreatedBy = fixture.ActorId.ToString("D")
        };
        var tenantStatus = new ProjectStatusCatalogEntry
        {
            TenantId = fixture.TenantId,
            OrganizationId = null,
            Code = "tenant-status",
            NormalizedCode = "TENANT-STATUS",
            NameEn = "Tenant status",
            NameAr = "حالة المستأجر",
            IsActive = true,
            CreatedBy = fixture.ActorId.ToString("D")
        };
        db.ProjectStatusCatalogEntries.AddRange(foreignOrganizationStatus, tenantStatus);
        await db.SaveChangesAsync();

        var otherOrganizationCreate = await fixture.Service.CreateStatusAsync(
            Request(fixture.OtherOrganizationId, "other-create", "Unauthorized organization"), fixture.ActorId);
        Assert.True(otherOrganizationCreate.IsFailure);
        Assert.Equal(404, otherOrganizationCreate.Error!.StatusCode);

        var tenantCreate = await fixture.Service.CreateStatusAsync(
            Request(null, "tenant-create", "Unauthorized tenant configuration"), fixture.ActorId);
        Assert.True(tenantCreate.IsFailure);
        Assert.Equal(403, tenantCreate.Error!.StatusCode);

        var otherOrganizationUpdate = await fixture.Service.UpdateStatusAsync(
            foreignOrganizationStatus.Id,
            Request(fixture.OtherOrganizationId, foreignOrganizationStatus.Code, "Unauthorized edit") with
            {
                RowVersion = [1]
            }, fixture.ActorId);
        Assert.True(otherOrganizationUpdate.IsFailure);
        Assert.Equal(404, otherOrganizationUpdate.Error!.StatusCode);

        var tenantUpdate = await fixture.Service.UpdateStatusAsync(
            tenantStatus.Id,
            Request(null, tenantStatus.Code, "Unauthorized tenant edit") with
            {
                RowVersion = [1]
            }, fixture.ActorId);
        Assert.True(tenantUpdate.IsFailure);
        Assert.Equal(403, tenantUpdate.Error!.StatusCode);
        Assert.Equal(2, await db.ProjectStatusCatalogEntries.CountAsync());
    }

    [Fact]
    public async Task Organization_admin_configuration_writes_remain_denied_in_read_only_view_as_mode()
    {
        var fixture = CreateFixture(readOnly: true);
        await using var db = fixture.Db;
        await SeedOrganizationsAndPreInitiationAsync(fixture);

        var result = await fixture.Service.CreateStatusAsync(
            Request(fixture.OrganizationId, "read-only-create", "Read only attempt"), fixture.ActorId);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error!.StatusCode);
        Assert.Empty(await db.ProjectStatusCatalogEntries.ToListAsync());
    }

    public void Dispose() => AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());

    private static async Task SeedOrganizationsAndPreInitiationAsync(LifecycleFixture fixture)
    {
        fixture.Db.Organizations.AddRange(
            new OrganizationEntity
            {
                Id = fixture.OrganizationId,
                TenantId = fixture.TenantId,
                Name = "Authorized organization",
                IsActive = true,
                CreatedBy = "test"
            },
            new OrganizationEntity
            {
                Id = fixture.OtherOrganizationId,
                TenantId = fixture.TenantId,
                Name = "Unrelated organization",
                IsActive = true,
                CreatedBy = "test"
            });
        fixture.Db.ProjectPhaseCatalogEntries.Add(new ProjectPhaseCatalogEntry
        {
            TenantId = fixture.TenantId,
            OrganizationId = fixture.OrganizationId,
            Code = "pre-initiation",
            NormalizedCode = "PRE-INITIATION",
            NameEn = "Pre-Initiation",
            NameAr = "ما قبل البدء",
            IsDefault = true,
            IsActive = true,
            CreatedBy = "test"
        });
        fixture.Db.ProjectPhaseCatalogEntries.Add(new ProjectPhaseCatalogEntry
        {
            TenantId = fixture.TenantId,
            OrganizationId = fixture.OtherOrganizationId,
            Code = "unrelated-org-phase",
            NormalizedCode = "UNRELATED-ORG-PHASE",
            NameEn = "Unrelated organization phase",
            NameAr = "مرحلة منظمة أخرى",
            IsActive = true,
            CreatedBy = "test"
        });
        await fixture.Db.SaveChangesAsync();
    }

    private static ProjectLifecycleCatalogRequest Request(
        Guid? organizationId, string code, string name, bool isDefault = false) => new(
        OrganizationId: organizationId,
        Code: code,
        NameEn: name,
        NameAr: $"حالة {name}",
        DescriptionEn: null,
        DescriptionAr: null,
        DisplayOrder: 10,
        IsDefault: isDefault,
        IsActive: true);

    private static LifecycleFixture CreateFixture(bool readOnly = false)
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")),
                    new Claim("tenantId", tenantId.ToString("D")),
                    new Claim("permission", "OrganizationConfiguration.View"),
                    new Claim("permission", "OrganizationConfiguration.Manage")
                ], "organization-admin-lifecycle-test"))
            }
        };
        AppDbContext.ConfigureTenantResolution(http);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"organization-admin-lifecycle-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options);
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            actorId,
            tenantId,
            ManagementPerspective.ProjectManager,
            readOnly ? ManagementViewMode.ReadOnlyViewAs : ManagementViewMode.Native,
            null,
            null,
            null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>())));
        var scopedAccess = new Mock<IOrganizationSettingsAccessService>();
        scopedAccess.Setup(service => service.HasScopedOrganizationAdminAuthorityAsync(
                It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, bool _, CancellationToken _) => id == organizationId);
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var lifecycle = new ProjectLifecycleCatalogService(
            db, http, audit.Object, management, organizationSettingsAccess: scopedAccess.Object);
        return new LifecycleFixture(db, actorId, tenantId, organizationId, otherOrganizationId, lifecycle);
    }

    private sealed record LifecycleFixture(
        AppDbContext Db,
        Guid ActorId,
        Guid TenantId,
        Guid OrganizationId,
        Guid OtherOrganizationId,
        ProjectLifecycleCatalogService Service);
}
