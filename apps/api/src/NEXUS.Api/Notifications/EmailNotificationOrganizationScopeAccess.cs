using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Organization.Application;

namespace NEXUS.Api.Notifications;

/// <summary>Reuses explicit per-user Organization Settings grants for SMTP profile overrides.</summary>
public sealed class EmailNotificationOrganizationScopeAccess(IOrganizationSettingsAccessService organizationAccess)
    : IEmailNotificationOrganizationScopeAccess
{
    public Task<bool> HasAccessAsync(Guid organizationId, bool manage, CancellationToken cancellationToken = default) =>
        organizationAccess.HasAccessAsync(organizationId, manage, cancellationToken);

    public Task<bool> HasScopedOrganizationAdminAuthorityAsync(Guid organizationId, bool manage, CancellationToken cancellationToken = default) =>
        organizationAccess.HasScopedOrganizationAdminAuthorityAsync(organizationId, manage, cancellationToken);
}
