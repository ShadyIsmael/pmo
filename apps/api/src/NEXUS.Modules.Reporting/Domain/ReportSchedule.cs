using NEXUS.SharedKernel;

namespace NEXUS.Modules.Reporting.Domain;

public class ReportSchedule : BaseEntity
{
    public Guid ReportDefinitionId { get; set; }
    public ReportDefinition ReportDefinition { get; set; } = null!;
    public string Frequency { get; set; } = string.Empty;
    public string? CronExpression { get; set; }
    public string? Recipients { get; set; }
    public string? OutputFormat { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
}
