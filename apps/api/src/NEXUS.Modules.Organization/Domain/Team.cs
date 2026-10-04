using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

/// <summary>
/// A Team is an independent organizational master used to group Resources and to own
/// work. It is deliberately NOT a Resource type: <c>Resource.Type</c> only ever holds
/// <c>Staff</c> or <c>VendorResource</c>.
///
/// Membership is modelled by <see cref="TeamMember"/> rather than by a single
/// <c>Resource.TeamId</c> pointer, so a Resource can belong to several Teams later
/// without another migration.
///
/// Nested Teams are not supported in this phase: there is deliberately no
/// <c>ParentTeamId</c>.
///
/// The nullable external-directory fields exist so that future Active Directory
/// group mapping can be added without reshaping the entity. No AD integration is
/// performed today.
/// </summary>
public class Team : BaseEntity
{
    /// <summary>Optional owning Department. A Team is independent and may exist without one.</summary>
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Auto-generated, tenant-scoped, for example <c>TEAM-000001</c>. Read-only after creation.</summary>
    public string? Code { get; set; }

    public string? Description { get; set; }

    /// <summary>Optional Resource that leads this Team.</summary>
    public Guid? TeamLeadId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Future Active Directory group mapping. Not populated today.</summary>
    public string? ExternalDirectoryId { get; set; }

    /// <summary>Future group mail / distribution list alias. Not populated today.</summary>
    public string? EmailAlias { get; set; }

    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}
