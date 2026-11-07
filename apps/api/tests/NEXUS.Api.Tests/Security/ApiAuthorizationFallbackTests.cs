using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using NEXUS.Api.Authorization;
using NEXUS.Modules.Identity.Api;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ApiAuthorizationFallbackTests
{
    [Fact]
    public async Task Fallback_policy_rejects_anonymous_principals_and_accepts_authenticated_principals()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.FallbackPolicy = ApiAuthorizationDefaults.FallbackPolicy);
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var anonymousResult = await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource: null,
            policy: ApiAuthorizationDefaults.FallbackPolicy);
        var authenticatedResult = await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user")], "test")),
            resource: null,
            policy: ApiAuthorizationDefaults.FallbackPolicy);

        Assert.False(anonymousResult.Succeeded);
        Assert.True(authenticatedResult.Succeeded);
    }

    [Fact]
    public void Auth_controller_actions_have_explicit_authorization_or_anonymous_metadata()
    {
        var actionMethods = typeof(AuthController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().Any())
            .ToArray();

        Assert.NotEmpty(actionMethods);
        foreach (var actionMethod in actionMethods)
        {
            var hasAuthorize = actionMethod.GetCustomAttribute<AuthorizeAttribute>(inherit: true) is not null;
            var allowsAnonymous = actionMethod.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;
            Assert.True(hasAuthorize || allowsAnonymous, $"{actionMethod.Name} must declare authorization or an explicit anonymous exception.");
        }

        Assert.NotNull(typeof(AuthController).GetMethod(nameof(AuthController.Login))!.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(typeof(AuthController).GetMethod(nameof(AuthController.Refresh))!.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(typeof(AuthController).GetMethod(nameof(AuthController.Logout))!.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void Discovered_api_controller_actions_declare_authorization_or_an_explicit_anonymous_exception()
    {
        var apiAssembly = typeof(ApiAuthorizationDefaults).Assembly;
        var apiAssemblies = apiAssembly.GetReferencedAssemblies()
            .Where(reference => reference.Name?.StartsWith("NEXUS.Modules.", StringComparison.Ordinal) == true)
            .Select(Assembly.Load)
            .Append(apiAssembly)
            .Distinct()
            .ToArray();
        var controllers = apiAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .ToArray();
        var unannotatedActions = new List<string>();
        var actionCount = 0;

        foreach (var controller in controllers)
        {
            var controllerHasAuthorize = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any()
                    || method.GetCustomAttributes<RouteAttribute>(inherit: true).Any());

            foreach (var action in actions)
            {
                actionCount++;
                var actionHasAuthorize = action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
                var actionAllowsAnonymous = action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();
                if (!controllerHasAuthorize && !actionHasAuthorize && !actionAllowsAnonymous)
                    unannotatedActions.Add($"{controller.FullName}.{action.Name}");
            }
        }

        Assert.NotEmpty(controllers);
        Assert.True(actionCount > 0, "Expected to discover API controller actions.");
        Assert.Empty(unannotatedActions);
    }
}
