using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

/// <summary>
/// Canonical tenant-scoped organizational hierarchy. A unit can represent any
/// configured business level (for example Division, Department or Function).
/// Operational Teams are intentionally not children of this hierarchy.
/// </summary>
public class OrganizationalUnit : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TypeCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentOrganizationalUnitId { get; set; }
    public OrganizationalUnit? ParentOrganizationalUnit { get; set; }
    public ICollection<OrganizationalUnit> Children { get; set; } = new List<OrganizationalUnit>();
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
