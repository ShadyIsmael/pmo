using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Domain;

public class ResourceSkill : BaseEntity
{
    public Guid ResourceId { get; set; }
    public Resource Resource { get; set; } = null!;
    public Guid SkillId { get; set; }
    public Skill Skill { get; set; } = null!;
    public string Proficiency { get; set; } = string.Empty;
    public int YearsOfExperience { get; set; }
}
