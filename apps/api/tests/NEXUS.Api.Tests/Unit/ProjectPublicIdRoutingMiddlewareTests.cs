using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class ProjectPublicIdRoutingMiddlewareTests
{
    [Fact]
    public async Task Resolves_public_id_to_internal_id_for_project_routes()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        await using var db = CreateDb(project);
        var context = AuthenticatedContext(tenantId);
        context.Request.RouteValues["projectId"] = project.PublicId.ToString("D");
        var nextCalled = false;
        var middleware = new ProjectPublicIdRoutingMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, NullLogger<ProjectPublicIdRoutingMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.True(nextCalled);
        Assert.Equal(project.Id, context.Request.RouteValues["projectId"]);
        Assert.Equal(project.Id, ProjectRequestContext.GetInternalProjectId(context));
        Assert.Equal(project.PublicId, ProjectRequestContext.GetPublicProjectId(context));
    }

    [Fact]
    public async Task Rejects_internal_project_id_in_external_route()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        await using var db = CreateDb(project);
        var context = AuthenticatedContext(tenantId);
        context.Request.RouteValues["projectId"] = project.Id.ToString("D");
        var nextCalled = false;
        var middleware = new ProjectPublicIdRoutingMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, NullLogger<ProjectPublicIdRoutingMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task Does_not_resolve_project_from_another_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantB, Name = "Project B", Code = "B" };
        await using var db = CreateDb(project);
        var context = AuthenticatedContext(tenantA);
        context.Request.Path = $"/api/projects/{project.PublicId:D}/plans";
        context.Request.Method = HttpMethods.Post;
        context.Request.RouteValues["projectId"] = project.PublicId.ToString("D");
        var nextCalled = false;
        var middleware = new ProjectPublicIdRoutingMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, NullLogger<ProjectPublicIdRoutingMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task Plan_create_outside_management_scope_is_rejected_before_controller_execution()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        var context = AuthenticatedContext(tenantId, userId);
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor { HttpContext = context });
        await using var db = CreateDb(project);
        context.Request.Path = $"/api/projects/{project.PublicId:D}/plans";
        context.Request.Method = HttpMethods.Post;
        context.Request.RouteValues["projectId"] = project.PublicId.ToString("D");

        var resolver = new Mock<IManagementScopeResolver>();
        resolver.Setup(item => item.ResolveAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ManagementPerspective?>(), It.IsAny<ManagementViewMode>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid resolvedUserId, Guid resolvedTenantId, ManagementPerspective? _, ManagementViewMode _, Guid? _, Guid? _, Guid? _, CancellationToken _) =>
                new ManagementContext(resolvedUserId, resolvedTenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
                    new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>())));

        var accessor = new ManagementContextAccessor();
        var controllerReached = false;
        var resourceScope = new ProjectResourceScopeMiddleware(_ => { controllerReached = true; return Task.CompletedTask; });
        var management = new ManagementContextMiddleware(http => resourceScope.InvokeAsync(http, db, accessor));
        var routing = new ProjectPublicIdRoutingMiddleware(http => management.InvokeAsync(
            http,
            resolver.Object,
            accessor,
            NullLogger<ManagementContextMiddleware>.Instance,
            Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>()));

        await routing.InvokeAsync(
            context,
            db,
            NullLogger<ProjectPublicIdRoutingMiddleware>.Instance,
            Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(controllerReached);
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
    }

    [Fact]
    public async Task Resolves_project_id_query_parameter_and_preserves_other_filters()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        await using var db = CreateDb(project);
        var context = AuthenticatedContext(tenantId);
        context.Request.QueryString = new QueryString($"?projectId={project.PublicId:D}&page=2");
        var middleware = new ProjectPublicIdRoutingMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, db, NullLogger<ProjectPublicIdRoutingMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.Equal(project.Id.ToString("D"), context.Request.Query["projectId"]);
        Assert.Equal("2", context.Request.Query["page"]);
        Assert.Equal(project.Id, ProjectRequestContext.GetInternalProjectId(context));
    }

    [Fact]
    public async Task Rejects_internal_project_id_in_external_query_filter()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        await using var db = CreateDb(project);
        var context = AuthenticatedContext(tenantId);
        context.Request.QueryString = new QueryString($"?projectId={project.Id:D}");
        var nextCalled = false;
        var middleware = new ProjectPublicIdRoutingMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, NullLogger<ProjectPublicIdRoutingMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task Resolves_comma_separated_public_project_ids_and_rewrites_the_filter()
    {
        var tenantId = Guid.NewGuid();
        var projectA = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        var projectB = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project B", Code = "B" };
        await using var db = CreateDb([projectA, projectB]);
        var context = AuthenticatedContext(tenantId);
        context.Request.QueryString = new QueryString($"?projectIds={projectA.PublicId:D},{projectB.PublicId:D}&page=3");
        var nextCalled = false;
        var middleware = new ProjectPublicIdRoutingMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, NullLogger<ProjectPublicIdRoutingMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.True(nextCalled);
        var resolved = context.Request.Query["projectIds"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, resolved.Length);
        Assert.Contains(projectA.Id.ToString("D"), resolved);
        Assert.Contains(projectB.Id.ToString("D"), resolved);
        Assert.Equal("3", context.Request.Query["page"]);
        Assert.Null(ProjectRequestContext.GetInternalProjectId(context));
    }

    [Fact]
    public async Task Resolves_repeated_public_project_ids_and_deduplicates_them()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        await using var db = CreateDb([project]);
        var context = AuthenticatedContext(tenantId);
        context.Request.QueryString = new QueryString($"?projectIds={project.PublicId:D}&projectIds={project.PublicId:D}");
        var nextCalled = false;
        var middleware = new ProjectPublicIdRoutingMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, NullLogger<ProjectPublicIdRoutingMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.True(nextCalled);
        Assert.Equal(project.Id.ToString("D"), context.Request.Query["projectIds"].ToString());
    }

    [Fact]
    public async Task Rejects_mixing_project_id_and_project_ids_filters()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        await using var db = CreateDb(project);
        var context = AuthenticatedContext(tenantId);
        context.Request.QueryString = new QueryString($"?projectId={project.PublicId:D}&projectIds={project.PublicId:D}");
        var nextCalled = false;
        var middleware = new ProjectPublicIdRoutingMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, NullLogger<ProjectPublicIdRoutingMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task Rejects_unknown_project_in_project_ids_filter()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        await using var db = CreateDb(project);
        var context = AuthenticatedContext(tenantId);
        context.Request.QueryString = new QueryString($"?projectIds={project.PublicId:D},{Guid.NewGuid():D}");
        var nextCalled = false;
        var middleware = new ProjectPublicIdRoutingMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, NullLogger<ProjectPublicIdRoutingMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task Rejects_internal_project_id_in_project_ids_filter()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        await using var db = CreateDb(project);
        var context = AuthenticatedContext(tenantId);
        context.Request.QueryString = new QueryString($"?projectIds={project.Id:D}");
        var nextCalled = false;
        var middleware = new ProjectPublicIdRoutingMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, NullLogger<ProjectPublicIdRoutingMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task Rejects_malformed_project_ids_filter()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        await using var db = CreateDb(project);
        var context = AuthenticatedContext(tenantId);
        context.Request.QueryString = new QueryString($"?projectIds={project.PublicId:D},not-a-guid");
        var nextCalled = false;
        var middleware = new ProjectPublicIdRoutingMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, NullLogger<ProjectPublicIdRoutingMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task Rejects_project_route_id_that_is_not_part_of_project_ids_filter()
    {
        var tenantId = Guid.NewGuid();
        var projectA = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        var projectB = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project B", Code = "B" };
        await using var db = CreateDb([projectA, projectB]);
        var context = AuthenticatedContext(tenantId);
        context.Request.RouteValues["projectId"] = projectA.PublicId.ToString("D");
        context.Request.QueryString = new QueryString($"?projectIds={projectB.PublicId:D}");
        var nextCalled = false;
        var middleware = new ProjectPublicIdRoutingMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, NullLogger<ProjectPublicIdRoutingMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("allowed-project", "owned-plan", 200)]
    [InlineData("allowed-project", "foreign-plan", 404)]
    [InlineData("other-project", "owned-plan", 404)]
    public async Task Public_url_and_nested_plan_are_checked_against_user_scope_and_parent_project(string scope, string selectedPlan, int expectedStatus)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectA = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        var projectB = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project B", Code = "B" };
        var planA = new Plan { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = projectA.Id, Name = "Plan A" };
        var planB = new Plan { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = projectB.Id, Name = "Plan B" };
        await using var db = CreateDb([projectA, projectB], [planA, planB]);
        var context = AuthenticatedContext(tenantId, userId);
        context.Request.Path = $"/api/projects/{projectA.PublicId:D}/plans/{(selectedPlan == "owned-plan" ? planA.Id : planB.Id):D}/activities";
        context.Request.RouteValues["projectId"] = projectA.PublicId.ToString("D");
        context.Request.RouteValues["planId"] = (selectedPlan == "owned-plan" ? planA.Id : planB.Id).ToString("D");

        var resolver = new Mock<IManagementScopeResolver>();
        resolver.Setup(item => item.ResolveAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ManagementPerspective?>(), It.IsAny<ManagementViewMode>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid resolvedUserId, Guid resolvedTenantId, ManagementPerspective? _, ManagementViewMode _, Guid? _, Guid? _, Guid? _, CancellationToken _) =>
                new ManagementContext(resolvedUserId, resolvedTenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
                    new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), scope == "allowed-project" ? new HashSet<Guid> { projectA.Id } : new HashSet<Guid> { projectB.Id })));

        var accessor = new ManagementContextAccessor();
        var nextCalled = false;
        var resourceScope = new ProjectResourceScopeMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var management = new ManagementContextMiddleware(http => resourceScope.InvokeAsync(http, db, accessor));
        var routing = new ProjectPublicIdRoutingMiddleware(http => management.InvokeAsync(http, resolver.Object, accessor, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>()));

        await routing.InvokeAsync(context, db, NullLogger<ProjectPublicIdRoutingMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedStatus == StatusCodes.Status200OK, nextCalled);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task Direct_project_public_id_url_is_independently_checked_against_management_scope(bool userHasProjectAccess, int expectedStatus)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        await using var db = CreateDb(project);
        var context = AuthenticatedContext(tenantId, userId);
        context.Request.Path = $"/api/projects/{project.PublicId:D}/overview";
        context.Request.RouteValues["id"] = project.PublicId.ToString("D");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = "Project" }), "Project overview"));

        var resolver = new Mock<IManagementScopeResolver>();
        resolver.Setup(item => item.ResolveAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ManagementPerspective?>(), It.IsAny<ManagementViewMode>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid resolvedUserId, Guid resolvedTenantId, ManagementPerspective? _, ManagementViewMode _, Guid? _, Guid? _, Guid? _, CancellationToken _) =>
                new ManagementContext(resolvedUserId, resolvedTenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
                    new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), userHasProjectAccess ? new HashSet<Guid> { project.Id } : new HashSet<Guid>())));

        var accessor = new ManagementContextAccessor();
        var nextCalled = false;
        var resourceScope = new ProjectResourceScopeMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var management = new ManagementContextMiddleware(http => resourceScope.InvokeAsync(http, db, accessor));
        var routing = new ProjectPublicIdRoutingMiddleware(http => management.InvokeAsync(http, resolver.Object, accessor, NullLogger<ManagementContextMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>()));

        await routing.InvokeAsync(context, db, NullLogger<ProjectPublicIdRoutingMiddleware>.Instance, Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedStatus == StatusCodes.Status200OK, nextCalled);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task Direct_project_child_lookup_requires_project_in_management_scope(bool inScope, int expectedStatus)
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        var budget = new NEXUS.Modules.CostManagement.Domain.Budget { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id, Name = "Project budget" };
        await using var db = CreateDb([project], budgets: [budget]);
        var context = AuthenticatedContext(tenantId);
        context.Request.Path = $"/api/budgets/{budget.Id:D}";
        context.Request.RouteValues["id"] = budget.Id.ToString("D");
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(Guid.NewGuid(), tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), inScope ? new HashSet<Guid> { project.Id } : new HashSet<Guid>())));
        var nextCalled = false;
        var middleware = new ProjectResourceScopeMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, accessor);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(inScope, nextCalled);
    }

    [Fact]
    public async Task Risk_action_must_belong_to_the_addressed_risk()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        var addressedRisk = new Risk { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id, Title = "Addressed" };
        var otherRisk = new Risk { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id, Title = "Other" };
        var foreignAction = new RiskAction { Id = Guid.NewGuid(), TenantId = tenantId, RiskId = otherRisk.Id, Title = "Foreign action" };
        await using var db = CreateDb([project], risks: [addressedRisk, otherRisk], riskActions: [foreignAction]);

        var context = AuthenticatedContext(tenantId);
        context.Request.Path = $"/api/risks/{addressedRisk.Id:D}/actions/{foreignAction.Id:D}";
        context.Request.RouteValues["riskId"] = addressedRisk.Id.ToString("D");
        context.Request.RouteValues["actionId"] = foreignAction.Id.ToString("D");
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(Guid.NewGuid(), tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { project.Id })));
        var nextCalled = false;
        var middleware = new ProjectResourceScopeMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, accessor);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Plan_activity_must_belong_to_the_addressed_plan()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        var planA = new Plan { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id, Name = "Plan A" };
        var planB = new Plan { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id, Name = "Plan B" };
        var versionA = new PlanVersion { Id = Guid.NewGuid(), TenantId = tenantId, PlanId = planA.Id, VersionNumber = 1 };
        var versionB = new PlanVersion { Id = Guid.NewGuid(), TenantId = tenantId, PlanId = planB.Id, VersionNumber = 1 };
        var activityB = new PlanActivity { Id = Guid.NewGuid(), TenantId = tenantId, PlanVersionId = versionB.Id, Code = "B-1", Name = "Activity in Plan B" };
        await using var db = CreateDb([project], plans: [planA, planB], planVersions: [versionA, versionB], activities: [activityB]);

        var context = AuthenticatedContext(tenantId);
        context.Request.Path = $"/api/projects/{project.PublicId:D}/plans/{planA.Id:D}/activities/{activityB.Id:D}";
        context.Request.RouteValues["projectId"] = project.PublicId.ToString("D");
        context.Request.RouteValues["planId"] = planA.Id.ToString("D");
        context.Request.RouteValues["activityId"] = activityB.Id.ToString("D");
        context.Items[ProjectRequestContext.InternalProjectIdKey] = project.Id;
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(Guid.NewGuid(), tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { project.Id })));
        var nextCalled = false;
        var middleware = new ProjectResourceScopeMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, accessor);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Plan_predecessor_bulk_route_is_allowed_after_parent_plan_validation()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        var plan = new Plan { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id, Name = "Plan A" };
        await using var db = CreateDb([project], [plan]);
        var context = AuthenticatedContext(tenantId);
        context.Request.Path = $"/api/projects/{project.PublicId:D}/plans/{plan.Id:D}/dependencies/predecessors";
        context.Request.RouteValues["projectId"] = project.PublicId.ToString("D");
        context.Request.RouteValues["planId"] = plan.Id.ToString("D");
        context.Items[ProjectRequestContext.InternalProjectIdKey] = project.Id;
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(Guid.NewGuid(), tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { project.Id })));
        var nextCalled = false;

        await new ProjectResourceScopeMiddleware(_ => { nextCalled = true; return Task.CompletedTask; })
            .InvokeAsync(context, db, accessor);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Plan_assignment_batch_route_is_allowed_only_for_the_addressed_project_plan()
    {
        var tenantId = Guid.NewGuid();
        var projectA = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        var projectB = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project B", Code = "B" };
        var planA = new Plan { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = projectA.Id, Name = "Plan A" };
        var planB = new Plan { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = projectB.Id, Name = "Plan B" };
        await using var db = CreateDb([projectA, projectB], [planA, planB]);
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(Guid.NewGuid(), tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectA.Id })));

        var validContext = AuthenticatedContext(tenantId);
        validContext.Request.Path = $"/api/projects/{projectA.PublicId:D}/plans/{planA.Id:D}/assignments/batch";
        validContext.Request.Method = HttpMethods.Post;
        validContext.Request.RouteValues["projectId"] = projectA.PublicId.ToString("D");
        validContext.Request.RouteValues["planId"] = planA.Id.ToString("D");
        validContext.Items[ProjectRequestContext.InternalProjectIdKey] = projectA.Id;
        var validNextCalled = false;
        await new ProjectResourceScopeMiddleware(_ => { validNextCalled = true; return Task.CompletedTask; })
            .InvokeAsync(validContext, db, accessor);

        var foreignPlanContext = AuthenticatedContext(tenantId);
        foreignPlanContext.Request.Path = $"/api/projects/{projectA.PublicId:D}/plans/{planB.Id:D}/assignments/batch";
        foreignPlanContext.Request.Method = HttpMethods.Post;
        foreignPlanContext.Request.RouteValues["projectId"] = projectA.PublicId.ToString("D");
        foreignPlanContext.Request.RouteValues["planId"] = planB.Id.ToString("D");
        foreignPlanContext.Items[ProjectRequestContext.InternalProjectIdKey] = projectA.Id;
        var foreignPlanNextCalled = false;
        await new ProjectResourceScopeMiddleware(_ => { foreignPlanNextCalled = true; return Task.CompletedTask; })
            .InvokeAsync(foreignPlanContext, db, accessor);

        Assert.Equal(StatusCodes.Status200OK, validContext.Response.StatusCode);
        Assert.True(validNextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, foreignPlanContext.Response.StatusCode);
        Assert.False(foreignPlanNextCalled);
    }

    [Fact]
    public async Task Meeting_and_work_item_attachments_must_belong_to_the_addressed_parent()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        var meetingA = new Meeting { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id, Title = "Meeting A" };
        var meetingB = new Meeting { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id, Title = "Meeting B" };
        var minutesB = new MeetingMinutes { Id = Guid.NewGuid(), TenantId = tenantId, MeetingId = meetingB.Id };
        var minutesAttachment = new MeetingMinutesAttachment { Id = Guid.NewGuid(), TenantId = tenantId, MeetingMinutesId = minutesB.Id, FileName = "minutes.pdf", ContentType = "application/pdf" };
        var workItemA = new WorkItem { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id, Title = "Work A" };
        var workItemB = new WorkItem { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id, Title = "Work B" };
        var workItemAttachment = new WorkItemAttachment { Id = Guid.NewGuid(), TenantId = tenantId, WorkItemId = workItemB.Id, FileName = "work.pdf", ContentType = "application/pdf" };
        await using var db = CreateDb([project], meetings: [meetingA, meetingB], meetingMinutes: [minutesB],
            meetingAttachments: [minutesAttachment], workItems: [workItemA, workItemB], workItemAttachments: [workItemAttachment]);
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(Guid.NewGuid(), tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { project.Id })));

        var meetingContext = AuthenticatedContext(tenantId);
        meetingContext.Request.Path = $"/api/meetings/{meetingA.Id:D}/minutes/attachments/{minutesAttachment.Id:D}/content";
        meetingContext.Request.RouteValues["id"] = meetingA.Id.ToString("D");
        meetingContext.Request.RouteValues["attachmentId"] = minutesAttachment.Id.ToString("D");
        var meetingNextCalled = false;
        await new ProjectResourceScopeMiddleware(_ => { meetingNextCalled = true; return Task.CompletedTask; })
            .InvokeAsync(meetingContext, db, accessor);

        var workItemContext = AuthenticatedContext(tenantId);
        workItemContext.Request.Path = $"/api/work-items/{workItemA.Id:D}/attachments/{workItemAttachment.Id:D}/content";
        workItemContext.Request.RouteValues["id"] = workItemA.Id.ToString("D");
        workItemContext.Request.RouteValues["attachmentId"] = workItemAttachment.Id.ToString("D");
        var workItemNextCalled = false;
        await new ProjectResourceScopeMiddleware(_ => { workItemNextCalled = true; return Task.CompletedTask; })
            .InvokeAsync(workItemContext, db, accessor);

        Assert.Equal(StatusCodes.Status404NotFound, meetingContext.Response.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, workItemContext.Response.StatusCode);
        Assert.False(meetingNextCalled);
        Assert.False(workItemNextCalled);
    }

    [Fact]
    public async Task Project_person_must_belong_to_the_addressed_project()
    {
        var tenantId = Guid.NewGuid();
        var projectA = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        var projectB = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project B", Code = "B" };
        var person = new ProjectMember { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = projectB.Id, ResourceId = Guid.NewGuid() };
        await using var db = CreateDb([projectA, projectB], projectMembers: [person]);
        var context = AuthenticatedContext(tenantId);
        context.Request.Path = $"/api/projects/{projectA.PublicId:D}/people/{person.Id:D}";
        context.Request.RouteValues["projectId"] = projectA.PublicId.ToString("D");
        context.Request.RouteValues["personId"] = person.Id.ToString("D");
        context.Items[ProjectRequestContext.InternalProjectIdKey] = projectA.Id;
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(Guid.NewGuid(), tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectA.Id })));
        var nextCalled = false;

        await new ProjectResourceScopeMiddleware(_ => { nextCalled = true; return Task.CompletedTask; })
            .InvokeAsync(context, db, accessor);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public void Project_public_ids_are_random_v4_and_have_a_unique_database_index()
    {
        var project = new Project();
        var id = project.PublicId.ToString("N");
        Assert.Equal('4', id[12]);
        Assert.Contains(id[16], new[] { '8', '9', 'a', 'b' });

        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        using var db = new AppDbContext(options);
        var index = db.Model.FindEntityType(typeof(Project))!.GetIndexes()
            .Single(candidate => candidate.Properties.Any(property => property.Name == nameof(Project.PublicId)));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public async Task Missing_project_owned_resource_is_rejected_before_controller()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb([]);
        var missingBudgetId = Guid.NewGuid();
        var context = AuthenticatedContext(tenantId);
        context.Request.Path = $"/api/budgets/{missingBudgetId:D}";
        context.Request.RouteValues["id"] = missingBudgetId.ToString("D");
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(Guid.NewGuid(), tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>())));
        var nextCalled = false;
        var middleware = new ProjectResourceScopeMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db, accessor);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Duplicate_project_id_fields_in_body_are_rejected_before_controller()
    {
        var tenantId = Guid.NewGuid();
        var projectA = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project A", Code = "A" };
        var projectB = new Project { Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), TenantId = tenantId, Name = "Project B", Code = "B" };
        await using var db = CreateDb([projectA, projectB]);
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(Guid.NewGuid(), tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectA.Id })));
        var context = AuthenticatedContext(tenantId);
        context.Request.Path = "/api/work-items";
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes($"{{\"projectId\":\"{projectA.Id:D}\",\"projectId\":\"{projectB.Id:D}\"}}"));
        var nextCalled = false;

        await new ProjectResourceScopeMiddleware(_ => { nextCalled = true; return Task.CompletedTask; })
            .InvokeAsync(context, db, accessor);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    private static AppDbContext CreateDb(Project project) => CreateDb([project]);

    private static AppDbContext CreateDb(
        Project[] projects,
        Plan[]? plans = null,
        NEXUS.Modules.CostManagement.Domain.Budget[]? budgets = null,
        Risk[]? risks = null,
        RiskAction[]? riskActions = null,
        PlanVersion[]? planVersions = null,
        PlanActivity[]? activities = null,
        Meeting[]? meetings = null,
        MeetingMinutes[]? meetingMinutes = null,
        MeetingMinutesAttachment[]? meetingAttachments = null,
        WorkItem[]? workItems = null,
        WorkItemAttachment[]? workItemAttachments = null,
        ProjectMember[]? projectMembers = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);
        db.Projects.AddRange(projects);
        if (plans is not null) db.Plans.AddRange(plans);
        if (budgets is not null) db.Budgets.AddRange(budgets);
        if (risks is not null) db.Risks.AddRange(risks);
        if (riskActions is not null) db.RiskActions.AddRange(riskActions);
        if (planVersions is not null) db.PlanVersions.AddRange(planVersions);
        if (activities is not null) db.PlanActivities.AddRange(activities);
        if (meetings is not null) db.Meetings.AddRange(meetings);
        if (meetingMinutes is not null) db.MeetingMinutes.AddRange(meetingMinutes);
        if (meetingAttachments is not null) db.MeetingMinutesAttachments.AddRange(meetingAttachments);
        if (workItems is not null) db.WorkItems.AddRange(workItems);
        if (workItemAttachments is not null) db.WorkItemAttachments.AddRange(workItemAttachments);
        if (projectMembers is not null) db.ProjectMembers.AddRange(projectMembers);
        db.SaveChanges();
        return db;
    }

    private static DefaultHttpContext AuthenticatedContext(Guid tenantId, Guid? userId = null)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString("D")),
            new Claim("tenantId", tenantId.ToString("D"))
        }, "test"));
        return context;
    }
}
