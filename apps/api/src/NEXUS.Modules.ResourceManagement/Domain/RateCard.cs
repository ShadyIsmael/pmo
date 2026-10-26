using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Domain;

public class RateCard : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Seniority { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal? DailyRate { get; set; }
    public decimal? MonthlyRate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}
