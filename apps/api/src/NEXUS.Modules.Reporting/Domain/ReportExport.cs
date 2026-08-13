using NEXUS.SharedKernel;

namespace NEXUS.Modules.Reporting.Domain;

public class ReportExport : BaseEntity
{
    public Guid ReportDefinitionId { get; set; }
    public ReportDefinition ReportDefinition { get; set; } = null!;
    public string Format { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? GeneratedAt { get; set; }
    public Guid? GeneratedBy { get; set; }
    public string? Parameters { get; set; }
}
