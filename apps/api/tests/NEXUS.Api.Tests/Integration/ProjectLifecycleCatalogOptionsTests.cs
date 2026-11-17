using System.Security.Claims;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Application.Mapping;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class ProjectLifecycleCatalogOptionsTests : IDisposable
{
    [Fact]
    public async Task Http_options_route_accepts_external_public_id_and_returns_only_actual_organization_phases()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectPublicId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId, PublicId = projectPublicId, TenantId = tenantId,
            Name = "HTTP lifecycle project", Code = "HTTP-LIFECYCLE", PortfolioId = portfolioId,
            ProjectPhaseId = Guid.NewGuid(), CreatedBy = userId.ToString()
        };
        var inaccessibleProject = new Project
        {
            TenantId = tenantId, Name = "Out of scope", Code = "OUT-OF-SCOPE",
            PortfolioId = portfolioId, CreatedBy = userId.ToString()
        };
        var crossTenantProject = new Project
        {
            TenantId = otherTenantId, Name = "Other tenant", Code = "OTHER-TENANT",
            CreatedBy = userId.ToString()
        };
        var ownership = Ownership(tenantId, organizationId, portfolioId);
        var configured = Phase(tenantId, Guid.NewGuid(), "configured-phase", "Configured phase", order: 5);
        configured.OrganizationId = organizationId;
        var preInitiation = Phase(tenantId, Guid.NewGuid(), "pre-initiation", "Pre-Initiation", isDefault: true);
        preInitiation.OrganizationId = organizationId;
        var unrelated = Phase(tenantId, Guid.NewGuid(), "other-org-phase", "Other org phase");
        unrelated.OrganizationId = otherOrganizationId;
        var legacy = Phase(tenantId, Guid.NewGuid(), "legacy-tenant-phase", "Legacy tenant phase");
        var inactive = Phase(tenantId, Guid.NewGuid(), "inactive-phase", "Inactive phase");
        inactive.OrganizationId = organizationId;
        inactive.IsActive = false;
        var tenantStatus = Status(tenantId, Guid.NewGuid(), "not-started", "Not Started", isDefault: true);
        var organizationStatus = Status(tenantId, Guid.NewGuid(), "org-status", "Organization status", isDefault: true);
        organizationStatus.OrganizationId = organizationId;
        var foreignTenantStatus = Status(otherTenantId, Guid.NewGuid(), "cross-tenant-status", "Cross tenant status", isDefault: true);

        var databaseName = $"project-lifecycle-http-{Guid.NewGuid():N}";
        var http = new HttpContextAccessor();
        AppDbContext.ConfigureTenantResolution(http);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using (var seed = new AppDbContext(options))
        {
            using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
            seed.AddRange(ownership.Organization, ownership.Portfolio, project, inaccessibleProject,
                configured, preInitiation, unrelated, legacy, inactive, tenantStatus, organizationStatus);
            await seed.SaveChangesAsync();
        }
        await using (var foreignSeed = new AppDbContext(options))
        {
            using var foreignTenantScope = TrustedTenantExecutionContext.Enter(otherTenantId);
            foreignSeed.AddRange(crossTenantProject, foreignTenantStatus);
            await foreignSeed.SaveChangesAsync();
        }

        var scope = new ManagementScope(new HashSet<Guid> { portfolioId }, new HashSet<Guid>(), new HashSet<Guid> { projectId })
        {
            OrganizationIds = new HashSet<Guid> { organizationId, otherOrganizationId },
            OrganizationReferences =
            [
                // The first accessible Organization is intentionally not the Project owner.
                new ManagementEntityReference(otherOrganizationId, "Other organization"),
                new ManagementEntityReference(organizationId, "Owning organization")
            ],
            // Simulate stale ordering/context metadata; the persisted Project graph is authoritative.
            ProjectOrganizationIds = new Dictionary<Guid, Guid> { [projectId] = otherOrganizationId }
        };
        var management = new ManagementContextAccessor();
        var resolvedManagement = new ManagementContext(
            userId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, projectId, scope);
        var scopeResolver = new Mock<IManagementScopeResolver>();
        scopeResolver.Setup(resolver => resolver.ResolveAsync(
                userId, tenantId, It.IsAny<ManagementPerspective?>(), It.IsAny<ManagementViewMode>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedManagement);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IHttpContextAccessor>(http);
        builder.Services.AddSingleton<IManagementContextAccessor>(management);
        builder.Services.AddSingleton(scopeResolver.Object);
        var effectivePermissions = new Mock<IUserEffectivePermissionService>();
        effectivePermissions.Setup(service => service.GetEffectivePermissionsForOrganizationAsync(
                It.IsAny<Guid>(), tenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>
            {
                "Project.Create", ProjectLifecyclePermissions.PhaseChange
            });
        builder.Services.AddSingleton(effectivePermissions.Object);
        builder.Services.AddDbContext<AppDbContext>(db => db
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<AppDbContext>());
        builder.Services.AddScoped<IProjectOrganizationLookup, ProjectOrganizationLookup>();
        builder.Services.AddScoped<NEXUS.Modules.Audit.Application.IAuditService>(_ =>
            Mock.Of<NEXUS.Modules.Audit.Application.IAuditService>());
        builder.Services.AddScoped<ProjectLifecycleCatalogService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(ProjectLifecycleCatalogController).Assembly);
        builder.Services.AddAuthentication("lifecycle-test")
            .AddScheme<LifecycleOptionsTestAuthOptions, LifecycleOptionsTestAuthHandler>(
                "lifecycle-test", auth => { auth.TenantId = tenantId; auth.UserId = userId; });
        builder.Services.AddAuthorization(auth => auth.AddPolicy(
            ProjectLifecyclePermissions.OptionsLookup,
            policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                context.User.HasClaim("permission", ProjectLifecyclePermissions.PhaseChange)
                || context.User.HasClaim("permission", "Project.Create"))));

        await using var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseMiddleware<ProjectPublicIdRoutingMiddleware>();
        // Preserve production ordering; scope is established only after the
        // external query identifier has been resolved at the HTTP boundary.
        app.UseMiddleware<ManagementContextMiddleware>();
        app.UseMiddleware<ProjectResourceScopeMiddleware>();
        app.Use(async (context, next) =>
        {
            if (context.User.Identity?.IsAuthenticated == true
                && ProjectRequestContext.GetPublicProjectId(context) == projectPublicId
                && context.Request.Query.TryGetValue("projectId", out var queryId))
            {
                Guid.Parse(queryId.ToString()).Should().Be(projectId);
                ProjectRequestContext.GetInternalProjectId(context).Should().Be(projectId);
                ProjectRequestContext.GetPublicProjectId(context).Should().Be(projectPublicId);
            }
            await next();
        });
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();

        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };

            var response = await client.GetAsync($"/api/project-lifecycle/options?projectId={projectPublicId:D}");
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            var optionsResult = await response.Content.ReadFromJsonAsync<ProjectLifecycleOptionsDto>();
            optionsResult.Should().NotBeNull();
            optionsResult!.Phases.Select(option => option.Id).Should()
                .BeEquivalentTo(new[] { preInitiation.Id, configured.Id });
            optionsResult.Phases.First().Id.Should().Be(preInitiation.Id);
            optionsResult.Phases.Select(option => option.Id).Should().NotContain(legacy.Id);
            optionsResult.Phases.Select(option => option.Id).Should().NotContain(unrelated.Id);
            optionsResult.Phases.Select(option => option.Id).Should().NotContain(inactive.Id);

            var unscopedOptions = await client.GetAsync("/api/project-lifecycle/options");
            unscopedOptions.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
            var unscopedError = await unscopedOptions.Content.ReadAsStringAsync();
            unscopedError.Should().Contain("VALIDATION_ERROR");
            unscopedError.Should().Contain("Project lifecycle options require an Organization or in-scope Project context.");

            var createWithoutPortfolio = await client.GetAsync(
                "/api/project-lifecycle/options?purpose=create");
            createWithoutPortfolio.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            var emptyCreateOptions = await createWithoutPortfolio.Content.ReadFromJsonAsync<ProjectLifecycleOptionsDto>();
            emptyCreateOptions.Should().NotBeNull();
            emptyCreateOptions!.Phases.Should().BeEmpty();
            emptyCreateOptions.Statuses.Select(option => option.Id).Should()
                .BeEquivalentTo(new[] { tenantStatus.Id });
            emptyCreateOptions.Statuses.Select(option => option.Id).Should().NotContain(organizationStatus.Id);
            emptyCreateOptions.Statuses.Select(option => option.Id).Should().NotContain(foreignTenantStatus.Id);

            var createOptions = await client.GetAsync(
                $"/api/project-lifecycle/options?purpose=create&portfolioId={portfolioId:D}");
            createOptions.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            var createResult = await createOptions.Content.ReadFromJsonAsync<ProjectLifecycleOptionsDto>();
            createResult.Should().NotBeNull();
            createResult!.Phases.Select(option => option.Id).Should()
                .BeEquivalentTo(new[] { preInitiation.Id, configured.Id });
            createResult.Phases.Select(option => option.Id).Should().NotContain(legacy.Id);
            createResult.Phases.Select(option => option.Id).Should().NotContain(unrelated.Id);
            createResult.Statuses.Select(option => option.Id).Should()
                .BeEquivalentTo(new[] { tenantStatus.Id });
            createResult.Statuses.Select(option => option.Id).Should().NotContain(organizationStatus.Id);
            createResult.Statuses.Select(option => option.Id).Should().NotContain(foreignTenantStatus.Id);

            var crossScopeCreate = await client.GetAsync(
                $"/api/project-lifecycle/options?purpose=create&portfolioId={Guid.NewGuid():D}");
            crossScopeCreate.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);

            var mismatch = await client.GetAsync(
                $"/api/project-lifecycle/options?projectId={projectPublicId:D}&organizationId={otherOrganizationId:D}");
            mismatch.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);

            var outOfScope = await client.GetAsync(
                $"/api/project-lifecycle/options?projectId={inaccessibleProject.PublicId:D}");
            outOfScope.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);

            var crossTenant = await client.GetAsync(
                $"/api/project-lifecycle/options?projectId={crossTenantProject.PublicId:D}");
            crossTenant.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);

            var invalidProject = await client.GetAsync(
                $"/api/project-lifecycle/options?projectId={Guid.NewGuid():D}");
            invalidProject.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);

            var internalId = await client.GetAsync(
                $"/api/project-lifecycle/options?projectId={projectId:D}");
            internalId.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);

            foreach (var invalidSession in new[] { "missing", "invalid" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    $"/api/project-lifecycle/options?projectId={projectPublicId:D}");
                request.Headers.Add("X-Lifecycle-Test-Session", invalidSession);
                var unauthenticated = await client.SendAsync(request);
                unauthenticated.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Project_options_reject_mismatched_or_unresolved_organization_without_tenant_fallback()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var project = new Project
        {
            TenantId = tenantId, Name = "Owned project", Code = "OWNED-PROJECT",
            PortfolioId = portfolioId, CreatedBy = userId.ToString()
        };
        var unownedProject = new Project
        {
            TenantId = tenantId, Name = "Unresolved project", Code = "UNRESOLVED-PROJECT",
            CreatedBy = userId.ToString()
        };
        var tenantPhase = Phase(tenantId, Guid.NewGuid(), "tenant-legacy", "Legacy tenant phase");
        var database = NewDatabase(tenantId, userId, project.Id, "ProjectPhase.Change");
        database.Management.Set(database.Management.Current! with
        {
            Scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { project.Id, unownedProject.Id })
        });
        await using var db = database.Db;
        var ownership = Ownership(tenantId, organizationId, portfolioId);
        db.AddRange(ownership.Organization, ownership.Portfolio, project, unownedProject, tenantPhase);
        await db.SaveChangesAsync();
        var service = new ProjectLifecycleCatalogService(
            db, database.Http, Mock.Of<NEXUS.Modules.Audit.Application.IAuditService>(), database.Management,
            new ProjectOrganizationLookup(db));

        var mismatched = await service.GetOptionsAsync(otherOrganizationId, project.Id);
        mismatched.IsFailure.Should().BeTrue();
        mismatched.Error!.StatusCode.Should().Be(404);

        var unresolved = await service.GetOptionsAsync(organizationId: null, unownedProject.Id);
        unresolved.IsFailure.Should().BeTrue();
        unresolved.Error!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Project_options_are_id_backed_and_do_not_expose_transition_graphs()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var phaseId = Guid.NewGuid();
        var nextPhaseId = Guid.NewGuid();
        var legacyPhaseId = Guid.NewGuid();
        var inactivePhaseId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var unrelatedOrganizationId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var nextStatusId = Guid.NewGuid();
        var project = new Project
        {
            TenantId = tenantId,
            Name = "Lifecycle options project",
            Code = "LIFECYCLE-OPTIONS",
            Phase = "legacy-phase-label",
            Status = "legacy-status-label",
            PortfolioId = portfolioId,
            ProjectPhaseId = phaseId,
            ProjectStatusId = statusId,
            CreatedBy = userId.ToString()
        };

        var phase = Phase(tenantId, phaseId, "phase-alpha", "Alpha phase", order: 10);
        phase.OrganizationId = organizationId;
        var nextPhase = Phase(tenantId, nextPhaseId, "phase-beta", "Beta phase", order: 20);
        nextPhase.OrganizationId = organizationId;
        var preInitiation = Phase(tenantId, Guid.NewGuid(), "pre-initiation", "Pre-Initiation", isDefault: true);
        preInitiation.OrganizationId = organizationId;
        var unrelatedPhase = Phase(tenantId, Guid.NewGuid(), "unrelated-phase", "Other Organization phase");
        unrelatedPhase.OrganizationId = unrelatedOrganizationId;
        var legacyPhase = Phase(tenantId, legacyPhaseId, "legacy-tenant-phase", "Legacy tenant phase");
        var inactivePhase = Phase(tenantId, inactivePhaseId, "inactive-configured", "Inactive configured phase");
        inactivePhase.OrganizationId = organizationId;
        inactivePhase.IsActive = false;
        var status = Status(tenantId, statusId, "status-open", "Open status", isDefault: true, order: 1);
        var nextStatus = Status(tenantId, nextStatusId, "status-paused", "Paused status", order: 2);
        var unrelatedStatus = Status(tenantId, Guid.NewGuid(), "other-org-status", "Other Organization status");
        unrelatedStatus.OrganizationId = unrelatedOrganizationId;
        var database = NewDatabase(tenantId, userId, project.Id);
        database.Management.Set(new ManagementContext(
            userId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, project.Id,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { project.Id })
            {
                OrganizationIds = new HashSet<Guid> { organizationId, unrelatedOrganizationId },
                OrganizationReferences =
                [
                    // The first accessible Organization is intentionally not the Project owner.
                    new ManagementEntityReference(unrelatedOrganizationId, "Other Organization"),
                    new ManagementEntityReference(organizationId, "Actual Organization")
                ],
                // Deliberately stale; persisted ownership must win over scope snapshot ordering/data.
                ProjectOrganizationIds = new Dictionary<Guid, Guid> { [project.Id] = unrelatedOrganizationId }
            }));
        await using var db = database.Db;
        var ownership = Ownership(tenantId, organizationId, portfolioId);
        db.AddRange(ownership.Organization, ownership.Portfolio, project, phase, nextPhase,
            preInitiation, unrelatedPhase, legacyPhase, inactivePhase, status, nextStatus, unrelatedStatus,
            new ProjectPhaseTransitionRule
            {
                TenantId = tenantId, SourcePhaseId = phaseId, TargetPhaseId = nextPhaseId,
                IsActive = true, CreatedBy = userId.ToString()
            },
            new ProjectStatusTransitionRule
            {
                TenantId = tenantId, SourceStatusId = statusId, TargetStatusId = nextStatusId,
                RequiresReason = true, IsActive = true, CreatedBy = userId.ToString()
            });
        await db.SaveChangesAsync();

        var service = new ProjectLifecycleCatalogService(
            db,
            database.Http,
            Mock.Of<NEXUS.Modules.Audit.Application.IAuditService>(),
            database.Management,
            new ProjectOrganizationLookup(db));

        var result = await service.GetOptionsAsync(organizationId: null, project.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Phases.Select(option => option.Id).Should().BeEquivalentTo(new[] { preInitiation.Id, phaseId, nextPhaseId });
        result.Value.Statuses.Select(option => option.Id).Should().BeEquivalentTo(new[] { statusId, nextStatusId });
        result.Value.Statuses.Select(option => option.Id).Should().NotContain(unrelatedStatus.Id);
        result.Value.Phases.First().Id.Should().Be(preInitiation.Id);
        result.Value.Phases.Single(option => option.Id == preInitiation.Id).IsDefault.Should().BeTrue();
        result.Value.Phases.Select(option => option.Id).Should().NotContain(legacyPhaseId);
        result.Value.Phases.Select(option => option.Id).Should().NotContain(unrelatedPhase.Id);
        result.Value.Phases.Select(option => option.Id).Should().NotContain(inactivePhaseId);
        (await db.ProjectPhaseCatalogEntries.AnyAsync(item => item.Id == legacyPhaseId)).Should().BeTrue();
        result.Value.Statuses.Single(option => option.Id == statusId).IsDefault.Should().BeTrue();
        typeof(ProjectLifecycleOptionDto).GetProperty("AllowedTransitionIds").Should().BeNull();
        typeof(ProjectLifecycleOptionDto).GetProperty("AllowedTransitions").Should().BeNull();
        typeof(ProjectLifecycleCatalogController).GetMethods()
            .Should().NotContain(method => method.Name.Contains("Transition", StringComparison.OrdinalIgnoreCase));
        result.Value.Phases.Select(option => option.Id).Should().NotContain(statusId);
        result.Value.Statuses.Select(option => option.Id).Should().NotContain(phaseId);
    }

    [Fact]
    public async Task Phase_change_permission_can_read_phase_targets_for_an_authorized_project()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var phaseId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var project = new Project
        {
            TenantId = tenantId,
            Name = "Status-scoped lookup project",
            Code = "STATUS-LOOKUP",
            PortfolioId = portfolioId,
            ProjectPhaseId = phaseId,
            ProjectStatusId = statusId,
            CreatedBy = userId.ToString()
        };
        var database = NewDatabase(tenantId, userId, project.Id, "ProjectPhase.Change");
        await using var db = database.Db;
        var preInitiation = Phase(tenantId, phaseId, "pre-initiation", "Pre-Initiation", isDefault: true);
        preInitiation.OrganizationId = organizationId;
        var ownership = Ownership(tenantId, organizationId, portfolioId);
        db.AddRange(ownership.Organization, ownership.Portfolio, project, preInitiation,
            Status(tenantId, statusId, "status-configured", "Configured status", isDefault: true));
        await db.SaveChangesAsync();
        var service = new ProjectLifecycleCatalogService(
            db, database.Http, Mock.Of<NEXUS.Modules.Audit.Application.IAuditService>(), database.Management,
            new ProjectOrganizationLookup(db));

        var result = await service.GetOptionsAsync(organizationId: null, project.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Phases.Should().ContainSingle(item => item.Id == phaseId && item.Code == "pre-initiation");
        result.Value.Statuses.Should().BeEmpty();
    }

    [Fact]
    public async Task Phase_catalog_get_is_non_mutating_and_reports_missing_protected_phase()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var database = NewDatabase(tenantId, userId, Guid.NewGuid(), ProjectLifecyclePermissions.PhaseView);
        await using var db = database.Db;
        var service = new ProjectLifecycleCatalogService(
            db, database.Http, Mock.Of<NEXUS.Modules.Audit.Application.IAuditService>(), database.Management,
            new ProjectOrganizationLookup(db));

        var result = await service.GetPhasesAsync(organizationId: null, includeInactive: false, search: null);

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(409);
        result.Error.Message.Should().Contain("Pre-Initiation");
        (await db.ProjectPhaseCatalogEntries.IgnoreQueryFilters().ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Project_options_get_is_non_mutating_and_reports_missing_organization_phase()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId, TenantId = tenantId, Name = "Options without phase", Code = "OPTIONS-NO-PHASE",
            PortfolioId = portfolioId, CreatedBy = userId.ToString()
        };
        var database = NewDatabase(tenantId, userId, projectId, "Project.View");
        await using var db = database.Db;
        var ownership = Ownership(tenantId, organizationId, portfolioId);
        db.AddRange(ownership.Organization, ownership.Portfolio, project);
        await db.SaveChangesAsync();
        var service = new ProjectLifecycleCatalogService(
            db, database.Http, Mock.Of<NEXUS.Modules.Audit.Application.IAuditService>(), database.Management,
            new ProjectOrganizationLookup(db));

        var result = await service.GetOptionsAsync(organizationId: null, projectId);

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(409);
        result.Error.Message.Should().Contain("Pre-Initiation");
        (await db.ProjectPhaseCatalogEntries.IgnoreQueryFilters().ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Create_options_before_portfolio_selection_return_only_tenant_statuses_without_a_protected_phase_conflict()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var database = NewDatabase(tenantId, userId, Guid.NewGuid(), "Project.Create");
        await using var db = database.Db;
        var tenantStatus = Status(tenantId, Guid.NewGuid(), "not-started", "Not Started", isDefault: true);
        var organizationStatus = Status(tenantId, Guid.NewGuid(), "org-status", "Organization status", isDefault: true);
        organizationStatus.OrganizationId = organizationId;
        db.AddRange(tenantStatus, organizationStatus);
        await db.SaveChangesAsync();
        var service = new ProjectLifecycleCatalogService(
            db, database.Http, Mock.Of<NEXUS.Modules.Audit.Application.IAuditService>(), database.Management,
            new ProjectOrganizationLookup(db));

        var result = await service.GetOptionsAsync(
            organizationId: null, projectId: null, portfolioId: null, programId: null, createPurpose: true);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Phases.Should().BeEmpty();
        result.Value.Statuses.Select(item => item.Id).Should().BeEquivalentTo(new[] { tenantStatus.Id });
        result.Value.Statuses.Should().NotContain(item => item.Id == organizationStatus.Id);
        // Status-only create options must never provision or repair the Organization-scoped
        // Pre-Initiation phase before a Portfolio (and therefore an Organization) is chosen.
        (await db.ProjectPhaseCatalogEntries.IgnoreQueryFilters().ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Create_options_resolve_the_active_portfolio_organization_without_tenant_or_other_organization_fallback()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var otherPortfolioId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var mismatchedProgramId = Guid.NewGuid();
        var inactiveProgramId = Guid.NewGuid();
        var database = NewDatabase(tenantId, userId, Guid.NewGuid(), "Project.Create");
        var scope = new ManagementScope(
            new HashSet<Guid> { portfolioId }, new HashSet<Guid> { programId, mismatchedProgramId, inactiveProgramId }, new HashSet<Guid>());
        database.Management.Set(new ManagementContext(
            userId, tenantId, ManagementPerspective.PortfolioManager, ManagementViewMode.Native,
            portfolioId, null, null, scope));
        await using var db = database.Db;

        var ownership = Ownership(tenantId, organizationId, portfolioId);
        var otherOrganization = new Organization
        {
            Id = otherOrganizationId, TenantId = tenantId, Name = "Other organization", IsActive = true,
            CreatedBy = "lifecycle-test"
        };
        var otherPortfolio = new PortfolioEntity
        {
            Id = otherPortfolioId, TenantId = tenantId, OrganizationId = otherOrganizationId,
            Name = "Other portfolio", Code = "PF-OTHER", Status = "Active", CreatedBy = "lifecycle-test"
        };
        var protectedPhase = Phase(tenantId, Guid.NewGuid(), "pre-initiation", "Pre-Initiation", isDefault: true);
        protectedPhase.OrganizationId = organizationId;
        var tenantPhase = Phase(tenantId, Guid.NewGuid(), "tenant-phase", "Tenant phase", order: 1);
        var unrelatedOrganizationPhase = Phase(tenantId, Guid.NewGuid(), "other-org-phase", "Other organization phase");
        unrelatedOrganizationPhase.OrganizationId = otherOrganizationId;
        var organizationStatus = Status(tenantId, Guid.NewGuid(), "status-open", "Open", isDefault: true);
        organizationStatus.OrganizationId = organizationId;
        var tenantStatus = Status(tenantId, Guid.NewGuid(), "tenant-status", "Tenant status", isDefault: true);
        var unrelatedOrganizationStatus = Status(tenantId, Guid.NewGuid(), "other-org-status", "Other organization status");
        unrelatedOrganizationStatus.OrganizationId = otherOrganizationId;
        var program = new ProgramEntity
        {
            Id = programId, TenantId = tenantId, PortfolioId = portfolioId,
            Name = "Owned program", Code = "PGM-OWNED", Status = "Active", CreatedBy = "lifecycle-test"
        };
        var mismatchedProgram = new ProgramEntity
        {
            Id = mismatchedProgramId, TenantId = tenantId, PortfolioId = otherPortfolioId,
            Name = "Other program", Code = "PGM-OTHER", Status = "Active", CreatedBy = "lifecycle-test"
        };
        var inactiveProgram = new ProgramEntity
        {
            Id = inactiveProgramId, TenantId = tenantId, PortfolioId = portfolioId,
            Name = "Inactive program", Code = "PGM-INACTIVE", Status = "Archived", CreatedBy = "lifecycle-test"
        };
        db.AddRange(ownership.Organization, ownership.Portfolio, otherOrganization, otherPortfolio,
            protectedPhase, tenantPhase, unrelatedOrganizationPhase, organizationStatus, tenantStatus,
            unrelatedOrganizationStatus, program, mismatchedProgram, inactiveProgram);
        await db.SaveChangesAsync();
        var service = new ProjectLifecycleCatalogService(
            db, database.Http, Mock.Of<NEXUS.Modules.Audit.Application.IAuditService>(), database.Management,
            new ProjectOrganizationLookup(db));

        var withoutProgram = await service.GetOptionsAsync(
            organizationId: null, projectId: null, portfolioId, programId: null, createPurpose: true);
        var withProgram = await service.GetOptionsAsync(
            organizationId: null, projectId: null, portfolioId, programId, createPurpose: true);
        var mismatched = await service.GetOptionsAsync(
            organizationId: null, projectId: null, portfolioId, mismatchedProgramId, createPurpose: true);
        var unassignedPortfolio = await service.GetOptionsAsync(
            organizationId: null, projectId: null, otherPortfolioId, programId: null, createPurpose: true);
        var inactiveProgramResult = await service.GetOptionsAsync(
            organizationId: null, projectId: null, portfolioId, inactiveProgramId, createPurpose: true);

        withoutProgram.IsSuccess.Should().BeTrue();
        withProgram.IsSuccess.Should().BeTrue();
        foreach (var result in new[] { withoutProgram, withProgram })
        {
            result.Value!.Phases.Select(item => item.Id).Should().BeEquivalentTo(new[] { protectedPhase.Id });
            result.Value.Phases.Should().NotContain(item => item.Id == tenantPhase.Id);
            result.Value.Phases.Should().NotContain(item => item.Id == unrelatedOrganizationPhase.Id);
            result.Value.Statuses.Select(item => item.Id).Should().BeEquivalentTo(new[] { tenantStatus.Id });
            result.Value.Statuses.Should().NotContain(item => item.Id == organizationStatus.Id);
            result.Value.Statuses.Should().NotContain(item => item.Id == unrelatedOrganizationStatus.Id);
        }
        mismatched.IsFailure.Should().BeTrue();
        mismatched.Error!.StatusCode.Should().Be(404);
        unassignedPortfolio.IsFailure.Should().BeTrue();
        unassignedPortfolio.Error!.StatusCode.Should().Be(404);
        inactiveProgramResult.IsFailure.Should().BeTrue();
        inactiveProgramResult.Error!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Create_options_reject_unassigned_or_inactive_portfolios()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var database = NewDatabase(tenantId, userId, Guid.NewGuid(), "Project.Create");
        await using var db = database.Db;
        var ownership = Ownership(tenantId, organizationId, portfolioId);
        ownership.Portfolio.Status = "Archived";
        db.AddRange(ownership.Organization, ownership.Portfolio);
        await db.SaveChangesAsync();
        var service = new ProjectLifecycleCatalogService(
            db, database.Http, Mock.Of<NEXUS.Modules.Audit.Application.IAuditService>(), database.Management,
            new ProjectOrganizationLookup(db));

        var result = await service.GetOptionsAsync(
            organizationId: null, projectId: null, portfolioId, programId: null, createPurpose: true);

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Lifecycle_catalog_mutators_reject_read_only_management_context_before_writing()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var database = NewDatabase(tenantId, userId, Guid.NewGuid());
        database.Management.Set(database.Management.Current! with { ViewMode = ManagementViewMode.ReadOnlyViewAs });
        var permissions = new[]
        {
            ProjectLifecyclePermissions.PhaseCreate,
            ProjectLifecyclePermissions.PhaseEdit,
            ProjectLifecyclePermissions.PhaseActivateDeactivate,
            ProjectLifecyclePermissions.StatusCreate,
            ProjectLifecyclePermissions.StatusEdit,
            ProjectLifecyclePermissions.StatusActivateDeactivate
        };
        database.Http.HttpContext!.User.AddIdentity(new ClaimsIdentity(
            permissions.Select(permission => new Claim("permission", permission)), "lifecycle-read-only-test"));
        await using var db = database.Db;
        var service = new ProjectLifecycleCatalogService(
            db, database.Http, Mock.Of<NEXUS.Modules.Audit.Application.IAuditService>(), database.Management,
            new ProjectOrganizationLookup(db));
        var request = new ProjectLifecycleCatalogRequest(
            null, "phase-new", "New phase", "مرحلة جديدة", null, null, 10, false, true);
        var actorId = userId;
        var results = new[]
        {
            await service.CreatePhaseAsync(request, actorId),
            await service.CreateStatusAsync(request, actorId),
            await service.UpdatePhaseAsync(Guid.NewGuid(), request, actorId),
            await service.UpdateStatusAsync(Guid.NewGuid(), request, actorId),
            await service.SetPhaseActiveAsync(Guid.NewGuid(), false, Array.Empty<byte>(), actorId),
            await service.SetStatusActiveAsync(Guid.NewGuid(), false, Array.Empty<byte>(), actorId),
            await service.SetPhaseDefaultAsync(Guid.NewGuid(), Array.Empty<byte>(), actorId),
            await service.SetStatusDefaultAsync(Guid.NewGuid(), Array.Empty<byte>(), actorId)
        };

        Assert.All(results, result =>
        {
            Assert.True(result.IsFailure);
            Assert.Equal(403, result.Error!.StatusCode);
        });
        (await db.ProjectPhaseCatalogEntries.IgnoreQueryFilters().ToListAsync()).Should().BeEmpty();
        (await db.ProjectStatusCatalogEntries.IgnoreQueryFilters().ToListAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Project.View", true)]
    [InlineData("StatusReport.View", false)]
    [InlineData("StatusReport.Create", false)]
    [InlineData("StatusReport.Edit", false)]
    [InlineData("StatusReport.Publish", false)]
    public async Task Project_and_status_report_permissions_read_only_scoped_lifecycle_options(
        string permission, bool includesPhases)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var phaseId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var project = new Project
        {
            TenantId = tenantId,
            Name = "Report options project",
            Code = "REPORT-OPTIONS",
            PortfolioId = portfolioId,
            ProjectPhaseId = phaseId,
            ProjectStatusId = statusId,
            CreatedBy = userId.ToString()
        };
        var database = NewDatabase(tenantId, userId, project.Id, permission);
        await using var db = database.Db;
        var configuredPhase = Phase(tenantId, phaseId, "phase-report", "Report phase");
        configuredPhase.OrganizationId = organizationId;
        var preInitiation = Phase(tenantId, Guid.NewGuid(), "pre-initiation", "Pre-Initiation", isDefault: true);
        preInitiation.OrganizationId = organizationId;
        var tenantPreInitiation = Phase(tenantId, Guid.NewGuid(), "pre-initiation", "Pre-Initiation", isDefault: true);
        var tenantPhase = Phase(tenantId, Guid.NewGuid(), "tenant-search-phase", "Tenant Search Phase");
        var ownership = Ownership(tenantId, organizationId, portfolioId);
        db.AddRange(ownership.Organization, ownership.Portfolio, project, configuredPhase, preInitiation, tenantPreInitiation, tenantPhase,
            Status(tenantId, statusId, "status-report", "Report status", isDefault: true));
        await db.SaveChangesAsync();
        var service = new ProjectLifecycleCatalogService(
            db, database.Http, Mock.Of<NEXUS.Modules.Audit.Application.IAuditService>(), database.Management,
            new ProjectOrganizationLookup(db));

        var result = await service.GetOptionsAsync(organizationId: null, project.Id);

        result.IsSuccess.Should().BeTrue();
        if (includesPhases)
        {
            result.Value!.Phases.Should().Contain(item => item.Id == phaseId);
            result.Value.Phases.Should().Contain(item => item.Id == preInitiation.Id);
        }
        else
            result.Value!.Phases.Should().BeEmpty();
        result.Value!.Statuses.Should().ContainSingle(item => item.Id == statusId);
        if (permission.StartsWith("StatusReport.", StringComparison.Ordinal))
        {
            var unscoped = await service.GetOptionsAsync(organizationId: null, projectId: null);
            unscoped.IsFailure.Should().BeTrue();
            unscoped.Error!.StatusCode.Should().Be(403);
        }
        else if (permission == "Project.View")
        {
            var searchOptions = await service.GetOptionsAsync(organizationId: null, projectId: null);
            searchOptions.IsFailure.Should().BeTrue();
            searchOptions.Error!.Code.Should().Be("VALIDATION_ERROR");
            searchOptions.Error.StatusCode.Should().Be(400);
            searchOptions.Error.Message.Should().Contain("Organization or in-scope Project context");
        }
    }

    [Fact]
    public void Project_mapping_takes_catalog_ids_and_normal_updates_cannot_change_phase_or_status()
    {
        var phaseId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var project = new CreateProjectDto
        {
            Name = "ID-backed lifecycle",
            Code = "LIFECYCLE-ID",
            PhaseId = phaseId,
            StatusId = statusId
        }.ToEntity("creator");

        project.ProjectPhaseId.Should().Be(phaseId);
        project.ProjectStatusId.Should().Be(statusId);

        project.Phase = "stored-phase-code";
        project.Status = "stored-status-code";
        new UpdateProjectDto { Status = "attempted-bypass" }.UpdateEntity(project, "editor");

        project.ProjectPhaseId.Should().Be(phaseId);
        project.ProjectStatusId.Should().Be(statusId);
        project.Phase.Should().Be("stored-phase-code");
        project.Status.Should().Be("stored-status-code");
    }

    [Fact]
    public async Task Status_catalog_options_include_configurable_reporting_semantics()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var database = NewDatabase(tenantId, userId, projectId, "Project.Create");
        await using var db = database.Db;
        db.ProjectPhaseCatalogEntries.Add(Phase(
            tenantId, Guid.NewGuid(), "pre-initiation", "Pre-Initiation", isDefault: true));
        var status = Status(tenantId, statusId, "status-review", "Under Review", isDefault: true);
        status.ReportingCategory = ProjectStatusReportingCategories.Completed;
        status.AttentionLevel = ProjectStatusAttentionLevels.NeedsAttention;
        db.ProjectStatusCatalogEntries.Add(status);
        await db.SaveChangesAsync();
        var service = new ProjectLifecycleCatalogService(
            db, database.Http, Mock.Of<NEXUS.Modules.Audit.Application.IAuditService>(), database.Management,
            new ProjectOrganizationLookup(db));

        var result = await service.GetOptionsAsync(
            organizationId: null, projectId: null, portfolioId: null, programId: null, createPurpose: true);

        result.IsSuccess.Should().BeTrue();
        var option = result.Value!.Statuses.Should().ContainSingle(item => item.Id == statusId).Which;
        option.ReportingCategory.Should().Be(ProjectStatusReportingCategories.Completed);
        option.AttentionLevel.Should().Be(ProjectStatusAttentionLevels.NeedsAttention);
    }

    [Fact]
    public async Task Authorized_status_change_does_not_require_a_transition_rule_and_is_audited()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "Direct status change", Code = "DIRECT-STATUS",
            Status = "status-open", ProjectStatusId = Guid.NewGuid(), CreatedBy = userId.ToString()
        };
        var targetStatus = Status(tenantId, Guid.NewGuid(), "status-review", "Under Review");
        targetStatus.OrganizationId = organizationId;
        var database = NewDatabase(tenantId, userId, project.Id, "ProjectStatus.Change");
        await using var db = database.Db;
        db.AddRange(project, targetStatus,
            Status(tenantId, project.ProjectStatusId!.Value, "status-open", "Open", isDefault: true));
        await db.SaveChangesAsync();

        var effectivePermissions = new Mock<IUserEffectivePermissionService>();
        effectivePermissions.Setup(item => item.GetEffectivePermissionsAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string> { "ProjectStatus.Change" });
        effectivePermissions.Setup(item => item.GetEffectivePermissionsForOrganizationAsync(
                userId, tenantId, organizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string> { "ProjectStatus.Change" });
        var audit = new Mock<NEXUS.Modules.Audit.Application.IAuditService>();
        var organizationLookup = new Mock<NEXUS.SharedKernel.IProjectOrganizationLookup>();
        organizationLookup.Setup(item => item.ResolveOrganizationIdAsync(tenantId, project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(organizationId);
        audit.Setup(item => item.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NEXUS.SharedKernel.Result.Success());
        var service = new ProjectService(db, audit.Object, database.Http,
            new ProjectManagerMembershipSynchronizer(db), database.Management,
            effectivePermissions: effectivePermissions.Object,
            projectOrganizationLookup: organizationLookup.Object);

        var sourceStatusId = project.ProjectStatusId!.Value;
        var result = await service.ChangeStatusAsync(project.Id,
            new ChangeProjectStatusDto(targetStatus.Id, Reason: null), userId.ToString());

        result.IsSuccess.Should().BeTrue();
        result.Value!.StatusId.Should().Be(targetStatus.Id);
        result.Value.Status.Should().Be(targetStatus.Code);
        (await db.Set<ProjectStatusTransitionRule>().CountAsync()).Should().Be(0);
        audit.Verify(item => item.LogAsync(It.Is<AuditEntry>(entry => entry.EntityId == project.Id
                && entry.Action == "StatusChanged"
                && entry.Changes!.Contains("status-open")
                && entry.Changes.Contains("status-review")
                && entry.Changes.Contains(sourceStatusId.ToString())
                && entry.Changes.Contains(targetStatus.Id.ToString())
                && entry.OriginalValues!.Contains(sourceStatusId.ToString())
                && entry.OriginalValues.Contains("status-open")
                && entry.NewValues!.Contains(targetStatus.Id.ToString())
                && entry.NewValues.Contains("status-review")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose() => AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());

    private static (Organization Organization, PortfolioEntity Portfolio) Ownership(
        Guid tenantId, Guid organizationId, Guid portfolioId) =>
        (new Organization
        {
            Id = organizationId, TenantId = tenantId, Name = "Lifecycle organization", IsActive = true,
            CreatedBy = "lifecycle-test"
        }, new PortfolioEntity
        {
            Id = portfolioId, TenantId = tenantId, OrganizationId = organizationId,
            Name = "Lifecycle portfolio", Code = "PF-LIFECYCLE", Status = "Active", CreatedBy = "lifecycle-test"
        });

    private static (AppDbContext Db, HttpContextAccessor Http, ManagementContextAccessor Management) NewDatabase(
        Guid tenantId, Guid userId, Guid projectId, string permission = "Project.Edit")
    {
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim("tenantId", tenantId.ToString()),
                    new Claim("permission", permission)
                ], "lifecycle-test"))
            }
        };
        AppDbContext.ConfigureTenantResolution(http);
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"project-lifecycle-options-{Guid.NewGuid():N}")
            // Protected Pre-Initiation provisioning intentionally opens a serializable
            // transaction. The in-memory provider does not support transactions; the
            // SQLite-backed Pre-Initiation suite exercises the transactional path.
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        var scope = new ManagementScope(
            new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId });
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            userId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, projectId, scope));
        return (db, http, management);
    }

    private static ProjectPhaseCatalogEntry Phase(
        Guid tenantId, Guid id, string code, string name, bool isDefault = false, int order = 0) => new()
    {
        Id = id,
        TenantId = tenantId,
        Code = code,
        NormalizedCode = code.ToUpperInvariant(),
        NameEn = name,
        NameAr = $"Arabic {name}",
        DisplayOrder = order,
        IsDefault = isDefault,
        IsActive = true,
        CreatedBy = "lifecycle-test"
    };

    private static ProjectStatusCatalogEntry Status(
        Guid tenantId, Guid id, string code, string name, bool isDefault = false, int order = 0) => new()
    {
        Id = id,
        TenantId = tenantId,
        Code = code,
        NormalizedCode = code.ToUpperInvariant(),
        NameEn = name,
        NameAr = $"Arabic {name}",
        DisplayOrder = order,
        IsDefault = isDefault,
        IsActive = true,
        CreatedBy = "lifecycle-test"
    };
}

public sealed class LifecycleOptionsTestAuthOptions : AuthenticationSchemeOptions
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
}

public sealed class LifecycleOptionsTestAuthHandler(
    IOptionsMonitor<LifecycleOptionsTestAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<LifecycleOptionsTestAuthOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var session = Request.Headers["X-Lifecycle-Test-Session"].ToString();
        if (session == "missing") return Task.FromResult(AuthenticateResult.NoResult());
        if (session == "invalid") return Task.FromResult(AuthenticateResult.Fail("Invalid test session"));
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Options.UserId.ToString()),
            new Claim("tenantId", Options.TenantId.ToString()),
            new Claim("permission", ProjectLifecyclePermissions.PhaseChange),
            new Claim("permission", "Project.Create")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
