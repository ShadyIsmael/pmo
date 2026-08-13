using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Domain;

public class Resource : BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? EmployeeId { get; set; }
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? CostPerHour { get; set; }
    public decimal? BillableRate { get; set; }
    public string? Currency { get; set; } = "GBP";
    public string? EmploymentType { get; set; }
    public string? Seniority { get; set; }
    public DateTime? DateOfJoining { get; set; }
    public Guid? BusinessUnitId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? VendorId { get; set; }
    public Vendor? Vendor { get; set; }
    public string? Timezone { get; set; }
    public decimal? UtilizationTarget { get; set; }
    public decimal? CurrentUtilization { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ResourceSkill> Skills { get; set; } = new List<ResourceSkill>();
    public ICollection<ResourceAllocation> Allocations { get; set; } = new List<ResourceAllocation>();
}
