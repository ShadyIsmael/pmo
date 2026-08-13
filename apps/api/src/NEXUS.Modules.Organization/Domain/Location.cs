using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

public class Location : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Postcode { get; set; }
    public string? Timezone { get; set; }
    public bool IsActive { get; set; } = true;
}
