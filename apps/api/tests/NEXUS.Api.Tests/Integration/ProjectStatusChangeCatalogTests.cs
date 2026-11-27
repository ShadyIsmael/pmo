using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class ProjectStatusChangeCatalogTests
{
    [Fact]
    public async Task UpdateAsync_uses_the_catalog_status_without_changing_phase_or_legacy_status()
    {
        var fixture = await CreateFixtureAsync();
        var service = CreateService(fixture, ["Project.View", "ProjectStatus.Change"]);

        var result = await ChangeAsync(service, fixture, fixture.TargetStatusId, legacyStatus: "Client supplied text");

        result.IsSuccess.Should().BeTrue();
        result.Value!.StatusId.Should().Be(fixture.TargetStatusId);
        result.Value.Phase.Should().Be("delivery");

        var saved = await fixture.Db.Projects.SingleAsync(item => item.Id == fixture.Project.Id);
        saved.ProjectStatusId.Should().Be(fixture.TargetStatusId);
        saved.Phase.Should().Be("delivery");
        saved.Status.Should().Be("Legacy status");
    }

    [Fact]
    public async Task UpdateAsync_rejects_an_inactive_catalog_status()
    {
        var fixture = await CreateFixtureAsync();
        var service = CreateService(fixture, ["Project.View", "ProjectStatus.Change"]);

        var result = await ChangeAsync(service, fixture, fixture.InactiveStatusId);

        result.IsFailure.Should().BeTrue();
        result.Error!.FieldName.Should().Be("statusId");
        result.Error.Message.Should().Contain("inactive");
        (await fixture.Db.Projects.SingleAsync(item => item.Id == fixture.Project.Id))
            .ProjectStatusId.Should().Be(fixture.SourceStatusId);
    }

    [Fact]
    public async Task UpdateAsync_rejects_statuses_outside_the_project_tenant_or_organization_scope()
    {
        var fixture = await CreateFixtureAsync();
        var service = CreateService(fixture, ["Project.View", "ProjectStatus.Change"]);

        var crossTenant = await ChangeAsync(service, fixture, fixture.OtherTenantStatusId);
        crossTenant.IsFailure.Should().BeTrue();
        crossTenant.Error!.FieldName.Should().Be("statusId");
        crossTenant.Error.Message.Should().Contain("not available");

        var crossOrganization = await ChangeAsync(service, fixture, fixture.OtherOrganizationStatusId);
        crossOrganization.IsFailure.Should().BeTrue();
        crossOrganization.Error!.FieldName.Should().Be("statusId");
        crossOrganization.Error.Message.Should().Contain("not available");

        (await fixture.Db.Projects.SingleAsync(item => item.Id == fixture.Project.Id))
            .ProjectStatusId.Should().Be(fixture.SourceStatusId);
    }

    [Fact]
    public async Task UpdateAsync_enforces_configured_transition_edges_and_reason_requirements()
    {
        var fixture = await CreateFixtureAsync();
        AppDbContext.ConfigureTenantResolution(CreateTenantAccessor(fixture.TenantId, fixture.UserId));
        fixture.Db.ProjectStatusTransitionRules.Add(new ProjectStatusTransitionRule
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            OrganizationId = fixture.OrganizationId,
            SourceStatusId = fixture.SourceStatusId,
            TargetStatusId = fixture.TargetStatusId,
            RequiresReason = true
        });
        await fixture.Db.SaveChangesAsync();
        var service = CreateService(fixture, ["Project.View", "ProjectStatus.Change"]);

        var unlisted = await ChangeAsync(service, fixture, fixture.UnlistedStatusId);
        unlisted.IsFailure.Should().BeTrue();
        unlisted.Error!.Message.Should().Contain("not an authorized transition");

        var missingReason = await ChangeAsync(service, fixture, fixture.TargetStatusId);
        missingReason.IsFailure.Should().BeTrue();
        missingReason.Error!.FieldName.Should().Be("statusChangeReason");
        missingReason.Error.Message.Should().Contain("reason is required");

        var withReason = await ChangeAsync(service, fixture, fixture.TargetStatusId, "Approved by governance");
        withReason.IsSuccess.Should().BeTrue();
        (await fixture.Db.Projects.SingleAsync(item => item.Id == fixture.Project.Id))
            .ProjectStatusId.Should().Be(fixture.TargetStatusId);
    }

    [Fact]
    public async Task UpdateAsync_blocks_status_changes_without_permission_with_an_active_deny_or_in_read_only_mode()
    {
        var fixture = await CreateFixtureAsync();

        // HttpContextAccessor shares its ambient HttpContext across instances, so each
        // scenario must build its service and issue the request before the next one.
        var scenarios = new (string Scenario, Func<ProjectService> Create)[]
        {
            ("missing permission", () => CreateService(fixture, ["Project.View"])),
            ("active deny", () => CreateService(fixture, ["Project.View", "ProjectStatus.Change"], ["ProjectStatus.Change"])),
            ("read-only view", () => CreateService(fixture, ["Project.View", "ProjectStatus.Change"], viewMode: ManagementViewMode.ReadOnlyViewAs))
        };

        foreach (var (scenario, create) in scenarios)
        {
            var result = await ChangeAsync(create(), fixture, fixture.TargetStatusId);
            result.IsFailure.Should().BeTrue($"status change must be blocked: {scenario}");
            result.Error!.StatusCode.Should().Be(403, $"status change must be forbidden: {scenario}");
        }

        (await fixture.Db.Projects.SingleAsync(item => item.Id == fixture.Project.Id))
            .ProjectStatusId.Should().Be(fixture.SourceStatusId);
    }

    private static Task<Result<ProjectDto>> ChangeAsync(
        ProjectService service,
        ProjectFixture fixture,
        Guid statusId,
        string? reason = null,
        string? legacyStatus = null)
        => service.UpdateAsync(fixture.Project.Id, new UpdateProjectDto
        {
            StatusId = statusId,
            StatusChangeReason = reason,
            Status = legacyStatus
        }, fixture.UserId.ToString());

    private static async Task<ProjectFixture> CreateFixtureAsync()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var sourceStatusId = Guid.NewGuid();
        var targetStatusId = Guid.NewGuid();
        var inactiveStatusId = Guid.NewGuid();
        var unlistedStatusId = Guid.NewGuid();
        var otherOrganizationStatusId = Guid.NewGuid();
        var otherTenantStatusId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"status-change-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        AppDbContext.ConfigureTenantResolution(CreateTenantAccessor(tenantId, userId));

        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PortfolioId = portfolioId,
            Name = "Status change test",
            Code = $"P-{Guid.NewGuid():N}"[..10],
            Currency = "GBP",
            Priority = "Medium",
            ProjectPhaseId = Guid.NewGuid(),
            Phase = "delivery",
            Status = "Legacy status",
            ProjectStatusId = sourceStatusId
        };

        db.Organizations.AddRange(
            new Organization
            {
                Id = organizationId, TenantId = tenantId, Name = "In-scope organization", IsActive = true
            },
            new Organization
            {
                Id = otherOrganizationId, TenantId = tenantId, Name = "Other organization", IsActive = true
            });
        db.Currencies.Add(new Currency
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "GBP", Name = "British Pound", Symbol = "£", IsBase = true
        });
        db.Portfolios.Add(new PortfolioEntity
        {
            Id = portfolioId,
            TenantId = tenantId,
            OrganizationId = organizationId,
            Code = "PORT-STATUS-001",
            Name = "Status test portfolio",
            Currency = "GBP"
        });
        db.Projects.Add(project);
        db.ProjectStatusCatalogEntries.AddRange(
            Status(sourceStatusId, tenantId, organizationId, "source", "Source status"),
            Status(targetStatusId, tenantId, organizationId, "target", "Target status"),
            Status(unlistedStatusId, tenantId, organizationId, "unlisted", "Unlisted status"),
            Status(inactiveStatusId, tenantId, organizationId, "inactive", "Retired status", isActive: false),
            Status(otherOrganizationStatusId, tenantId, otherOrganizationId, "other-org", "Other organization status"));
        await db.SaveChangesAsync();

        // Cross-Tenant catalog rows must be persisted under their own tenant scope;
        // the global query filter then hides them from the project's tenant.
        AppDbContext.ConfigureTenantResolution(CreateTenantAccessor(otherTenantId, userId));
        await using (var otherTenantDb = new AppDbContext(options))
        {
            otherTenantDb.ProjectStatusCatalogEntries.Add(
                Status(otherTenantStatusId, otherTenantId, organizationId, "other-tenant", "Other tenant status"));
            await otherTenantDb.SaveChangesAsync();
        }

        AppDbContext.ConfigureTenantResolution(CreateTenantAccessor(tenantId, userId));

        return new ProjectFixture(db, tenantId, organizationId, userId, project, sourceStatusId,
            targetStatusId, inactiveStatusId, unlistedStatusId, otherOrganizationStatusId, otherTenantStatusId);
    }

    private static ProjectStatusCatalogEntry Status(
        Guid id,
        Guid tenantId,
        Guid? organizationId,
        string code,
        string name,
        bool isActive = true)
        => new()
        {
            Id = id,
            TenantId = tenantId,
            OrganizationId = organizationId,
            Code = code,
            NormalizedCode = code.ToUpperInvariant(),
            NameEn = name,
            NameAr = name,
            DisplayOrder = 10,
            IsActive = isActive
        };

    private static ProjectService CreateService(
        ProjectFixture fixture,
        string[] permissions,
        string[]? deniedPermissions = null,
        ManagementViewMode viewMode = ManagementViewMode.Native)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, fixture.UserId.ToString()),
            new("tenantId", fixture.TenantId.ToString()),
            new("permission", "Project.View")
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        claims.AddRange((deniedPermissions ?? []).Select(permission => new Claim("deny", permission)));
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "status-change-test"))
            }
        };
        AppDbContext.ConfigureTenantResolution(http);

        var scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(),
            new HashSet<Guid> { fixture.Project.Id });
        var management = new Mock<IManagementContextAccessor>();
        management.SetupGet(item => item.Current).Returns(new ManagementContext(
            fixture.UserId,
            fixture.TenantId,
            ManagementPerspective.ProjectManager,
            viewMode,
            null,
            null,
            fixture.Project.Id,
            scope));
        var audit = new Mock<IAuditService>();
        audit.Setup(item => item.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        return new ProjectService(
            fixture.Db,
            audit.Object,
            http,
            new ProjectManagerMembershipSynchronizer(fixture.Db),
            management.Object,
            projectOrganizationLookup: new ProjectOrganizationLookup(fixture.Db));
    }

    private static HttpContextAccessor CreateTenantAccessor(Guid tenantId, Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("tenantId", tenantId.ToString()),
                new Claim("permission", "Project.View")
            ], "status-change-test"))
        }
    };

    private sealed record ProjectFixture(
        AppDbContext Db,
        Guid TenantId,
        Guid OrganizationId,
        Guid UserId,
        Project Project,
        Guid SourceStatusId,
        Guid TargetStatusId,
        Guid InactiveStatusId,
        Guid UnlistedStatusId,
        Guid OtherOrganizationStatusId,
        Guid OtherTenantStatusId);
}
