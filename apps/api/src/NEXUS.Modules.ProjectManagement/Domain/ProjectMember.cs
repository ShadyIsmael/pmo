using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class ProjectMember : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid ResourceId { get; set; }
    public string Role { get; set; } = string.Empty;
    public decimal AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Active";
    public bool IsBillable { get; set; } = true;
    public string? Notes { get; set; }
}
