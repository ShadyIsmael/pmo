using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Domain;

public class ResourceCalendar : BaseEntity
{
    public Guid ResourceId { get; set; }
    public Resource Resource { get; set; } = null!;
    public DateTime Date { get; set; }
    public string DayType { get; set; } = string.Empty;
    public decimal? AvailableHours { get; set; }
    public string? Notes { get; set; }
}
