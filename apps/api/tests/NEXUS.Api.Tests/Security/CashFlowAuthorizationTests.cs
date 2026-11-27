using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using NEXUS.Api.Authorization;
using NEXUS.Api.Finance;
using NEXUS.Api.ProgramManagement;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using Xunit;
using AspNetAuthorizationService = Microsoft.AspNetCore.Authorization.IAuthorizationService;

namespace NEXUS.Api.Tests.Security;

public sealed class CashFlowAuthorizationTests
{
    private const string FinancialFoundationView = "ProjectFinancialFoundation.View";
    private const string ProjectSettingsView = "ProjectSettings.View";

    [Fact]
    public async Task Cash_flow_read_requires_both_existing_project_scoped_view_permissions()
    {
        var controller = typeof(ProjectCashFlowController);
        var action = controller.GetMethod(nameof(ProjectCashFlowController.Get));
        Assert.NotNull(action);
        Assert.NotNull(controller.GetCustomAttribute<ApiControllerAttribute>());
        Assert.Contains(controller.GetCustomAttributes<RouteAttribute>(), route => route.Template == "api/projects/{projectId:guid}/cash-flow");
        Assert.Contains(action!.GetCustomAttributes<HttpGetAttribute>(), route => string.IsNullOrEmpty(route.Template));

        var requiredPolicies = controller.GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Concat(action.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy))
            .Where(policy => !string.IsNullOrWhiteSpace(policy))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Contains(FinancialFoundationView, requiredPolicies);
        Assert.Contains(ProjectSettingsView, requiredPolicies);

        AssertProjectScopedViewPermission(FinancialFoundationView);
        AssertProjectScopedViewPermission(ProjectSettingsView);

        var authorization = CreateAuthorizationService();
        var both = PrincipalWith(FinancialFoundationView, ProjectSettingsView);
        Assert.True((await authorization.AuthorizeAsync(both, FinancialFoundationView)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(both, ProjectSettingsView)).Succeeded);

        Assert.False((await authorization.AuthorizeAsync(
            PrincipalWith(FinancialFoundationView), ProjectSettingsView)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            PrincipalWith(ProjectSettingsView), FinancialFoundationView)).Succeeded);

        foreach (var legacyPermission in new[] { "ProjectFinance.View", "finances.read" })
        {
            var legacyOnly = PrincipalWith(legacyPermission);
            Assert.False((await authorization.AuthorizeAsync(legacyOnly, FinancialFoundationView)).Succeeded);
            Assert.False((await authorization.AuthorizeAsync(legacyOnly, ProjectSettingsView)).Succeeded);
        }
    }

    [Fact]
    public void Program_cash_flow_endpoint_requires_program_view_and_program_financial_rollup_view()
    {
        AssertEndpointPolicies(
            nameof(ProgramPortfolioCashFlowController.GetProgramCashFlow),
            "program-management/programs/{programId:guid}/cash-flow",
            "Program.View",
            "ProgramFinancialRollup.View");
    }

    [Fact]
    public void Portfolio_cash_flow_endpoint_requires_portfolio_view_and_portfolio_financial_rollup_view()
    {
        AssertEndpointPolicies(
            nameof(ProgramPortfolioCashFlowController.GetPortfolioCashFlow),
            "portfolio-management/portfolios/{portfolioId:guid}/cash-flow",
            "Portfolio.View",
            "PortfolioFinancialRollup.View");
    }

    private static void AssertEndpointPolicies(
        string actionName,
        string expectedRoute,
        string scopeViewPermission,
        string financialRollupViewPermission)
    {
        var controller = typeof(ProgramPortfolioCashFlowController);
        var action = controller.GetMethod(actionName);
        Assert.NotNull(action);

        var route = Assert.Single(action!.GetCustomAttributes<HttpGetAttribute>());
        Assert.Equal(expectedRoute, route.Template);

        var policies = action.GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Where(policy => !string.IsNullOrWhiteSpace(policy))
            .ToArray();
        Assert.Contains(scopeViewPermission, policies);
        Assert.Contains(financialRollupViewPermission, policies);
    }

    private static void AssertProjectScopedViewPermission(string key)
    {
        Assert.True(PermissionCatalog.ByKey.TryGetValue(key, out var permission), $"Missing permission catalog entry: {key}");
        Assert.NotNull(permission);
        Assert.True(permission!.IsActive);
        Assert.Equal(PermissionScope.Project, permission.Scope);
        Assert.Equal(PermissionLevel.View, permission.Level);
    }

    private static AspNetAuthorizationService CreateAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddCanonicalFinancialPolicies();
            options.AddPolicy(ProjectSettingsView, policy =>
                policy.RequireClaim("permission", ProjectSettingsView));
        });
        return services.BuildServiceProvider().GetRequiredService<AspNetAuthorizationService>();
    }

    private static ClaimsPrincipal PrincipalWith(params string[] permissions) =>
        new(new ClaimsIdentity(permissions.Select(permission => new Claim("permission", permission)), "test"));
}
