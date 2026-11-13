using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

/// <summary>Tenant or Organization scoped, independently configurable project statuses.</summary>
public sealed class ProjectStatusCatalogEntry : BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NormalizedCode { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public string ReportingCategory { get; set; } = ProjectStatusReportingCategories.InProgress;
    public string AttentionLevel { get; set; } = ProjectStatusAttentionLevels.None;
}
