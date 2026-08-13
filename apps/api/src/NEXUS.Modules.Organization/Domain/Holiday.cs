using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

public class Holiday : BaseEntity
{
    public Guid WorkingCalendarId { get; set; }
    public WorkingCalendar WorkingCalendar { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public bool IsRecurring { get; set; }
}
