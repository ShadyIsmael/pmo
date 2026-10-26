namespace NEXUS.Modules.ResourceManagement.Application.DTOs;

public class ResourceCostProfileDto
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string CompensationType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool OvertimeEligible { get; set; }
    public string? OvertimeRateType { get; set; }
    public decimal? OvertimeRateValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class CreateResourceCostProfileDto
{
    public Guid ResourceId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string CompensationType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool OvertimeEligible { get; set; }
    public string? OvertimeRateType { get; set; }
    public decimal? OvertimeRateValue { get; set; }
}

public class UpdateResourceCostProfileDto
{
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? CompensationType { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public bool? OvertimeEligible { get; set; }
    public string? OvertimeRateType { get; set; }
    public decimal? OvertimeRateValue { get; set; }
    public byte[]? RowVersion { get; set; }
}
