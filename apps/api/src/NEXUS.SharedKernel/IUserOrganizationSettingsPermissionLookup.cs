namespace NEXUS.SharedKernel;

public sealed record UserOrganizationSettingsPermissions(bool CanRead, bool CanManage);

/// <summary>Reads the explicit per-user Organization Admin grant used by effective authorization.</summary>
public interface IUserOrganizationSettingsPermissionLookup
{
    Task<UserOrganizationSettingsPermissions?> GetAsync(
        Guid tenantId,
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken = default);
}
