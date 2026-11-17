using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ProjectTeamCapacityAuthorizationTests
{
    [Fact]
    public async Task Team_capacity_requires_both_view_permissions_and_active_deny_removes_access()
    {
        var action = typeof(ProjectController).GetMethod(nameof(ProjectController.GetTeamCapacityOverview))!;
        var requiredPolicies = action.GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Where(policy => !string.IsNullOrWhiteSpace(policy))
            .Select(policy => policy!)
            .ToArray();

        Assert.Contains("Project.View", requiredPolicies);
        Assert.Contains("Resource.View", requiredPolicies);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Project.View", policy => policy.RequireClaim("permission", "Project.View"));
            options.AddPolicy("Resource.View", policy => policy.RequireClaim("permission", "Resource.View"));
        });
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
        var combinedPolicy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(),
            requiredPolicies.Select(policy => (IAuthorizeData)new AuthorizeAttribute { Policy = policy }));
        Assert.NotNull(combinedPolicy);

        Assert.True((await authorization.AuthorizeAsync(
            PrincipalWith("Project.View", "Resource.View"), null, combinedPolicy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Project.View"), null, combinedPolicy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Resource.View"), null, combinedPolicy)).Succeeded);

        var effectiveAfterDeny = EffectivePermissionResolver.Resolve(
            ["Project.View", "Resource.View"],
            [("Resource.View", PermissionOverrideEffect.Deny)]);
        Assert.DoesNotContain("Resource.View", effectiveAfterDeny);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith(effectiveAfterDeny), null, combinedPolicy)).Succeeded);
    }

    [Fact]
    public async Task Team_capacity_controller_returns_ok_when_scoped_capacity_service_succeeds()
    {
        var projectId = Guid.NewGuid();
        var startDate = new DateTime(2026, 11, 1);
        var endDate = new DateTime(2026, 12, 1);
        var dto = new ProjectTeamCapacityOverviewDto
        {
            StartDate = startDate,
            EndDate = endDate,
            ActiveResourceCount = 1,
            IsCapacityDataAvailable = true,
            IsAllocationDataAvailable = true
        };
        var overview = new Mock<IProjectOverviewService>();
        overview.Setup(service => service.GetTeamCapacityAsync(
                projectId, startDate, endDate, "Month", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProjectTeamCapacityOverviewDto>.Success(dto));

        var controller = new ProjectController(
            Mock.Of<IProjectService>(),
            Mock.Of<IProjectSnapshotService>(),
            Mock.Of<IProjectHandoverRequestService>(),
            Mock.Of<IProjectStatusReportService>(),
            overview.Object,
            Mock.Of<IProjectReportingService>());

        var result = await controller.GetTeamCapacityOverview(projectId, startDate, endDate, "Month");

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.Same(dto, response.Value);
        overview.Verify(service => service.GetTeamCapacityAsync(
            projectId, startDate, endDate, "Month", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ClaimsPrincipal PrincipalWith(IEnumerable<string> permissions) =>
        new(new ClaimsIdentity(permissions.Select(permission => new Claim("permission", permission)), "team-capacity-test"));

    private static ClaimsPrincipal PrincipalWith(params string[] permissions) => PrincipalWith((IEnumerable<string>)permissions);
}
