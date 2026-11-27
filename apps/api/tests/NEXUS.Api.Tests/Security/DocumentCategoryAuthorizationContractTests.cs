using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NEXUS.Modules.Documents.Api;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Identity.Application;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class DocumentCategoryAuthorizationContractTests
{
    [Fact]
    public void Category_management_endpoints_use_granular_canonical_permissions()
    {
        var controller = typeof(DocumentCategoryController);
        Assert.Equal("api/document-categories", controller.GetCustomAttribute<RouteAttribute>()?.Template);

        var actions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => new
            {
                Route = method.GetCustomAttributes<HttpMethodAttribute>().SingleOrDefault(),
                Policy = method.GetCustomAttribute<AuthorizeAttribute>()?.Policy
            })
            .Where(action => action.Route is not null)
            .ToArray();

        Assert.Equal(8, actions.Length);
        Assert.Collection(actions.Where(action => action.Policy != DocumentCategoryPermissions.TemplateOptionsLookupPolicy
                    && action.Route!.Template != "project-upload-options")
                .OrderBy(action => action.Route!.HttpMethods.Single()).ThenBy(action => action.Route!.Template),
            action => AssertAction(action.Route!, action.Policy, "DELETE", "{id:guid}", "DocumentCategory.Delete"),
            action => AssertAction(action.Route!, action.Policy, "GET", null, "DocumentCategory.View"),
            action => AssertAction(action.Route!, action.Policy, "GET", "{id:guid}", "DocumentCategory.View"),
            action => AssertAction(action.Route!, action.Policy, "PATCH", "{id:guid}/status", "DocumentCategory.ActivateDeactivate"),
            action => AssertAction(action.Route!, action.Policy, "POST", null, "DocumentCategory.Create"),
            action => AssertAction(action.Route!, action.Policy, "PUT", "{id:guid}", "DocumentCategory.Edit"));

        var templateOptions = Assert.Single(actions, action => action.Route?.Template == "template-options");
        Assert.Contains("GET", templateOptions.Route!.HttpMethods);
        Assert.Equal("template-options", templateOptions.Route.Template);
        Assert.Equal(DocumentCategoryPermissions.TemplateOptionsLookupPolicy, templateOptions.Policy);

        var projectUploadOptions = Assert.Single(actions, action => action.Route?.Template == "project-upload-options");
        Assert.Contains("GET", projectUploadOptions.Route!.HttpMethods);
        Assert.Equal(DocumentPermissions.Create, projectUploadOptions.Policy);
        Assert.Contains(DocumentPermissions.Create, PermissionCatalog.ByKey.Keys);
    }

    [Fact]
    public void Existing_template_dropdown_keeps_document_view_permission_and_separate_route()
    {
        var controller = typeof(DocumentController);
        Assert.Equal("api/documents", controller.GetCustomAttribute<RouteAttribute>()?.Template);

        var dropdown = controller.GetMethod(nameof(DocumentController.GetCategories));
        Assert.NotNull(dropdown);
        Assert.Equal("Document.View", dropdown!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Contains(dropdown.GetCustomAttributes<HttpGetAttribute>(), route => route.Template == "categories");
        Assert.Contains(dropdown.GetParameters(), parameter => parameter.Name == "organizationId");
    }

    [Fact]
    public void Category_policy_names_are_registered_explicitly_in_the_API()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "apps/api/src/NEXUS.Api/Program.cs"));
        var registrations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["View"] = "DocumentCategory.View",
            ["Create"] = "DocumentCategory.Create",
            ["Edit"] = "DocumentCategory.Edit",
            ["ActivateDeactivate"] = "DocumentCategory.ActivateDeactivate",
            ["Delete"] = "DocumentCategory.Delete"
        };

        foreach (var (member, key) in registrations)
        {
            Assert.Contains($"options.AddPolicy(DocumentCategoryPermissions.{member}", program, StringComparison.Ordinal);
            Assert.Contains(key, PermissionCatalog.ByKey.Keys);
        }

        Assert.Contains("options.AddPolicy(DocumentCategoryPermissions.TemplateOptionsLookupPolicy", program, StringComparison.Ordinal);
        Assert.Contains(".RequireClaim(\"permission\", DocumentPermissions.Create)", program, StringComparison.Ordinal);
        Assert.Contains(".RequireClaim(\"permission\", DocumentPermissions.TemplateManage)", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Category_delete_permission_is_assignable_and_part_of_the_protected_admin_baseline()
    {
        var permission = Assert.Single(PermissionCatalog.Entries, entry => entry.Key == DocumentCategoryPermissions.Delete);

        Assert.Equal("Delete", permission.LabelEn);
        Assert.Equal("حذف", permission.LabelAr);
        Assert.Equal(PermissionScope.Tenant, permission.Scope);
        Assert.True(permission.IsAssignable);
        Assert.True(permission.IsTenantAdminBaseline);
        Assert.Contains(DocumentCategoryPermissions.View, permission.RequiredPermissionKeys);
        Assert.Equal(RoleCategories.Selectable, permission.AllowedCategories);
        Assert.Contains(DocumentCategoryPermissions.Delete, ProtectedTenantAdminPolicy.MandatoryPermissionKeys);
    }

    private static void AssertAction(HttpMethodAttribute route, string? policy, string method, string? template, string expectedPolicy)
    {
        Assert.Contains(method, route.HttpMethods);
        Assert.Equal(template, route.Template);
        Assert.Equal(expectedPolicy, policy);
        Assert.Contains(expectedPolicy, PermissionCatalog.ByKey.Keys);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                    && Directory.Exists(Path.Combine(directory.FullName, "apps")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root for source contract assertions.");
    }
}
