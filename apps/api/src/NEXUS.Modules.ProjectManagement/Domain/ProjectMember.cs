using NEXUS.SharedKernel;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class ProjectMember : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    /// <summary>Exactly one of ResourceId or TeamId is set. Resource rows represent direct membership.</summary>
    public Guid? ResourceId { get; set; }
    /// <summary>When set, this membership dynamically includes the Team's effective Resources.</summary>
    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }
    public string Role { get; set; } = string.Empty;
    public decimal AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Active";
    public bool IsBillable { get; set; } = true;
    public string? Notes { get; set; }
    public string CalendarPolicy { get; set; } = CalendarPolicyConstants.UseResourceCalendar;
    public ICollection<ProjectMemberRole> FunctionalRoles { get; set; } = new List<ProjectMemberRole>();
}
