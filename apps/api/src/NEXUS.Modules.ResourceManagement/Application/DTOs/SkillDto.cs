namespace NEXUS.Modules.ResourceManagement.Application.DTOs;

public class SkillDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSkillDto
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
}

public class UpdateSkillDto
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
}
