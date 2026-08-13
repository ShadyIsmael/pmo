using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

public class BusinessUnit : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public Guid? HeadOfUnitId { get; set; }
    public decimal? Budget { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Department> Departments { get; set; } = new List<Department>();
}
