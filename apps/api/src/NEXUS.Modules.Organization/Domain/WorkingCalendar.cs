using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

public class WorkingCalendar : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int WorkingDaysPerWeek { get; set; } = 5;
    public int HoursPerDay { get; set; } = 8;
    public int PaidBreakMinutesPerDay { get; set; }
    public bool IsDefault { get; set; }
    public ICollection<Holiday> Holidays { get; set; } = new List<Holiday>();

    public decimal ProductiveHoursPerDay => HoursPerDay - (PaidBreakMinutesPerDay / 60m);
}
