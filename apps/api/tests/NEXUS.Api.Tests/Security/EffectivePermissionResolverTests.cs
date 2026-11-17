using Microsoft.Extensions.Options;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class EffectivePermissionResolverTests
{
    [Fact]
    public void Default_protection_policy_makes_every_active_catalog_entry_grantable()
    {
        var policy = new PermissionProtectionPolicy(Options.Create(new PermissionProtectionOptions()));

        Assert.Empty(policy.ProtectedPermissionKeys);
        Assert.All(PermissionCatalog.Entries.Where(entry => entry.IsActive), entry =>
            Assert.True(policy.IsGrantable(entry), entry.Key));
    }

    [Fact]
    public void Only_explicitly_configured_permission_keys_are_protected()
    {
        var policy = new PermissionProtectionPolicy(Options.Create(new PermissionProtectionOptions
        {
            ProtectedPermissionKeys = ["Document.View"]
        }));

        Assert.True(policy.IsProtected("Document.View"));
        Assert.False(policy.IsGrantable(PermissionCatalog.ByKey["Document.View"]));
        Assert.False(policy.IsProtected("Document.Create"));
        Assert.True(policy.IsGrantable(PermissionCatalog.ByKey["Document.Create"]));
        Assert.True(policy.IsGrantable(PermissionCatalog.ByKey["Plugin.View"]));
    }

    [Fact]
    public void Invalid_protection_keys_are_rejected_at_configuration_validation()
    {
        Assert.False(PermissionProtectionPolicy.IsValidConfiguration(new PermissionProtectionOptions
        {
            ProtectedPermissionKeys = ["Document.NotACanonicalPermission"]
        }));
        Assert.False(PermissionProtectionPolicy.IsValidConfiguration(new PermissionProtectionOptions
        {
            ProtectedPermissionKeys = ["Document.View", "Document.View"]
        }));
        Assert.True(PermissionProtectionPolicy.IsValidConfiguration(new PermissionProtectionOptions
        {
            ProtectedPermissionKeys = ["Document.View"]
        }));
    }

    [Fact]
    public void Deny_wins_over_inherited_and_allow_overrides_in_any_order()
    {
        var allowThenDeny = EffectivePermissionResolver.Resolve(
            ["Project.View"],
            [("Project.View", PermissionOverrideEffect.Allow), ("Project.View", PermissionOverrideEffect.Deny)]);
        var denyThenAllow = EffectivePermissionResolver.Resolve(
            ["Project.View"],
            [("Project.View", PermissionOverrideEffect.Deny), ("Project.View", PermissionOverrideEffect.Allow)]);

        Assert.DoesNotContain("Project.View", allowThenDeny);
        Assert.DoesNotContain("Project.View", denyThenAllow);
    }

    [Fact]
    public void Deny_remains_effective_for_admin_roles_and_no_role_identity_adds_grants()
    {
        var inherited = new[] { "Roles.ConfigurePermissions", "Document.View" };
        var effective = EffectivePermissionResolver.Resolve(
            inherited,
            [("Document.View", PermissionOverrideEffect.Deny)]);

        Assert.Contains("Roles.ConfigurePermissions", effective);
        Assert.DoesNotContain("Document.View", effective);
        Assert.DoesNotContain("Document.Create", effective);
    }

}
