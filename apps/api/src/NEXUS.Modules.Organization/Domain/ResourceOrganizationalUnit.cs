using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

/// <summary>Effective-dated Resource membership in the canonical organization hierarchy.</summary>
public class ResourceOrganizationalUnit : BaseEntity
{
    public Guid ResourceId { get; set; }
    public Guid OrganizationalUnitId { get; set; }
    public OrganizationalUnit OrganizationalUnit { get; set; } = null!;
    public bool IsPrimary { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}
