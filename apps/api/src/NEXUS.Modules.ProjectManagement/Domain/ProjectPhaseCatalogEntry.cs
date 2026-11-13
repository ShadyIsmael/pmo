using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

/// <summary>
/// Tenant or Organization scoped reference data for a project lifecycle phase.
/// The Code is stable and is the compatibility key for existing Phase Gate
/// configurations; localized names are presentation data only.
/// </summary>
public sealed class ProjectPhaseCatalogEntry : BaseEntity
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
    /// <summary>Marks phases such as Monitoring & Controlling that remain active alongside delivery work.</summary>
    public bool IsContinuousActivity { get; set; }
    /// <summary>Optional link to a Document Category whose templates are relevant for this phase.</summary>
    public Guid? DocumentCategoryId { get; set; }
}
