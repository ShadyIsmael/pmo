using System.Text.Json;
using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Organization.Application.Settings;
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
    private readonly ITenantFiscalYearService? _tenantFiscalYear;

    public PaymentObligationService(
        DbContext context,
        IAuditService auditService,
        IProjectReferenceLookup projectLookup,
        IProjectMilestoneReferenceLookup milestoneLookup,
        IHttpContextAccessor httpContextAccessor,
        IManagementContextAccessor? managementContext = null,
        ITenantFiscalYearService? tenantFiscalYear = null)
    {
        _context = context;
        _auditService = auditService;
        _projectLookup = projectLookup;
        _milestoneLookup = milestoneLookup;
        _httpContextAccessor = httpContextAccessor;
        _managementContext = managementContext;
        _tenantFiscalYear = tenantFiscalYear;
    }

    public async Task<Result<PaymentObligationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "PaymentObligation.View");
        if (accessError is not null) return Result<PaymentObligationDto>.Failure(accessError);
        var obligation = await ApplyManagementScope(_context.Set<PaymentObligation>().Where(item => item.TenantId == CurrentTenant()))
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
        if (FinancePermissionGuard.Require(_httpContextAccessor, "PaymentObligation.View") is not null)
            return new PagedResult<PaymentObligationDto>(Array.Empty<PaymentObligationDto>(), page, pageSize, 0);

        var query = ApplyManagementScope(_context.Set<PaymentObligation>().AsNoTracking()
            .Where(item => item.TenantId == CurrentTenant() && !item.IsDeleted).Include(item => item.Settlements));
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
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "PaymentObligation.Create");
        if (accessError is not null) return Result<PaymentObligationDto>.Failure(accessError);
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
        await AppendFinancialAuditAsync("PaymentObligation", obligation.Id, "PaymentObligationCreated", userId,
            obligation.ProjectId, obligation.TenantId, null, obligation.PaymentReference, null, obligation.Amount,
            null, obligation.Currency, null, obligation.ToDto(), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<PaymentObligationDto>.Success((await _context.Set<PaymentObligation>().Include(item => item.Settlements).AsNoTracking().SingleAsync(item => item.Id == obligation.Id, cancellationToken)).ToDto());
    }

    public async Task<Result<PaymentObligationDto>> UpdateAsync(Guid id, UpdatePaymentObligationDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "PaymentObligation.Edit");
        if (accessError is not null) return Result<PaymentObligationDto>.Failure(accessError);
        var obligation = await ApplyManagementScope(_context.Set<PaymentObligation>().Where(item => item.TenantId == CurrentTenant()))
            .Include(item => item.Settlements).FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
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
        if (obligation.Settlements.Any(item => !item.IsDeleted)
            && !string.Equals(NormalizeCurrency(obligation.Currency), normalizedCurrency, StringComparison.Ordinal))
            return Result<PaymentObligationDto>.Failure(Error.Conflict(
                "Payment currency cannot change after an actual settlement has been recorded."));

        var original = JsonSerializer.Serialize(obligation.ToDto());
        var beforeAmount = obligation.Amount;
        var beforeReference = obligation.PaymentReference;
        var beforeCurrency = obligation.Currency;
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

        await AppendFinancialAuditAsync("PaymentObligation", obligation.Id, "PaymentObligationUpdated", userId,
            obligation.ProjectId, obligation.TenantId, beforeReference, obligation.PaymentReference,
            beforeAmount, obligation.Amount, beforeCurrency, obligation.Currency, original, dto, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PaymentObligationDto>.Failure(Error.Conflict("Payment obligation was changed by another user"));
        }
        return Result<PaymentObligationDto>.Success((await _context.Set<PaymentObligation>().Include(item => item.Settlements).AsNoTracking().SingleAsync(item => item.Id == obligation.Id, cancellationToken)).ToDto());
    }

    public async Task<Result> DeleteAsync(Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "PaymentObligation.Delete");
        if (accessError is not null) return Result.Failure(accessError);
        var obligation = await ApplyManagementScope(_context.Set<PaymentObligation>().Where(item => item.TenantId == CurrentTenant()))
            .Include(item => item.Settlements).FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (obligation is null) return Result.Failure(Error.NotFound("Payment obligation not found"));
        var writeError = EnsureWritableProject(obligation.ProjectId);
        if (writeError is not null) return Result.Failure(writeError);
        if (!MatchesVersion(obligation.RowVersion, rowVersion)) return Result.Failure(Error.Conflict("Payment obligation was changed by another user"));
        if (obligation.Settlements.Any(item => !item.IsDeleted)) return Result.Failure(Error.Validation("A payment with settlements cannot be deleted"));

        var original = JsonSerializer.Serialize(obligation.ToDto());
        await AppendFinancialAuditAsync("PaymentObligation", obligation.Id, "PaymentObligationDeleted", userId,
            obligation.ProjectId, obligation.TenantId, obligation.PaymentReference, null,
            obligation.Amount, null, obligation.Currency, null, original, new { isDeleted = true }, cancellationToken);
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
        return Result.Success();
    }

    public async Task<Result<PaymentSettlementDto>> RegisterSettlementAsync(Guid paymentObligationId, RecordPaymentSettlementDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "PaymentObligationSettlement.Record");
        if (accessError is not null) return Result<PaymentSettlementDto>.Failure(accessError);
        if (!Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value, out var tenantId)
            || tenantId == Guid.Empty)
            return Result<PaymentSettlementDto>.Failure(Error.Forbidden("A valid Tenant context is required."));
        var obligationScope = await ApplyManagementScope(_context.Set<PaymentObligation>().AsNoTracking())
            .Where(item => item.Id == paymentObligationId && !item.IsDeleted && item.TenantId == tenantId)
            .Select(item => new { item.ProjectId })
            .FirstOrDefaultAsync(cancellationToken);
        if (obligationScope is null) return Result<PaymentSettlementDto>.Failure(Error.NotFound("Payment obligation not found"));
        if (!HasSettlementRecordPermission(userId))
            return Result<PaymentSettlementDto>.Failure(Error.Forbidden("The PaymentObligationSettlement.Record permission is required to record an actual settlement."));
        var writeError = EnsureWritableProject(obligationScope.ProjectId);
        if (writeError is not null) return Result<PaymentSettlementDto>.Failure(writeError);
        if (dto.Amount <= 0m || !HasMonetaryPrecision(dto.Amount)) return Result<PaymentSettlementDto>.Failure(Error.Validation("Settlement amount must be greater than zero and use no more than four decimal places"));
        if (string.IsNullOrWhiteSpace(dto.PaymentReference)) return Result<PaymentSettlementDto>.Failure(Error.Validation("Payment reference is required"));
        if (dto.PaymentReference.Trim().Length > 100 || dto.Notes?.Length > 2000) return Result<PaymentSettlementDto>.Failure(Error.Validation("Settlement reference or notes exceeds the maximum length"));
        if (dto.SettlementDate == default) return Result<PaymentSettlementDto>.Failure(Error.Validation("Settlement date is required"));
        if (dto.IdempotencyKey == Guid.Empty) return Result<PaymentSettlementDto>.Failure(Error.Validation("Idempotency key is required"));

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await LockProjectForFinancialUpdateAsync(tenantId, obligationScope.ProjectId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PaymentSettlementDto>.Failure(Error.NotFound("Project not found"));
        }

        var obligation = await ApplyManagementScope(_context.Set<PaymentObligation>())
            .Include(item => item.Settlements)
            .FirstOrDefaultAsync(item => item.Id == paymentObligationId && item.TenantId == tenantId
                && item.ProjectId == obligationScope.ProjectId && !item.IsDeleted, cancellationToken);
        if (obligation is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PaymentSettlementDto>.Failure(Error.NotFound("Payment obligation not found"));
        }

        var existingSettlement = await _context.Set<PaymentSettlement>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.IdempotencyKey == dto.IdempotencyKey, cancellationToken);
        if (existingSettlement is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            if (existingSettlement.PaymentObligationId == paymentObligationId
                && existingSettlement.ProjectPaymentRequestId is null
                && existingSettlement.Amount == dto.Amount
                && existingSettlement.SettlementDate == dto.SettlementDate
                && existingSettlement.PaymentReference == dto.PaymentReference.Trim()
                && existingSettlement.Notes == dto.Notes?.Trim())
                return Result<PaymentSettlementDto>.Success(existingSettlement.ToDto());
            return Result<PaymentSettlementDto>.Failure(Error.Conflict("Idempotency key was already used for a different settlement"));
        }

        if (_tenantFiscalYear is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PaymentSettlementDto>.Failure(Error.Conflict(
                "Tenant Fiscal Year configuration is unavailable; the settlement cannot be assigned to an open financial period."));
        }

        var settlementPeriod = await _tenantFiscalYear.ResolveForDateAsync(
            tenantId, DateOnly.FromDateTime(dto.SettlementDate), cancellationToken);
        var isClosed = await _context.Set<ProjectAnnualBudgetClosing>().AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.ProjectId == obligation.ProjectId
                && item.FiscalYear == settlementPeriod.FiscalYear
                && item.Status == ProjectAnnualBudgetClosingStatuses.Closed && !item.IsDeleted, cancellationToken)
            || await _context.Set<ProjectAnnualBudget>().AsNoTracking()
                .AnyAsync(item => item.TenantId == tenantId && item.ProjectId == obligation.ProjectId
                    && item.FiscalYear == settlementPeriod.FiscalYear && item.ClosedAtUtc.HasValue && !item.IsDeleted, cancellationToken);
        if (isClosed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PaymentSettlementDto>.Failure(Error.Conflict(
                "Actual settlements cannot be posted into a closed Project Fiscal Year."));
        }

        var outstanding = obligation.Amount - obligation.Settlements.Where(item => !item.IsDeleted).Sum(item => item.Amount);
        if (dto.Amount > outstanding)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PaymentSettlementDto>.Failure(Error.Validation("Settlement amount cannot exceed the outstanding balance"));
        }

        var settlement = new PaymentSettlement
        {
            TenantId = tenantId,
            CreatedBy = userId,
            PaymentObligationId = obligation.Id,
            ProjectId = obligation.ProjectId,
            PaymentObligation = obligation,
            IdempotencyKey = dto.IdempotencyKey,
            Amount = dto.Amount,
            Currency = obligation.Currency,
            SettlementDate = dto.SettlementDate,
            PaymentReference = dto.PaymentReference.Trim(),
            Description = dto.Notes?.Trim(),
            Notes = dto.Notes?.Trim(),
            RowVersion = NewVersion()
        };

        var paidBefore = obligation.SettledAmount;
        var balanceBefore = Math.Max(0m, obligation.Amount - paidBefore);
        var paidAfter = paidBefore + settlement.Amount;
        var balanceAfter = Math.Max(0m, obligation.Amount - paidAfter);
        var actorId = Guid.TryParse(userId, out var parsedActor) ? parsedActor : (Guid?)null;
        await _auditService.AppendAsync(new AuditEntry
        {
            ProjectId = obligation.ProjectId,
            TenantId = settlement.TenantId,
            EntityType = "PaymentSettlement",
            EntityId = settlement.Id,
            Action = "SettlementRecorded",
            UserId = actorId,
            UserName = CurrentUserName,
            Timestamp = DateTime.UtcNow,
            Description = $"Payment settlement recorded against {obligation.PaymentReference}",
            Changes = JsonSerializer.Serialize(new
            {
                financialReference = new { from = (string?)null, to = settlement.PaymentReference },
                amount = new { from = (decimal?)null, to = settlement.Amount },
                currency = new { from = (string?)null, to = obligation.Currency },
                settlementDate = new { from = (DateTime?)null, to = settlement.SettlementDate },
                paidToDate = new { from = paidBefore, to = paidAfter },
                outstandingPaymentBalance = new { from = balanceBefore, to = balanceAfter }
            }),
            OriginalValues = JsonSerializer.Serialize(new { paidToDate = paidBefore, outstandingPaymentBalance = balanceBefore }),
            NewValues = JsonSerializer.Serialize(new
            {
                financialReference = settlement.PaymentReference,
                amount = settlement.Amount,
                currency = obligation.Currency,
                settlementDate = settlement.SettlementDate,
                paidToDate = paidAfter,
                outstandingPaymentBalance = balanceAfter
            }),
            CreatedBy = userId
        }, cancellationToken);

        _context.Set<PaymentSettlement>().Add(settlement);
        obligation.UpdatedBy = userId;
        obligation.UpdatedAt = DateTime.UtcNow;
        obligation.RowVersion = NewVersion();

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PaymentSettlementDto>.Failure(Error.Conflict("Payment balance changed while recording this settlement. Refresh and try again."));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            var duplicate = await _context.Set<PaymentSettlement>().AsNoTracking()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.IdempotencyKey == dto.IdempotencyKey, cancellationToken);
            if (duplicate is null) throw;
            return duplicate.PaymentObligationId == paymentObligationId
                && duplicate.Amount == dto.Amount
                && duplicate.SettlementDate == dto.SettlementDate
                && duplicate.PaymentReference == dto.PaymentReference.Trim()
                && duplicate.Notes == dto.Notes?.Trim()
                ? Result<PaymentSettlementDto>.Success(duplicate.ToDto())
                : Result<PaymentSettlementDto>.Failure(Error.Conflict("Idempotency key was already used for a different settlement"));
        }
        return Result<PaymentSettlementDto>.Success(settlement.ToDto());
    }

    private async Task<bool> LockProjectForFinancialUpdateAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!string.Equals(_context.Database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
            return true;

        var connection = _context.Database.GetDbConnection();
        await using DbCommand command = connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT [Id] FROM [Projects] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = @projectId AND [TenantId] = @tenantId AND [IsDeleted] = 0";
        AddParameter(command, "@projectId", projectId);
        AddParameter(command, "@tenantId", tenantId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null and not DBNull;
    }

    private static void AddParameter(DbCommand command, string name, Guid value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.Guid;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    public async Task<PagedResult<PaymentSettlementDto>> GetSettlementsAsync(Guid paymentObligationId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        if (FinancePermissionGuard.Require(_httpContextAccessor, "PaymentObligation.View") is not null)
            return new PagedResult<PaymentSettlementDto>(Array.Empty<PaymentSettlementDto>(), page, pageSize, 0);

        if (!await ApplyManagementScope(_context.Set<PaymentObligation>().Where(item => item.TenantId == CurrentTenant()))
                .AnyAsync(item => item.Id == paymentObligationId && !item.IsDeleted, cancellationToken))
            return new PagedResult<PaymentSettlementDto>(Array.Empty<PaymentSettlementDto>(), page, pageSize, 0);
        var query = _context.Set<PaymentSettlement>().AsNoTracking().Where(item => item.TenantId == CurrentTenant()
            && item.PaymentObligationId == paymentObligationId && !item.IsDeleted);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.SettlementDate).ThenBy(item => item.PaymentReference).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<PaymentSettlementDto>(items.Select(item => item.ToDto()).ToList(), page, pageSize, total);
    }

    private IQueryable<PaymentObligation> ApplyManagementScope(IQueryable<PaymentObligation> query)
    {
        var management = _managementContext?.Current;
        if (management is null || management.TenantId != CurrentTenant() || management.Scope is null)
            return query.Where(_ => false);
        var projectIds = management.Scope.ProjectIds.ToArray();
        return query.Where(item => projectIds.Contains(item.ProjectId));
    }

    private bool HasProjectScope(Guid projectId)
    {
        var management = _managementContext?.Current;
        return management is not null && management.TenantId == CurrentTenant() && management.Scope is not null
            && management.Scope.ProjectIds.Contains(projectId);
    }

    private Error? EnsureWritableProject(Guid projectId)
    {
        if (_managementContext?.Current?.EffectiveReadOnly == true)
            return Error.Forbidden("This action is not available while using read-only perspective mode");
        return HasProjectScope(projectId) ? null : Error.Forbidden("Project is outside the current management scope");
    }

    private bool HasSettlementRecordPermission(string userId)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var actorId = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var managementUserId = _managementContext?.Current?.UserId;
        return string.Equals(actorId, userId, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(userId, out var parsedActor)
            && parsedActor != Guid.Empty
            && managementUserId == parsedActor
            && FinancePermissionGuard.HasCurrentPermission(_httpContextAccessor, "PaymentObligationSettlement.Record");
    }

    private Guid CurrentTenant() => FinancePermissionGuard.TryGetTenantId(_httpContextAccessor.HttpContext?.User, out var tenantId) ? tenantId : Guid.Empty;
    private string? CurrentUserName => _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
        ?? _httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value;
    private static byte[] NewVersion() => Guid.NewGuid().ToByteArray();
    private static bool MatchesVersion(byte[] current, byte[] requested) => requested.Length > 0 && current.Length > 0 && current.SequenceEqual(requested);
    private static bool HasMonetaryPrecision(decimal amount) => decimal.Round(amount, 4) == amount;
    private static string NormalizeCurrency(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private static bool IsCurrencyCode(string value) => value.Length == 3 && value.All(character => character is >= 'A' and <= 'Z');
    private Task AppendFinancialAuditAsync(
        string entityType,
        Guid entityId,
        string action,
        string userId,
        Guid projectId,
        Guid tenantId,
        string? beforeReference,
        string? afterReference,
        decimal? beforeAmount,
        decimal? afterAmount,
        string? beforeCurrency,
        string? afterCurrency,
        string? original,
        object? values,
        CancellationToken cancellationToken)
    {
        var actorId = Guid.TryParse(userId, out var parsed) ? parsed : (Guid?)null;
        var financialReference = afterReference ?? beforeReference ?? entityId.ToString("D");
        return _auditService.AppendAsync(new AuditEntry
        {
            ProjectId = projectId,
            TenantId = tenantId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = actorId,
            UserName = CurrentUserName,
            Timestamp = DateTime.UtcNow,
            Description = $"{action.Replace("PaymentObligation", "Payment obligation", StringComparison.Ordinal)}: {financialReference}",
            Changes = JsonSerializer.Serialize(new
            {
                financialReference = new { from = beforeReference, to = afterReference ?? financialReference },
                amount = new { from = beforeAmount, to = afterAmount },
                currency = new { from = beforeCurrency, to = afterCurrency }
            }),
            OriginalValues = original,
            NewValues = values is null ? null : JsonSerializer.Serialize(values),
            CreatedBy = userId
        }, cancellationToken);
    }
}
