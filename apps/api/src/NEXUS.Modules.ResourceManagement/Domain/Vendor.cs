using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Domain;

public class Vendor : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Category { get; set; }
    public Guid? VendorCategoryId { get; set; }
    public string? Status { get; set; }
    public decimal? ContractValue { get; set; }
    public DateTime? ContractStart { get; set; }
    public DateTime? ContractEnd { get; set; }
    public bool IsActive { get; set; } = true;
}
