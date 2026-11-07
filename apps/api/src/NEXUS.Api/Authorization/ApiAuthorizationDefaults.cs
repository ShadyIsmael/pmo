using Microsoft.AspNetCore.Authorization;

namespace NEXUS.Api.Authorization;

/// <summary>
/// Shared authorization defaults for HTTP endpoints.
/// New endpoints require an authenticated principal unless they opt into an
/// explicit <see cref="AllowAnonymousAttribute"/> exception.
/// </summary>
public static class ApiAuthorizationDefaults
{
    public static AuthorizationPolicy FallbackPolicy { get; } = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
}
