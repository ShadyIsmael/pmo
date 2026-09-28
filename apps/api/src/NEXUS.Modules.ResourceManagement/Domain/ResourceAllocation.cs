using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Domain;

public class ResourceAllocation : BaseEntity
{
    public const string StatusConfirmed = "Confirmed";
    public const string StatusProposed = "Proposed";
    public const string StatusTentative = "Tentative";
    public const string StatusCancelled = "Cancelled";

    public const string SourceManual = "Manual";
    public const string SourceProjectPlan = "ProjectPlan";

    public Guid ResourceId { get; set; }
    public Resource Resource { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? ProjectRoleId { get; set; }
    public decimal AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public decimal? PlannedHours { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = StatusConfirmed;
    public bool IsBillable { get; set; } = true;
    public string? Notes { get; set; }
    public string AllocationSource { get; set; } = SourceManual;
    public Guid? SourceReferenceId { get; set; }
}
