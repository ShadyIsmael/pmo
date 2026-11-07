using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NEXUS.Api.Authorization;
using NEXUS.Api.ProgramManagement;
using NEXUS.Api.PortfolioManagement;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;
using Xunit;
using AspNetAuthorizationService = Microsoft.AspNetCore.Authorization.IAuthorizationService;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Api.Tests.Security;

public sealed class ProgramPortfolioFinancialRollupAuthorizationTests
{
    [Fact]
    public void Financial_rollup_routes_require_the_matching_parent_view_and_rollup_view()
    {
        var programMethod = typeof(ProgramManagementController).GetMethod(nameof(ProgramManagementController.GetFinancialRollup));
        var portfolioMethod = typeof(PortfolioManagementController).GetMethod(nameof(PortfolioManagementController.GetFinancialRollup));

        Assert.NotNull(programMethod);
        Assert.NotNull(portfolioMethod);
        Assert.Equal("Program.View", typeof(ProgramManagementController).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("ProgramFinancialRollup.View", programMethod!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("programs/{programId:guid}/financial-rollup",
            Assert.Single(programMethod.GetCustomAttributes<HttpGetAttribute>()).Template);

        var portfolioPolicies = portfolioMethod!.GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("Portfolio.View", portfolioPolicies);
        Assert.Contains("PortfolioFinancialRollup.View", portfolioPolicies);
        Assert.Equal(2, portfolioPolicies.Count);
        Assert.Equal("portfolios/{portfolioId:guid}/financial-rollup",
            Assert.Single(portfolioMethod.GetCustomAttributes<HttpGetAttribute>()).Template);
    }

    [Fact]
    public void Rollup_catalog_entries_are_separate_assignable_scopes_and_not_admin_baseline()
    {
        Assert.False(PermissionCatalog.ByKey.ContainsKey("ProjectFinancialRollup.View"));
        AssertRollupPermission("ProgramFinancialRollup.View", PermissionScope.Program);
        AssertRollupPermission("PortfolioFinancialRollup.View", PermissionScope.Portfolio);
    }

    [Fact]
    public async Task Each_rollup_requires_its_own_key_and_parent_view_and_honors_deny()
    {
        var authorization = CreateAuthorizationService();
        await AssertPairRequired(authorization, "Program.View", "ProgramFinancialRollup.View", "PortfolioFinancialRollup.View");
        await AssertPairRequired(authorization, "Portfolio.View", "PortfolioFinancialRollup.View", "ProgramFinancialRollup.View");

        var deniedProgram = EffectivePermissionResolver.Resolve(
            ["Program.View", "ProgramFinancialRollup.View"],
            [("ProgramFinancialRollup.View", PermissionOverrideEffect.Deny)]);
        Assert.False(await PairSucceeds(authorization, PrincipalWith(deniedProgram.ToArray()), "Program.View", "ProgramFinancialRollup.View"));

        var deniedPortfolio = EffectivePermissionResolver.Resolve(
            ["Portfolio.View", "PortfolioFinancialRollup.View"],
            [("PortfolioFinancialRollup.View", PermissionOverrideEffect.Deny)]);
        Assert.False(await PairSucceeds(authorization, PrincipalWith(deniedPortfolio.ToArray()), "Portfolio.View", "PortfolioFinancialRollup.View"));
    }

    [Fact]
    public async Task Program_rollup_does_not_read_a_program_outside_native_management_scope()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var requestedProgramId = Guid.NewGuid();
        var scope = new ManagementScope(
            new HashSet<Guid>(),
            new HashSet<Guid> { Guid.NewGuid() },
            new HashSet<Guid>());
        var service = CreateService(actorId, tenantId, scope, ["Program.View", "ProgramFinancialRollup.View"]);

        var result = await service.GetProgramAsync(requestedProgramId, null, 1, 25, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Portfolio_rollup_does_not_read_a_portfolio_outside_native_management_scope()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var requestedPortfolioId = Guid.NewGuid();
        var scope = new ManagementScope(
            new HashSet<Guid> { Guid.NewGuid() },
            new HashSet<Guid>(),
            new HashSet<Guid>());
        var service = CreateService(actorId, tenantId, scope, ["Portfolio.View", "PortfolioFinancialRollup.View"]);

        var result = await service.GetPortfolioAsync(requestedPortfolioId, null, 1, 25, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    private static void AssertRollupPermission(string key, PermissionScope scope)
    {
        var permission = PermissionCatalog.ByKey[key];
        Assert.True(permission.IsActive);
        Assert.True(permission.IsAssignable);
        Assert.True(permission.IsSensitive);
        Assert.False(permission.IsTenantAdminBaseline);
        Assert.Equal(PermissionLevel.View, permission.Level);
        Assert.Equal(scope, permission.Scope);
        Assert.Empty(permission.RequiredPermissionKeys);
        Assert.False(string.IsNullOrWhiteSpace(permission.LabelEn));
        Assert.False(string.IsNullOrWhiteSpace(permission.LabelAr));
        Assert.False(string.IsNullOrWhiteSpace(permission.DescriptionEn));
        Assert.False(string.IsNullOrWhiteSpace(permission.DescriptionAr));
    }

    private static async Task AssertPairRequired(
        AspNetAuthorizationService authorization,
        string parent,
        string rollup,
        string siblingRollup)
    {
        Assert.True(await PairSucceeds(authorization, PrincipalWith(parent, rollup), parent, rollup));
        Assert.False(await PairSucceeds(authorization, PrincipalWith(parent), parent, rollup));
        Assert.False(await PairSucceeds(authorization, PrincipalWith(rollup), parent, rollup));
        Assert.False(await PairSucceeds(authorization, PrincipalWith(parent, siblingRollup), parent, rollup));
        Assert.False(await PairSucceeds(authorization, PrincipalWith("ProjectFinance.View"), parent, rollup));
        Assert.False(await PairSucceeds(authorization, PrincipalWith("Unrelated.Permission"), parent, rollup));
    }

    private static async Task<bool> PairSucceeds(
        AspNetAuthorizationService authorization,
        ClaimsPrincipal principal,
        string parent,
        string rollup)
    {
        var parentResult = await authorization.AuthorizeAsync(principal, parent);
        var rollupResult = await authorization.AuthorizeAsync(principal, rollup);
        return parentResult.Succeeded && rollupResult.Succeeded;
    }

    private static AspNetAuthorizationService CreateAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Program.View", policy => policy.RequireClaim("permission", "Program.View"));
            options.AddPolicy("Portfolio.View", policy => policy.RequireClaim("permission", "Portfolio.View"));
            options.AddCanonicalFinancialActionPolicies();
        });
        return services.BuildServiceProvider().GetRequiredService<AspNetAuthorizationService>();
    }

    private static ProgramPortfolioFinancialRollupService CreateService(
        Guid actorId,
        Guid tenantId,
        ManagementScope scope,
        IEnumerable<string> effectivePermissions)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var management = new Mock<IManagementContextAccessor>();
        management.SetupGet(accessor => accessor.Current).Returns(new ManagementContext(
            actorId,
            tenantId,
            ManagementPerspective.ProgramManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            scope));
        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(effectivePermissions.ToHashSet(StringComparer.Ordinal));

        return new ProgramPortfolioFinancialRollupService(
            db,
            management.Object,
            permissions.Object,
            Mock.Of<ITenantFiscalYearService>(),
            new FiscalYearCalendarService());
    }

    private static ClaimsPrincipal PrincipalWith(params string[] permissions) =>
        new(new ClaimsIdentity(permissions.Select(permission => new Claim("permission", permission)), "test"));
}
