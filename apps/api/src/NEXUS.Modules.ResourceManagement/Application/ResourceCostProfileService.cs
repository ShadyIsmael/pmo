using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Application.Mapping;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public class ResourceCostProfileService : IResourceCostProfileService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;

    public ResourceCostProfileService(DbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<Result<IReadOnlyList<ResourceCostProfileDto>>> GetByResourceAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var resourceExists = await _context.Set<Resource>().AnyAsync(r => r.Id == resourceId && !r.IsDeleted, cancellationToken);
        if (!resourceExists) return Result<IReadOnlyList<ResourceCostProfileDto>>.Failure(Error.NotFound("Resource not found"));

        var profiles = await _context.Set<ResourceCostProfile>()
            .Where(cp => cp.ResourceId == resourceId && !cp.IsDeleted)
            .OrderByDescending(cp => cp.EffectiveFrom)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ResourceCostProfileDto>>.Success(profiles.Select(cp => cp.ToDto()).ToList());
    }

    public async Task<Result<ResourceCostProfileDto>> GetByIdAsync(Guid resourceId, Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await _context.Set<ResourceCostProfile>()
            .FirstOrDefaultAsync(cp => cp.Id == id && cp.ResourceId == resourceId && !cp.IsDeleted, cancellationToken);
        if (profile is null)
            return Result<ResourceCostProfileDto>.Failure(Error.NotFound("Resource Cost Profile not found"));

        return Result<ResourceCostProfileDto>.Success(profile.ToDto());
    }

    public async Task<Result<ResourceCostProfileDto>> CreateAsync(CreateResourceCostProfileDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (!CompensationType.IsValid(dto.CompensationType))
            return Result<ResourceCostProfileDto>.Failure(Error.Validation("Invalid compensation type"));

        if (dto.EffectiveFrom == default)
            return Result<ResourceCostProfileDto>.Failure(Error.Validation("Effective start date is required"));

        if (dto.EffectiveTo.HasValue && dto.EffectiveTo.Value.Date < dto.EffectiveFrom.Date)
            return Result<ResourceCostProfileDto>.Failure(Error.Validation("Effective end date must be on or after the start date"));

        var currency = dto.Currency?.Trim();
        if (string.IsNullOrEmpty(currency) || currency.Length != 3 || !currency.All(char.IsAsciiLetter))
            return Result<ResourceCostProfileDto>.Failure(Error.Validation("Select a configured currency.", nameof(dto.Currency)));
        dto.Currency = currency.ToUpperInvariant();

        var currencyExists = await _context.Set<Currency>()
            .AnyAsync(item => item.Code == dto.Currency && !item.IsDeleted, cancellationToken);
        if (!currencyExists)
            return Result<ResourceCostProfileDto>.Failure(Error.Validation("The selected currency is not configured for this tenant.", nameof(dto.Currency)));

        var resource = await _context.Set<Resource>()
            .FirstOrDefaultAsync(r => r.Id == dto.ResourceId && !r.IsDeleted, cancellationToken);
        if (resource is null)
            return Result<ResourceCostProfileDto>.Failure(Error.NotFound("Resource not found"));

        if (dto.Amount <= 0)
            return Result<ResourceCostProfileDto>.Failure(Error.Validation("Amount must be greater than zero"));

        if (dto.OvertimeEligible)
        {
            if (string.IsNullOrEmpty(dto.OvertimeRateType) || !OvertimeRateTypeConstants.IsValid(dto.OvertimeRateType))
                return Result<ResourceCostProfileDto>.Failure(Error.Validation("Valid overtime rate type is required when overtime is eligible"));
            if (!dto.OvertimeRateValue.HasValue || dto.OvertimeRateValue.Value <= 0)
                return Result<ResourceCostProfileDto>.Failure(Error.Validation("Overtime rate value must be greater than zero when overtime is eligible"));
        }
        else
        {
            dto.OvertimeRateType = null;
            dto.OvertimeRateValue = null;
        }

        var requestedEnd = dto.EffectiveTo ?? DateTime.MaxValue;
        var hasOverlap = await _context.Set<ResourceCostProfile>().AnyAsync(cp =>
            cp.ResourceId == dto.ResourceId && !cp.IsDeleted &&
            cp.EffectiveFrom <= requestedEnd &&
            (!cp.EffectiveTo.HasValue || cp.EffectiveTo.Value >= dto.EffectiveFrom), cancellationToken);
        if (hasOverlap)
            return Result<ResourceCostProfileDto>.Failure(Error.Validation("The effective date range overlaps another cost profile for this resource."));

        var profile = dto.ToEntity();
        // The resource was resolved within the caller's tenant scope. Copying its tenant
        // avoids assigning a profile to a fallback or mismatched tenant.
        profile.TenantId = resource.TenantId;
        profile.CreatedBy = userId;

        _context.Set<ResourceCostProfile>().Add(profile);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ResourceCostProfile",
            EntityId = profile.Id,
            Action = "Create",
            UserId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
            Timestamp = DateTime.UtcNow,
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            CreatedBy = userId
        }, cancellationToken);

        return Result<ResourceCostProfileDto>.Success(profile.ToDto());
    }

    public async Task<Result<ResourceCostProfileDto>> UpdateAsync(Guid resourceId, Guid id, UpdateResourceCostProfileDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var profile = await _context.Set<ResourceCostProfile>()
            .FirstOrDefaultAsync(cp => cp.Id == id && cp.ResourceId == resourceId && !cp.IsDeleted, cancellationToken);
        if (profile is null)
            return Result<ResourceCostProfileDto>.Failure(Error.NotFound("Resource Cost Profile not found"));

        if (dto.RowVersion is null || dto.RowVersion.Length == 0)
            return Result<ResourceCostProfileDto>.Failure(Error.Validation("RowVersion is required"));

        _context.Entry(profile).Property(x => x.RowVersion).OriginalValue = dto.RowVersion;

        var compensationType = dto.CompensationType ?? profile.CompensationType;
        if (!CompensationType.IsValid(compensationType))
            return Result<ResourceCostProfileDto>.Failure(Error.Validation("Invalid compensation type"));

        var effectiveFrom = dto.EffectiveFrom ?? profile.EffectiveFrom;
        var effectiveTo = dto.EffectiveTo ?? profile.EffectiveTo;
        if (effectiveFrom == default || (effectiveTo.HasValue && effectiveTo.Value.Date < effectiveFrom.Date))
            return Result<ResourceCostProfileDto>.Failure(Error.Validation("Effective date range is invalid"));

        var requestedEnd = effectiveTo ?? DateTime.MaxValue;
        var hasOverlap = await _context.Set<ResourceCostProfile>().AnyAsync(cp =>
            cp.Id != id && cp.ResourceId == resourceId && !cp.IsDeleted &&
            cp.EffectiveFrom <= requestedEnd &&
            (!cp.EffectiveTo.HasValue || cp.EffectiveTo.Value >= effectiveFrom), cancellationToken);
        if (hasOverlap)
            return Result<ResourceCostProfileDto>.Failure(Error.Validation("The effective date range overlaps another cost profile for this resource."));

        if (dto.Currency is not null)
        {
            var updateCurrency = dto.Currency.Trim();
            if (updateCurrency.Length != 3 || !updateCurrency.All(char.IsAsciiLetter))
                return Result<ResourceCostProfileDto>.Failure(Error.Validation("Select a configured currency.", nameof(dto.Currency)));
            dto.Currency = updateCurrency.ToUpperInvariant();
            if (!string.Equals(dto.Currency, profile.Currency, StringComparison.OrdinalIgnoreCase))
            {
                var currencyExists = await _context.Set<Currency>()
                    .AnyAsync(item => item.Code == dto.Currency && !item.IsDeleted, cancellationToken);
                if (!currencyExists)
                    return Result<ResourceCostProfileDto>.Failure(Error.Validation("The selected currency is not configured for this tenant.", nameof(dto.Currency)));
            }
        }

        var overtimeEligible = dto.OvertimeEligible ?? profile.OvertimeEligible;
        if (overtimeEligible)
        {
            var otType = dto.OvertimeRateType ?? profile.OvertimeRateType;
            if (!OvertimeRateTypeConstants.IsValid(otType))
                return Result<ResourceCostProfileDto>.Failure(Error.Validation("Valid overtime rate type is required when overtime is eligible"));
        }

        profile.ApplyUpdate(dto);
        profile.UpdatedBy = userId;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ResourceCostProfileDto>.Failure(Error.Conflict("This cost profile was modified by another user"));
        }

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ResourceCostProfile",
            EntityId = profile.Id,
            Action = "Update",
            UserId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
            Timestamp = DateTime.UtcNow,
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            CreatedBy = userId
        }, cancellationToken);

        return Result<ResourceCostProfileDto>.Success(profile.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid resourceId, Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var profile = await _context.Set<ResourceCostProfile>()
            .FirstOrDefaultAsync(cp => cp.Id == id && cp.ResourceId == resourceId && !cp.IsDeleted, cancellationToken);
        if (profile is null)
            return Result.Failure(Error.NotFound("Resource Cost Profile not found"));

        profile.IsDeleted = true;
        profile.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ResourceCostProfile",
            EntityId = profile.Id,
            Action = "Delete",
            UserId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }
}
