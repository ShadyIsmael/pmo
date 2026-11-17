using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.CostManagement.Application;

public sealed class PaymentObligationService : IPaymentObligationService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IProjectReferenceLookup _projectLookup;
    private readonly IProjectMilestoneReferenceLookup _milestoneLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor? _managementContext;

    public PaymentObligationService(
        DbContext context,
        IAuditService auditService,
        IProjectReferenceLookup projectLookup,
        IProjectMilestoneReferenceLookup milestoneLookup,
        IHttpContextAccessor httpContextAccessor,
        IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _auditService = auditService;
        _projectLookup = projectLookup;
        _milestoneLookup = milestoneLookup;
        _httpContextAccessor = httpContextAccessor;
        _managementContext = managementContext;
    }

    public async Task<Result<PaymentObligationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var obligation = await ApplyManagementScope(_context.Set<PaymentObligation>())
            .Include(item => item.Settlements)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

        return obligation is null
            ? Result<PaymentObligationDto>.Failure(Error.NotFound("Payment obligation not found"))
            : Result<PaymentObligationDto>.Success(obligation.ToDto());
    }

    public async Task<PagedResult<PaymentObligationDto>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, Guid? milestoneId = null, bool? dueOnly = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = ApplyManagementScope(_context.Set<PaymentObligation>().AsNoTracking().Where(item => !item.IsDeleted).Include(item => item.Settlements));
        if (projectId.HasValue) query = query.Where(item => item.ProjectId == projectId.Value);
        if (milestoneId.HasValue) query = query.Where(item => item.MilestoneId == milestoneId.Value);

        if (dueOnly == true)
        {
            var asOfDate = DateTime.UtcNow.Date;
            query = query.Where(item =>
                item.DueDate.Date <= asOfDate
                && item.Amount > (item.Settlements.Where(settlement => !settlement.IsDeleted)
                    .Sum(settlement => (decimal?)settlement.Amount) ?? 0m));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.DueDate)
            .ThenBy(item => item.PaymentReference)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtoItems = items.Select(item => item.ToDto()).ToList();
        return new PagedResult<PaymentObligationDto>(dtoItems, page, pageSize, total);
    }

    public async Task<Result<PaymentObligationDto>> CreateAsync(CreatePaymentObligationDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (dto.ProjectId == Guid.Empty) return Result<PaymentObligationDto>.Failure(Error.Validation("ProjectId is required"));
        if (dto.Amount <= 0m || !HasMonetaryPrecision(dto.Amount)) return Result<PaymentObligationDto>.Failure(Error.Validation("Amount must be greater than zero and use no more than four decimal places"));
        if (string.IsNullOrWhiteSpace(dto.PaymentReference)) return Result<PaymentObligationDto>.Failure(Error.Validation("Payment reference is required"));
        if (string.IsNullOrWhiteSpace(dto.Purpose)) return Result<PaymentObligationDto>.Failure(Error.Validation("Purpose is required"));
        if (dto.PaymentReference.Trim().Length > 100 || dto.Purpose.Trim().Length > 250) return Result<PaymentObligationDto>.Failure(Error.Validation("Payment reference or purpose exceeds the maximum length"));
        if (dto.Description?.Length > 2000 || dto.Notes?.Length > 2000) return Result<PaymentObligationDto>.Failure(Error.Validation("Description or notes exceeds the maximum length"));
        if (dto.DueDate == default) return Result<PaymentObligationDto>.Failure(Error.Validation("Due date is required"));

        var project = await _projectLookup.FindAsync(dto.ProjectId, cancellationToken);
        if (project is null) return Result<PaymentObligationDto>.Failure(Error.NotFound("Project not found"));
        var writeError = EnsureWritableProject(dto.ProjectId);
        if (writeError is not null) return Result<PaymentObligationDto>.Failure(writeError);
        if (dto.MilestoneId.HasValue && !await _milestoneLookup.IsActiveMilestoneForProjectAsync(dto.ProjectId, dto.MilestoneId.Value, cancellationToken))
            return Result<PaymentObligationDto>.Failure(Error.Validation("Milestone must be an active milestone in a working plan for this project"));

        var projectCurrency = NormalizeCurrency(project.Currency);
        var currency = NormalizeCurrency(string.IsNullOrWhiteSpace(dto.Currency) ? projectCurrency : dto.Currency);
        if (!IsCurrencyCode(projectCurrency) || !IsCurrencyCode(currency))
            return Result<PaymentObligationDto>.Failure(Error.Validation("A valid three-letter project currency is required"));
        if (!string.Equals(currency, projectCurrency, StringComparison.OrdinalIgnoreCase))
            return Result<PaymentObligationDto>.Failure(Error.Validation("Payment currency must match the project currency"));

        var obligation = new PaymentObligation
        {
            TenantId = CurrentTenant(),
            CreatedBy = userId,
            ProjectId = dto.ProjectId,
            ProjectName = project.Name,
            MilestoneId = dto.MilestoneId,
            PaymentReference = dto.PaymentReference.Trim(),
            Purpose = dto.Purpose.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            Amount = dto.Amount,
            Currency = currency,
            DueDate = dto.DueDate,
            Notes = dto.Notes?.Trim(),
            RowVersion = NewVersion()
        };

        _context.Set<PaymentObligation>().Add(obligation);
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("PaymentObligation", obligation.Id, "Create", userId, null, dto, cancellationToken);

        return Result<PaymentObligationDto>.Success((await _context.Set<PaymentObligation>().Include(item => item.Settlements).AsNoTracking().SingleAsync(item => item.Id == obligation.Id, cancellationToken)).ToDto());
    }

    public async Task<Result<PaymentObligationDto>> UpdateAsync(Guid id, UpdatePaymentObligationDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var obligation = await ApplyManagementScope(_context.Set<PaymentObligation>()).Include(item => item.Settlements).FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (obligation is null) return Result<PaymentObligationDto>.Failure(Error.NotFound("Payment obligation not found"));
        if (!MatchesVersion(obligation.RowVersion, dto.RowVersion)) return Result<PaymentObligationDto>.Failure(Error.Conflict("Payment obligation was changed by another user"));
        if (dto.Amount <= 0m || !HasMonetaryPrecision(dto.Amount)) return Result<PaymentObligationDto>.Failure(Error.Validation("Amount must be greater than zero and use no more than four decimal places"));
        if (dto.Amount < obligation.SettledAmount) return Result<PaymentObligationDto>.Failure(Error.Validation("Payment amount cannot be less than the total settled amount"));
        if (string.IsNullOrWhiteSpace(dto.PaymentReference)) return Result<PaymentObligationDto>.Failure(Error.Validation("Payment reference is required"));
        if (string.IsNullOrWhiteSpace(dto.Purpose)) return Result<PaymentObligationDto>.Failure(Error.Validation("Purpose is required"));
        if (dto.PaymentReference.Trim().Length > 100 || dto.Purpose.Trim().Length > 250) return Result<PaymentObligationDto>.Failure(Error.Validation("Payment reference or purpose exceeds the maximum length"));
        if (dto.Description?.Length > 2000 || dto.Notes?.Length > 2000) return Result<PaymentObligationDto>.Failure(Error.Validation("Description or notes exceeds the maximum length"));
        if (dto.DueDate == default) return Result<PaymentObligationDto>.Failure(Error.Validation("Due date is required"));

        var project = await _projectLookup.FindAsync(obligation.ProjectId, cancellationToken);
        if (project is null) return Result<PaymentObligationDto>.Failure(Error.NotFound("Project not found"));
        var writeError = EnsureWritableProject(obligation.ProjectId);
        if (writeError is not null) return Result<PaymentObligationDto>.Failure(writeError);
        if (dto.MilestoneId.HasValue && !await _milestoneLookup.IsActiveMilestoneForProjectAsync(obligation.ProjectId, dto.MilestoneId.Value, cancellationToken))
            return Result<PaymentObligationDto>.Failure(Error.Validation("Milestone must be an active milestone in a working plan for this project"));
        var projectCurrency = NormalizeCurrency(project.Currency);
        var normalizedCurrency = NormalizeCurrency(string.IsNullOrWhiteSpace(dto.Currency) ? projectCurrency : dto.Currency);
        if (!IsCurrencyCode(projectCurrency) || !IsCurrencyCode(normalizedCurrency))
            return Result<PaymentObligationDto>.Failure(Error.Validation("A valid three-letter project currency is required"));
        if (!string.Equals(normalizedCurrency, projectCurrency, StringComparison.OrdinalIgnoreCase))
            return Result<PaymentObligationDto>.Failure(Error.Validation("Payment currency must match the project currency"));

        var original = JsonSerializer.Serialize(obligation.ToDto());
        obligation.MilestoneId = dto.MilestoneId;
        obligation.PaymentReference = dto.PaymentReference.Trim();
        obligation.Purpose = dto.Purpose.Trim();
        obligation.Description = dto.Description?.Trim() ?? string.Empty;
        obligation.Amount = dto.Amount;
        obligation.Currency = normalizedCurrency;
        obligation.DueDate = dto.DueDate;
        obligation.Notes = dto.Notes?.Trim();
        obligation.UpdatedBy = userId;
        obligation.UpdatedAt = DateTime.UtcNow;
        obligation.RowVersion = NewVersion();

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PaymentObligationDto>.Failure(Error.Conflict("Payment obligation was changed by another user"));
        }
        await AuditAsync("PaymentObligation", obligation.Id, "Update", userId, original, dto, cancellationToken);

        return Result<PaymentObligationDto>.Success((await _context.Set<PaymentObligation>().Include(item => item.Settlements).AsNoTracking().SingleAsync(item => item.Id == obligation.Id, cancellationToken)).ToDto());
    }

    public async Task<Result> DeleteAsync(Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var obligation = await ApplyManagementScope(_context.Set<PaymentObligation>()).Include(item => item.Settlements).FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (obligation is null) return Result.Failure(Error.NotFound("Payment obligation not found"));
        var writeError = EnsureWritableProject(obligation.ProjectId);
        if (writeError is not null) return Result.Failure(writeError);
        if (!MatchesVersion(obligation.RowVersion, rowVersion)) return Result.Failure(Error.Conflict("Payment obligation was changed by another user"));
        if (obligation.Settlements.Any(item => !item.IsDeleted)) return Result.Failure(Error.Validation("A payment with settlements cannot be deleted"));

        obligation.IsDeleted = true;
        obligation.UpdatedBy = userId;
        obligation.UpdatedAt = DateTime.UtcNow;
        obligation.RowVersion = NewVersion();

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Conflict("Payment obligation was changed by another user"));
        }
        await AuditAsync("PaymentObligation", obligation.Id, "Delete", userId, null, null, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<PaymentSettlementDto>> RegisterSettlementAsync(Guid paymentObligationId, RecordPaymentSettlementDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var obligation = await ApplyManagementScope(_context.Set<PaymentObligation>()).Include(item => item.Settlements).FirstOrDefaultAsync(item => item.Id == paymentObligationId && !item.IsDeleted, cancellationToken);
        if (obligation is null) return Result<PaymentSettlementDto>.Failure(Error.NotFound("Payment obligation not found"));
        var writeError = EnsureWritableProject(obligation.ProjectId);
        if (writeError is not null) return Result<PaymentSettlementDto>.Failure(writeError);
        if (dto.Amount <= 0m || !HasMonetaryPrecision(dto.Amount)) return Result<PaymentSettlementDto>.Failure(Error.Validation("Settlement amount must be greater than zero and use no more than four decimal places"));
        if (string.IsNullOrWhiteSpace(dto.PaymentReference)) return Result<PaymentSettlementDto>.Failure(Error.Validation("Payment reference is required"));
        if (dto.PaymentReference.Trim().Length > 100 || dto.Notes?.Length > 2000) return Result<PaymentSettlementDto>.Failure(Error.Validation("Settlement reference or notes exceeds the maximum length"));
        if (dto.SettlementDate == default) return Result<PaymentSettlementDto>.Failure(Error.Validation("Settlement date is required"));
        if (dto.IdempotencyKey == Guid.Empty) return Result<PaymentSettlementDto>.Failure(Error.Validation("Idempotency key is required"));

        var existingSettlement = await _context.Set<PaymentSettlement>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdempotencyKey == dto.IdempotencyKey, cancellationToken);
        if (existingSettlement is not null)
        {
            if (existingSettlement.PaymentObligationId == paymentObligationId
                && existingSettlement.Amount == dto.Amount
                && existingSettlement.SettlementDate == dto.SettlementDate
                && existingSettlement.PaymentReference == dto.PaymentReference.Trim()
                && existingSettlement.Notes == dto.Notes?.Trim())
                return Result<PaymentSettlementDto>.Success(existingSettlement.ToDto());
            return Result<PaymentSettlementDto>.Failure(Error.Conflict("Idempotency key was already used for a different settlement"));
        }

        var outstanding = obligation.Amount - obligation.Settlements.Where(item => !item.IsDeleted).Sum(item => item.Amount);
        if (dto.Amount > outstanding) return Result<PaymentSettlementDto>.Failure(Error.Validation("Settlement amount cannot exceed the outstanding balance"));

        var settlement = new PaymentSettlement
        {
            TenantId = CurrentTenant(),
            CreatedBy = userId,
            PaymentObligationId = obligation.Id,
            PaymentObligation = obligation,
            IdempotencyKey = dto.IdempotencyKey,
            Amount = dto.Amount,
            SettlementDate = dto.SettlementDate,
            PaymentReference = dto.PaymentReference.Trim(),
            Notes = dto.Notes?.Trim(),
            RowVersion = NewVersion()
        };

        _context.Set<PaymentSettlement>().Add(settlement);
        obligation.UpdatedBy = userId;
        obligation.UpdatedAt = DateTime.UtcNow;
        obligation.RowVersion = NewVersion();

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PaymentSettlementDto>.Failure(Error.Conflict("Payment balance changed while recording this settlement. Refresh and try again."));
        }
        catch (DbUpdateException)
        {
            var duplicate = await _context.Set<PaymentSettlement>().AsNoTracking()
                .FirstOrDefaultAsync(item => item.IdempotencyKey == dto.IdempotencyKey, cancellationToken);
            if (duplicate is null) throw;
            return duplicate.PaymentObligationId == paymentObligationId
                && duplicate.Amount == dto.Amount
                && duplicate.SettlementDate == dto.SettlementDate
                && duplicate.PaymentReference == dto.PaymentReference.Trim()
                && duplicate.Notes == dto.Notes?.Trim()
                ? Result<PaymentSettlementDto>.Success(duplicate.ToDto())
                : Result<PaymentSettlementDto>.Failure(Error.Conflict("Idempotency key was already used for a different settlement"));
        }
        await AuditAsync("PaymentSettlement", settlement.Id, "Create", userId, null, dto, cancellationToken);

        return Result<PaymentSettlementDto>.Success(settlement.ToDto());
    }

    public async Task<PagedResult<PaymentSettlementDto>> GetSettlementsAsync(Guid paymentObligationId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        if (!await ApplyManagementScope(_context.Set<PaymentObligation>()).AnyAsync(item => item.Id == paymentObligationId && !item.IsDeleted, cancellationToken))
            return new PagedResult<PaymentSettlementDto>(Array.Empty<PaymentSettlementDto>(), page, pageSize, 0);
        var query = _context.Set<PaymentSettlement>().AsNoTracking().Where(item => item.PaymentObligationId == paymentObligationId && !item.IsDeleted);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.SettlementDate).ThenBy(item => item.PaymentReference).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<PaymentSettlementDto>(items.Select(item => item.ToDto()).ToList(), page, pageSize, total);
    }

    private IQueryable<PaymentObligation> ApplyManagementScope(IQueryable<PaymentObligation> query)
    {
        if (_managementContext is null) return query;
        var scope = _managementContext.Current?.Scope;
        if (scope is null) return query.Where(_ => false);
        var projectIds = scope.ProjectIds.ToArray();
        return query.Where(item => projectIds.Contains(item.ProjectId));
    }

    private bool HasProjectScope(Guid projectId)
    {
        if (_managementContext is null) return true;
        var scope = _managementContext.Current?.Scope;
        return scope is not null && scope.ProjectIds.Contains(projectId);
    }

    private Error? EnsureWritableProject(Guid projectId)
    {
        if (_managementContext?.Current?.EffectiveReadOnly == true)
            return Error.Forbidden("This action is not available while using read-only perspective mode");
        return HasProjectScope(projectId) ? null : Error.Forbidden("Project is outside the current management scope");
    }

    private Guid CurrentTenant() => Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value, out var tenantId) ? tenantId : TenantConstants.DemoTenantId;
    private static byte[] NewVersion() => Guid.NewGuid().ToByteArray();
    private static bool MatchesVersion(byte[] current, byte[] requested) => requested.Length > 0 && current.Length > 0 && current.SequenceEqual(requested);
    private static bool HasMonetaryPrecision(decimal amount) => decimal.Round(amount, 4) == amount;
    private static string NormalizeCurrency(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private static bool IsCurrencyCode(string value) => value.Length == 3 && value.All(character => character is >= 'A' and <= 'Z');
    private async Task AuditAsync(string type, Guid id, string action, string userId, string? original, object? values, CancellationToken cancellationToken) => await _auditService.LogAsync(new AuditEntry { EntityType = type, EntityId = id, Action = action, UserId = Guid.TryParse(userId, out var parsed) ? parsed : null, OriginalValues = original, NewValues = values is null ? null : JsonSerializer.Serialize(values), Timestamp = DateTime.UtcNow, CreatedBy = userId }, cancellationToken);
}
