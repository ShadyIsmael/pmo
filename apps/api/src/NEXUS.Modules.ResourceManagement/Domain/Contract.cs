using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Domain;

public class Contract : BaseEntity
{
    public Guid ResourceId { get; set; }
    public Resource Resource { get; set; } = null!;
    public string Type { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Value { get; set; }
    public string Currency { get; set; } = "GBP";
    public string? Terms { get; set; }
    public string? VendorName { get; set; }
    public string Status { get; set; } = string.Empty;
}
