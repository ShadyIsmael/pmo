using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class ProjectSchedule : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? BaselineStartDate { get; set; }
    public DateTime? BaselineEndDate { get; set; }
    public DateTime? ForecastStartDate { get; set; }
    public DateTime? ForecastEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public int? TotalDays { get; set; }
    public int? RemainingDays { get; set; }
}
