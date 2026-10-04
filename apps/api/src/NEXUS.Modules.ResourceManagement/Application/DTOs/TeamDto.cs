namespace NEXUS.Modules.ResourceManagement.Application.DTOs;

public class TeamDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public Guid? TeamLeadId { get; set; }
    public string? TeamLeadName { get; set; }
    public bool IsActive { get; set; }
    public string? EmailAlias { get; set; }
    public string? ExternalDirectoryId { get; set; }
    public int MemberCount { get; set; }
    public IReadOnlyList<TeamMemberDto> Members { get; set; } = Array.Empty<TeamMemberDto>();
}

public class CreateTeamDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? TeamLeadId { get; set; }
    public string? EmailAlias { get; set; }

    /// <summary>
    /// Optional initial membership. Resource ids are validated to exist and to
    /// belong to the same tenant before the Team is created.
    /// </summary>
    public IReadOnlyList<Guid> MemberResourceIds { get; set; } = Array.Empty<Guid>();
}

public class UpdateTeamDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? TeamLeadId { get; set; }
    public string? EmailAlias { get; set; }
    public bool? IsActive { get; set; }
}

public class TeamMemberDto
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public string? ResourceType { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public class AddTeamMembersDto
{
    public IReadOnlyList<Guid> ResourceIds { get; set; } = Array.Empty<Guid>();
    public DateTime? EffectiveFrom { get; set; }
}

/// <summary>
/// Replaces the current membership of a Team. Resources that are not listed have
/// their current membership closed (EffectiveTo set) rather than removed, so
/// historical ownership stays attributable.
/// </summary>
public class SetTeamMembersDto
{
    public IReadOnlyList<Guid> ResourceIds { get; set; } = Array.Empty<Guid>();
    public DateTime? EffectiveFrom { get; set; }
}

public class RemoveTeamMemberDto
{
    public DateTime? EffectiveTo { get; set; }
}
