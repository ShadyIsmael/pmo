using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

/// <summary>Explicit per-user authorization for one organization settings workspace.</summary>
public sealed class OrganizationSettingsGrant : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public bool CanRead { get; set; }
    public bool CanManage { get; set; }
}
