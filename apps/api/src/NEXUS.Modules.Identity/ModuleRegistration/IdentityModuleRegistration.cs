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
        services.AddScoped<IAuthenticationService, DemoAuthService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserReferenceLookup, UserReferenceLookup>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        return services;
    }
}
