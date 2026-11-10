using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NEXUS.Api.Authorization;
using NEXUS.Api.Finance;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ProjectAnnualBudgetAssignedReviewAuthorizationTests
{
    [Fact]
    public async Task Assigned_review_history_and_detail_require_view_but_not_approve()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddCanonicalFinancialPolicies());
        using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        foreach (var actionName in new[]
                 {
                     nameof(ProjectAnnualBudgetAssignedApprovalsController.GetMyAssignedApprovalHistory),
                     nameof(ProjectAnnualBudgetAssignedApprovalsController.GetMyAssignedApprovalDetail)
                 })
        {
            var action = typeof(ProjectAnnualBudgetAssignedApprovalsController).GetMethod(actionName);
            Assert.NotNull(action);
            var endpointPolicy = await ResolveEndpointPolicy(policyProvider, action!);

            Assert.True((await authorization.AuthorizeAsync(
                PrincipalWith("ProjectAnnualBudget.View"), resource: null, endpointPolicy)).Succeeded);
            Assert.False((await authorization.AuthorizeAsync(
                PrincipalWith("ProjectAnnualBudget.Approve"), resource: null, endpointPolicy)).Succeeded);
            Assert.False((await authorization.AuthorizeAsync(
                PrincipalWith("Unrelated.Permission"), resource: null, endpointPolicy)).Succeeded);
        }
    }

    [Fact]
    public async Task Actionable_inbox_requires_approve_and_preserves_view_dependency_and_active_deny()
    {
        var action = typeof(ProjectAnnualBudgetAssignedApprovalsController)
            .GetMethod(nameof(ProjectAnnualBudgetAssignedApprovalsController.GetMyActionableApprovals));
        Assert.NotNull(action);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddCanonicalFinancialPolicies());
        using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var endpointPolicy = await ResolveEndpointPolicy(policyProvider, action!);

        Assert.True((await authorization.AuthorizeAsync(
            PrincipalWith("ProjectAnnualBudget.Approve", "ProjectAnnualBudget.View"), resource: null, endpointPolicy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            PrincipalWith("ProjectAnnualBudget.View"), resource: null, endpointPolicy)).Succeeded);

        var approve = Assert.Single(PermissionCatalog.Entries, item => item.Key == "ProjectAnnualBudget.Approve");
        Assert.Contains("ProjectAnnualBudget.View", approve.RequiredPermissionKeys);
        Assert.Equal(PermissionScope.Project, approve.Scope);

        var denied = EffectivePermissionResolver.Resolve(
            ["ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"],
            [("ProjectAnnualBudget.Approve", PermissionOverrideEffect.Deny)]);
        Assert.Contains("ProjectAnnualBudget.View", denied);
        Assert.DoesNotContain("ProjectAnnualBudget.Approve", denied);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith(denied), resource: null, endpointPolicy)).Succeeded);
    }

    [Fact]
    public void Read_routes_are_not_accidentally_inherited_from_the_actionable_approve_policy()
    {
        var controllerType = typeof(ProjectAnnualBudgetAssignedApprovalsController);
        var controllerAuth = Assert.Single(controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: false));
        Assert.Null(controllerAuth.Policy);

        Assert.Equal(
            "ProjectAnnualBudget.Approve",
            controllerType.GetMethod(nameof(ProjectAnnualBudgetAssignedApprovalsController.GetMyActionableApprovals))!
                .GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(
            "ProjectAnnualBudget.View",
            controllerType.GetMethod(nameof(ProjectAnnualBudgetAssignedApprovalsController.GetMyAssignedApprovalHistory))!
                .GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(
            "ProjectAnnualBudget.View",
            controllerType.GetMethod(nameof(ProjectAnnualBudgetAssignedApprovalsController.GetMyAssignedApprovalDetail))!
                .GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    private static async Task<AuthorizationPolicy> ResolveEndpointPolicy(
        IAuthorizationPolicyProvider policyProvider,
        System.Reflection.MethodInfo action)
    {
        var controllerType = typeof(ProjectAnnualBudgetAssignedApprovalsController);
        var authorizationData = controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(action.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .Cast<IAuthorizeData>()
            .ToArray();
        var policy = await AuthorizationPolicy.CombineAsync(policyProvider, authorizationData);
        return Assert.IsType<AuthorizationPolicy>(policy);
    }

    private static ClaimsPrincipal PrincipalWith(IEnumerable<string> permissions) => new(
        new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
                new Claim("tenantId", Guid.NewGuid().ToString("D")),
                .. permissions.Select(permission => new Claim("permission", permission))
            ],
            authenticationType: "test"));

    private static ClaimsPrincipal PrincipalWith(params string[] permissions) => PrincipalWith((IEnumerable<string>)permissions);
}
