using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Organization.Api;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Application.Settings;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class TenantRuntimeSettingsAuthorizationTests
{
    [Fact]
    public async Task Authenticated_runtime_bootstrap_is_tenant_scoped_and_returns_only_regional_defaults()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var reads = new List<SettingsScopeIdentity>();
        var repository = new Mock<ISettingsRepository>();
        repository.SetupGet(item => item.IsAvailable).Returns(true);
        repository.Setup(item => item.ReadAsync(It.IsAny<SettingsScopeIdentity>(), It.IsAny<CancellationToken>()))
            .Callback<SettingsScopeIdentity, CancellationToken>((identity, _) => reads.Add(identity))
            .Returns((SettingsScopeIdentity identity, CancellationToken _) => Task.FromResult<IReadOnlyDictionary<string, StoredConfigurationValue>>(
                identity.Scope == SettingScope.Tenant && identity.TenantId == tenantA
                    ? Values(("regional.defaultLanguage", "ar"), ("finance.fiscalYearStartMonth", 7))
                    : identity.Scope == SettingScope.Tenant && identity.TenantId == tenantB
                        ? Values(("regional.defaultLanguage", "fr"))
                        : new Dictionary<string, StoredConfigurationValue>(StringComparer.Ordinal)));

        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("tenantId", tenantA.ToString())], "authenticated-test"))
            }
        };
        var service = new ConfigurationSettingsService(
            new SettingDefinitionCatalog(),
            repository.Object,
            new EffectiveConfigurationResolver(),
            http,
            Mock.Of<IAuthorizationService>(),
            Mock.Of<IAuditService>(),
            Mock.Of<ICurrencyService>(),
            Mock.Of<IOrganizationSettingsAccessService>());

        var result = await service.GetTenantRuntimeAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(SettingScope.Tenant, result.Value!.Scope);
        Assert.Equal<Guid?>(tenantA, result.Value.OwnerId);
        Assert.Equal(new[]
        {
            "regional.defaultCurrency",
            "regional.defaultDateFormat",
            "regional.defaultLanguage",
            "regional.defaultTimeFormat",
            "regional.defaultTimeZone",
            "regional.firstDayOfWeek"
        }, result.Value.Settings.Select(setting => setting.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal("ar", result.Value.Settings.Single(setting => setting.Key == "regional.defaultLanguage").Value.GetString());
        Assert.DoesNotContain(result.Value.Settings, setting => setting.Key == "finance.fiscalYearStartMonth");
        Assert.Equal(new[]
        {
            SettingsScopeIdentity.Platform(),
            SettingsScopeIdentity.Tenant(tenantA)
        }, reads);
        Assert.DoesNotContain(reads, identity => identity.TenantId == tenantB);
    }

    [Fact]
    public void Runtime_bootstrap_route_requires_authentication_at_controller_level()
    {
        var authorize = typeof(SettingsController).GetCustomAttribute<AuthorizeAttribute>();
        var action = typeof(SettingsController).GetMethod(nameof(SettingsController.GetRuntime));

        Assert.NotNull(authorize);
        Assert.Null(authorize!.Policy);
        Assert.NotNull(action);
        Assert.Null(action!.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    private static IReadOnlyDictionary<string, StoredConfigurationValue> Values(params (string Key, object Value)[] values) =>
        values.ToDictionary(
            item => item.Key,
            item => new StoredConfigurationValue(Guid.NewGuid(), item.Key, JsonSerializer.SerializeToElement(item.Value), "test-version"),
            StringComparer.Ordinal);
}
