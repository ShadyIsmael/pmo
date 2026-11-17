using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using NEXUS.Api.Authorization;
using NEXUS.Api.Finance;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ResourceManagement.Api;
using Xunit;
using AspNetAuthorizationService = Microsoft.AspNetCore.Authorization.IAuthorizationService;

namespace NEXUS.Api.Tests.Security;

public sealed class CostUtilizationExportAuthorizationTests
{
    [Fact]
    public async Task Financial_foundation_read_has_a_dedicated_project_scoped_view_key()
    {
        var method = typeof(ProjectFinancialFoundationController).GetMethod(nameof(ProjectFinancialFoundationController.Get));
        Assert.NotNull(method);
        Assert.Equal("ProjectFinancialFoundation.View", method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Contains(method.GetCustomAttributes<HttpGetAttribute>(), route => string.IsNullOrEmpty(route.Template));

        var permission = PermissionCatalog.ByKey["ProjectFinancialFoundation.View"];
        Assert.True(permission.IsActive);
        Assert.Equal(PermissionLevel.View, permission.Level);
        Assert.Equal(PermissionScope.Project, permission.Scope);
        Assert.True(permission.IsAssignable);
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(permission.Key));
        Assert.Empty(permission.RequiredPermissionKeys);

        var authorization = CreateAuthorizationService();
        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("ProjectFinancialFoundation.View"), "ProjectFinancialFoundation.View")).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Project.Edit"), "ProjectFinancialFoundation.View")).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("finances.read"), "ProjectFinancialFoundation.View")).Succeeded);
    }

    [Fact]
    public void Report_read_and_export_routes_use_distinct_operations()
    {
        var controller = typeof(CostUtilizationAnalyticsController);
        var read = controller.GetMethod(nameof(CostUtilizationAnalyticsController.Get));
        var export = controller.GetMethod(nameof(CostUtilizationAnalyticsController.GetExportData));

        Assert.NotNull(read);
        Assert.NotNull(export);
        Assert.Equal("Resource.View", read!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("CostUtilization.Export", export!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Contains(export.GetCustomAttributes<HttpGetAttribute>(), route => route.Template == "export-data");
    }

    [Fact]
    public void Export_capability_is_sensitive_scoped_and_not_in_admin_baseline()
    {
        Assert.True(PermissionCatalog.ByKey.TryGetValue("CostUtilization.Export", out var permission));
        Assert.NotNull(permission);
        Assert.True(permission!.IsActive);
        Assert.Equal(PermissionLevel.Action, permission.Level);
        Assert.Equal(PermissionScope.Tenant, permission.Scope);
        Assert.True(permission.IsAssignable);
        Assert.True(permission.IsSensitive);
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(permission.Key));
        Assert.Contains("Resource.View", permission.RequiredPermissionKeys);
        Assert.False(string.IsNullOrWhiteSpace(permission.LabelEn));
        Assert.False(string.IsNullOrWhiteSpace(permission.LabelAr));
        Assert.False(string.IsNullOrWhiteSpace(permission.DescriptionEn));
        Assert.False(string.IsNullOrWhiteSpace(permission.DescriptionAr));
    }

    [Fact]
    public async Task Export_requires_resource_read_and_export_allow_and_fails_closed_without_either()
    {
        var authorization = CreateAuthorizationService();

        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Resource.View", "CostUtilization.Export"), "CostUtilization.Export")).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Resource.View"), "CostUtilization.Export")).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("CostUtilization.Export"), "CostUtilization.Export")).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Unrelated.Permission"), "CostUtilization.Export")).Succeeded);
    }

    [Fact]
    public async Task Direct_deny_for_export_overrides_inherited_export_and_read_grants()
    {
        var authorization = CreateAuthorizationService();
        var effective = EffectivePermissionResolver.Resolve(
            ["Resource.View", "CostUtilization.Export"],
            [("CostUtilization.Export", PermissionOverrideEffect.Deny)]);

        Assert.Contains("Resource.View", effective);
        Assert.DoesNotContain("CostUtilization.Export", effective);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith(effective), "CostUtilization.Export")).Succeeded);
    }

    private static AspNetAuthorizationService CreateAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Resource.View", policy => policy.RequireClaim("permission", "Resource.View"));
            options.AddCanonicalFinancialPolicies();
        });
        return services.BuildServiceProvider().GetRequiredService<AspNetAuthorizationService>();
    }

    private static ClaimsPrincipal PrincipalWith(params string[] permissions) =>
        new(new ClaimsIdentity(permissions.Select(permission => new Claim("permission", permission)), "test"));

    private static ClaimsPrincipal PrincipalWith(IEnumerable<string> permissions) =>
        new(new ClaimsIdentity(permissions.Select(permission => new Claim("permission", permission)), "test"));
}
