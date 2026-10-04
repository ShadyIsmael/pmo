using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;

namespace NEXUS.Modules.ResourceManagement.Application.Mapping;

public static class TeamMapping
{
    public static TeamDto ToDto(this Team team) => new()
    {
        Id = team.Id,
        Name = team.Name,
        Code = team.Code,
        Description = team.Description,
        DepartmentId = team.DepartmentId,
        DepartmentName = team.Department?.Name,
        TeamLeadId = team.TeamLeadId,
        IsActive = team.IsActive,
        EmailAlias = team.EmailAlias,
        ExternalDirectoryId = team.ExternalDirectoryId
    };

    public static Team ToEntity(this CreateTeamDto dto) => new()
    {
        Name = dto.Name.Trim(),
        Description = dto.Description?.Trim(),
        DepartmentId = NullIfEmpty(dto.DepartmentId),
        TeamLeadId = NullIfEmpty(dto.TeamLeadId),
        EmailAlias = dto.EmailAlias?.Trim(),
        IsActive = true
    };

    public static void ApplyUpdate(this Team team, UpdateTeamDto dto)
    {
        if (dto.Name is not null) team.Name = dto.Name.Trim();
        if (dto.Description is not null) team.Description = dto.Description.Trim();
        // An explicit Guid.Empty clears the optional reference; a null leaves it alone.
        if (dto.DepartmentId.HasValue) team.DepartmentId = NullIfEmpty(dto.DepartmentId);
        if (dto.TeamLeadId.HasValue) team.TeamLeadId = NullIfEmpty(dto.TeamLeadId);
        if (dto.EmailAlias is not null) team.EmailAlias = dto.EmailAlias.Trim();
        if (dto.IsActive.HasValue) team.IsActive = dto.IsActive.Value;
    }

    public static TeamMemberDto ToDto(this TeamMember member) => new()
    {
        Id = member.Id,
        TeamId = member.TeamId,
        ResourceId = member.ResourceId,
        EffectiveFrom = member.EffectiveFrom,
        EffectiveTo = member.EffectiveTo,
        IsActive = member.IsActive
    };

    private static Guid? NullIfEmpty(Guid? value) => value is null || value == Guid.Empty ? null : value;
}
