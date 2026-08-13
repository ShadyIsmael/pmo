using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Domain;

public class ResourceAllocation : BaseEntity
{
    public const string StatusConfirmed = "Confirmed";
    public const string StatusProposed = "Proposed";
    public const string StatusTentative = "Tentative";
    public const string StatusCancelled = "Cancelled";

    public Guid ResourceId { get; set; }
    public Resource Resource { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? WorkItemId { get; set; }
    public string Role { get; set; } = string.Empty;
    public decimal AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public decimal? PlannedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = StatusConfirmed;
    public bool IsBillable { get; set; } = true;
    public string? Notes { get; set; }
}
