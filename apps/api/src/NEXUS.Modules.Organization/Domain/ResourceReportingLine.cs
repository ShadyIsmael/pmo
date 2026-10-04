using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

/// <summary>Effective-dated reporting relationship between two Resources.</summary>
public class ResourceReportingLine : BaseEntity
{
    public Guid ResourceId { get; set; }
    public Guid ManagerResourceId { get; set; }
    public string ReportingType { get; set; } = "LineManager";
    public bool IsPrimary { get; set; } = true;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}
