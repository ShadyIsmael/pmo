using Microsoft.Extensions.Hosting;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Infrastructure;

internal static class DevelopmentAdminTemporaryPasswordBypass
{
    public static bool IsEnabled(
        Guid tenantId,
        string email,
        AuthenticationSource authenticationSource,
        LocalPasswordPolicyOptions options,
        IHostEnvironment environment) =>
        environment.IsDevelopment()
        && options.DevelopmentAdminTemporaryPasswordBypassEnabled
        && tenantId == SeededDevelopmentAdmin.TenantId
        && string.Equals(email, SeededDevelopmentAdmin.Email, StringComparison.OrdinalIgnoreCase)
        && authenticationSource == AuthenticationSource.Local;
}
