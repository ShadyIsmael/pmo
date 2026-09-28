using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

public class Lookup : BaseEntity
{
    public string Category { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Color { get; set; }
    public bool IsDefault { get; set; }
}
