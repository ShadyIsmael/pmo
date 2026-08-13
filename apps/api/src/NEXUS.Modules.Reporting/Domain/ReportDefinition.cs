using NEXUS.SharedKernel;

namespace NEXUS.Modules.Reporting.Domain;

public class ReportDefinition : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Query { get; set; }
    public string? Parameters { get; set; }
    public string? OutputFormat { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
}
