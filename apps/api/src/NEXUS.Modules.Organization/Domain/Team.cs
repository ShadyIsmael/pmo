using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

public class Team : BaseEntity
{
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public Guid? TeamLeadId { get; set; }
    public bool IsActive { get; set; } = true;
}
