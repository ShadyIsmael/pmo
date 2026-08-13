using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? Website { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Postcode { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<BusinessUnit> BusinessUnits { get; set; } = new List<BusinessUnit>();
}
