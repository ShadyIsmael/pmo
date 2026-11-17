using NEXUS.Modules.Identity.Application;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ProjectLifecyclePermissionContractTests
{
    private static readonly IReadOnlyDictionary<string, PermissionLevel> CatalogPermissions =
        new Dictionary<string, PermissionLevel>(StringComparer.Ordinal)
        {
            ["ProjectLifecyclePhase.View"] = PermissionLevel.View,
            ["ProjectLifecyclePhase.Create"] = PermissionLevel.Action,
            ["ProjectLifecyclePhase.Edit"] = PermissionLevel.Action,
            ["ProjectLifecyclePhase.ActivateDeactivate"] = PermissionLevel.Action,
            ["ProjectLifecycleStatus.View"] = PermissionLevel.View,
            ["ProjectLifecycleStatus.Create"] = PermissionLevel.Action,
            ["ProjectLifecycleStatus.Edit"] = PermissionLevel.Action,
            ["ProjectLifecycleStatus.ActivateDeactivate"] = PermissionLevel.Action
        };

    [Fact]
    public void Catalog_permissions_are_bilingual_assignable_and_separate_by_catalog()
    {
        foreach (var (key, expectedLevel) in CatalogPermissions)
        {
            var permission = PermissionCatalog.ByKey[key];

            Assert.True(permission.IsActive);
            Assert.True(permission.IsAssignable);
            Assert.Equal(PermissionScope.Tenant, permission.Scope);
            Assert.Equal(expectedLevel, permission.Level);
            Assert.Equal("administration", permission.ModuleId);
            Assert.Equal("project-lifecycle", permission.ScreenId);
            Assert.False(string.IsNullOrWhiteSpace(permission.LabelEn));
            Assert.False(string.IsNullOrWhiteSpace(permission.LabelAr));
            Assert.False(string.IsNullOrWhiteSpace(permission.DescriptionEn));
            Assert.False(string.IsNullOrWhiteSpace(permission.DescriptionAr));
            Assert.Equal(RoleCategories.Selectable, permission.AllowedCategories);
            Assert.False(PermissionProtectionPolicy.Empty.IsProtected(permission.Key));
            Assert.True(PermissionProtectionPolicy.Empty.IsGrantable(permission));

            if (expectedLevel == PermissionLevel.Action)
            {
                var viewKey = key.StartsWith("ProjectLifecyclePhase.", StringComparison.Ordinal)
                    ? "ProjectLifecyclePhase.View"
                    : "ProjectLifecycleStatus.View";
                Assert.Contains(viewKey, permission.RequiredPermissionKeys);
            }
        }

        Assert.DoesNotContain("ProjectLifecyclePhase.Delete", PermissionCatalog.ByKey.Keys);
        Assert.DoesNotContain("ProjectLifecycleStatus.Delete", PermissionCatalog.ByKey.Keys);
    }

    [Fact]
    public void Project_status_change_is_project_scoped_and_phase_change_retains_its_governed_permission()
    {
        var statusChange = PermissionCatalog.ByKey["ProjectStatus.Change"];
        var phaseChange = PermissionCatalog.ByKey["ProjectPhase.Change"];

        Assert.True(statusChange.IsActive);
        Assert.True(statusChange.IsAssignable);
        Assert.True(statusChange.IsSensitive);
        Assert.Equal(PermissionLevel.Action, statusChange.Level);
        Assert.Equal(PermissionScope.Project, statusChange.Scope);
        Assert.Contains("Project.View", statusChange.RequiredPermissionKeys);
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(statusChange.Key));

        Assert.True(phaseChange.IsActive);
        Assert.True(phaseChange.IsSensitive);
        Assert.Equal(PermissionScope.Project, phaseChange.Scope);
        Assert.Contains("Project.View", phaseChange.RequiredPermissionKeys);
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(phaseChange.Key));
    }

    [Fact]
    public void Lifecycle_policies_match_catalog_keys_and_minimal_lookup_scope()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "apps/api/src/NEXUS.Api/Program.cs"));
        var frontendPermissions = File.ReadAllText(Path.Combine(root, "apps/web-react/src/lib/permissions.ts"));

        foreach (var key in CatalogPermissions.Keys.Append("ProjectStatus.Change"))
        {
            Assert.Contains($"options.AddPolicy(\"{key}\"", program, StringComparison.Ordinal);
            Assert.Contains($"policy.RequireClaim(\"permission\", \"{key}\")", program, StringComparison.Ordinal);
            Assert.Contains($"\"{key}\"", frontendPermissions, StringComparison.Ordinal);
        }

        Assert.Contains("options.AddPolicy(\"ProjectPhase.Change\"", program, StringComparison.Ordinal);
        Assert.Contains("options.AddPolicy(\"ProjectLifecycle.OptionsLookup\"", program, StringComparison.Ordinal);
        Assert.Contains("context.User.HasClaim(\"permission\", \"Project.Create\")", program, StringComparison.Ordinal);
        Assert.Contains("|| context.User.HasClaim(\"permission\", \"Project.Edit\")", program, StringComparison.Ordinal);
        Assert.Contains("|| context.User.HasClaim(\"permission\", \"Project.View\")", program, StringComparison.Ordinal);
        Assert.Contains("|| context.User.HasClaim(\"permission\", \"StatusReport.View\")", program, StringComparison.Ordinal);
        Assert.Contains("|| context.User.HasClaim(\"permission\", \"StatusReport.Create\")", program, StringComparison.Ordinal);
        Assert.Contains("|| context.User.HasClaim(\"permission\", \"StatusReport.Edit\")", program, StringComparison.Ordinal);
        Assert.Contains("|| context.User.HasClaim(\"permission\", \"StatusReport.Publish\")", program, StringComparison.Ordinal);
        Assert.Contains("|| context.User.HasClaim(\"permission\", \"ProjectPhase.Change\")", program, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate the repository root for lifecycle permission contract assertions.");
    }
}
