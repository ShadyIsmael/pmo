using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace NEXUS.Api.Security;

public static class BrowserCsrfProtectionRegistration
{
    public const string RequestHeaderName = "X-Nexus-CSRF";

    public static IServiceCollection AddBrowserCsrfProtection(this IServiceCollection services)
    {
        services.AddAntiforgery(options =>
        {
            options.HeaderName = RequestHeaderName;
            options.Cookie.Name = "__Host-nexus-csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
            options.Cookie.IsEssential = true;
        });

        return services;
    }
}
