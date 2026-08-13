using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

public class Department : BaseEntity
{
    public Guid BusinessUnitId { get; set; }
    public BusinessUnit BusinessUnit { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public Guid? HeadOfDepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Team> Teams { get; set; } = new List<Team>();
}
