using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.Identity.Application;

/// <summary>
/// Stable identity for the sole account created by the Development identity seed.
/// </summary>
public static class SeededDevelopmentAdmin
{
    public const string Email = "admin@nexus.com";
    public static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid TenantId = TenantConstants.DemoTenantId;
}
