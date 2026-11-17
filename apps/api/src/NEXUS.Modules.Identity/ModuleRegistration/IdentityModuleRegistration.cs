using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.ModuleRegistration;

public static class IdentityModuleRegistration
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-Nexus-CSRF";
            options.Cookie.Name = "__Host-nexus-csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
            options.Cookie.Path = "/";
            options.Cookie.IsEssential = true;
        });
        services.AddOptions<LocalPasswordPolicyOptions>()
            .BindConfiguration(LocalPasswordPolicyOptions.SectionName)
            .Validate(options => options.TemporaryPasswordExpiryHours is >= 1 and <= 168
                && options.MinimumLength is >= 8 and <= 128
                && options.MaximumLength >= options.MinimumLength && options.MaximumLength <= 256,
                "Local password policy values are outside the supported range.")
            .ValidateOnStart();
        services.AddOptions<PermissionProtectionOptions>()
            .BindConfiguration(PermissionProtectionOptions.SectionName)
            .Validate(PermissionProtectionPolicy.IsValidConfiguration,
                "Protected permission keys must be unique active canonical catalog keys.")
            .ValidateOnStart();
        services.AddSingleton<IPermissionProtectionPolicy, PermissionProtectionPolicy>();
        services.AddScoped<IAuthenticationService, DemoAuthService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserReferenceLookup, UserReferenceLookup>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<IRoleAdministrationService, RoleAdministrationService>();
        services.AddScoped<IRoleAssignmentAuthorizationService, RoleAssignmentAuthorizationService>();
        services.AddScoped<IUserEffectivePermissionService, UserEffectivePermissionService>();
        services.AddScoped<IApprovalDelegationEligibilityLookup, ApprovalDelegationEligibilityLookup>();
        services.AddScoped<IUserSessionValidator, UserSessionValidator>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        return services;
    }
}
