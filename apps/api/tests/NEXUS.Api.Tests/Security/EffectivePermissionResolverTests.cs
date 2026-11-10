using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class EffectivePermissionResolverTests
{
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

    [Theory]
    [InlineData("ProjectAnnualBudget.ConfigureDraftApproval")]
    [InlineData("ProjectAnnualBudget.Delete")]
    [InlineData("ProjectAnnualBudget.Withdraw")]
    public void Direct_deny_wins_over_all_role_allows_and_a_direct_allow_for_annual_budget_lifecycle(string permission)
    {
        var effective = EffectivePermissionResolver.Resolve(
            [permission, permission],
            [(permission, PermissionOverrideEffect.Allow),
                (permission, PermissionOverrideEffect.Deny)]);

        Assert.DoesNotContain(permission, effective);
    }

    [Fact]
    public void Protected_admin_baseline_survives_deny_while_deny_wins_for_ordinary_permissions()
    {
        var effective = EffectivePermissionResolver.Resolve(
            ["Project.Edit"],
            [("Project.Edit", PermissionOverrideEffect.Deny), ("Plugin.View", PermissionOverrideEffect.Deny)],
            ["Plugin.View", "Roles.View"]);

        Assert.DoesNotContain("Project.Edit", effective);
        Assert.Contains("Plugin.View", effective);
        Assert.Contains("Roles.View", effective);
    }
}
