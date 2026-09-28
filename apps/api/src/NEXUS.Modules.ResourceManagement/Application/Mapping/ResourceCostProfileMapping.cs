using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.ResourceManagement.Application.Mapping;

public static class ResourceCostProfileMapping
{
    public static ResourceCostProfileDto ToDto(this ResourceCostProfile profile)
    {
        return new ResourceCostProfileDto
        {
            Id = profile.Id,
            ResourceId = profile.ResourceId,
            EffectiveFrom = profile.EffectiveFrom,
            EffectiveTo = profile.EffectiveTo,
            CompensationType = profile.CompensationType,
            Amount = profile.Amount,
            Currency = profile.Currency,
            OvertimeEligible = profile.OvertimeEligible,
            OvertimeRateType = profile.OvertimeRateType,
            OvertimeRateValue = profile.OvertimeRateValue,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            RowVersion = profile.RowVersion
        };
    }

    public static ResourceCostProfile ToEntity(this CreateResourceCostProfileDto dto)
    {
        return new ResourceCostProfile
        {
            ResourceId = dto.ResourceId,
            EffectiveFrom = dto.EffectiveFrom,
            CompensationType = dto.CompensationType,
            Amount = dto.Amount,
            Currency = dto.Currency,
            OvertimeEligible = dto.OvertimeEligible,
            OvertimeRateType = dto.OvertimeRateType,
            OvertimeRateValue = dto.OvertimeRateValue
        };
    }

    public static void ApplyUpdate(this ResourceCostProfile profile, UpdateResourceCostProfileDto dto)
    {
        if (dto.EffectiveFrom.HasValue) profile.EffectiveFrom = dto.EffectiveFrom.Value;
        if (dto.EffectiveTo.HasValue) profile.EffectiveTo = dto.EffectiveTo;
        if (dto.CompensationType != null) profile.CompensationType = dto.CompensationType;
        if (dto.Amount.HasValue) profile.Amount = dto.Amount.Value;
        if (dto.Currency != null) profile.Currency = dto.Currency;
        if (dto.OvertimeEligible.HasValue) profile.OvertimeEligible = dto.OvertimeEligible.Value;
        if (dto.OvertimeRateType != null) profile.OvertimeRateType = dto.OvertimeRateType;
        if (dto.OvertimeRateValue.HasValue) profile.OvertimeRateValue = dto.OvertimeRateValue;
    }
}
