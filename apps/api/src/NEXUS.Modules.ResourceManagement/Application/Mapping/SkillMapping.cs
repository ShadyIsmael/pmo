using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.ResourceManagement.Application.Mapping;

public static class SkillMapping
{
    public static SkillDto ToDto(this Skill skill)
    {
        return new SkillDto
        {
            Id = skill.Id,
            Name = skill.Name,
            Category = skill.Category,
            Description = skill.Description,
            IsActive = skill.IsActive
        };
    }

    public static Skill ToEntity(this CreateSkillDto dto)
    {
        return new Skill
        {
            Name = dto.Name,
            Category = dto.Category,
            Description = dto.Description,
            IsActive = true
        };
    }

    public static void ApplyUpdate(this Skill skill, UpdateSkillDto dto)
    {
        if (dto.Name != null) skill.Name = dto.Name;
        if (dto.Category != null) skill.Category = dto.Category;
        if (dto.Description != null) skill.Description = dto.Description;
        if (dto.IsActive.HasValue) skill.IsActive = dto.IsActive.Value;
    }
}
