using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.CostManagement.Application;

/// <summary>
/// Project-scoped Planned Payment registration, retrieval, pre-lock amendment and
/// first-request lock. Registration is the only lifetime-budget reservation point;
/// Payment Requests and settlements never create another reservation.
/// </summary>
public sealed class PlannedPaymentService : IPlannedPaymentService
{
    private const string ViewPermission = "PaymentObligation.View";
    private const string CreatePermission = "PaymentObligation.Create";
    private const string EditPermission = "PaymentObligation.Edit";
    private const string EntityType = "PlannedPayment";
    private const string PaymentRequestType = "ProjectPaymentRequest";

    private readonly DbContext _db;
    private readonly IProjectReferenceLookup _projectLookup;
    private readonly IProjectMilestoneReferenceLookup _milestoneLookup;
    private readonly ICurrentProjectManagerLookup _currentManagerLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor? _management;

    public PlannedPaymentService(
        DbContext context,
        IProjectReferenceLookup projectLookup,
        IProjectMilestoneReferenceLookup milestoneLookup,
        ICurrentProjectManagerLookup currentManagerLookup,
        IHttpContextAccessor httpContextAccessor,
        IManagementContextAccessor? management = null)
    {
        _db = context;
        _projectLookup = projectLookup;
        _milestoneLookup = milestoneLookup;
        _currentManagerLookup = currentManagerLookup;
        _httpContextAccessor = httpContextAccessor;
        _management = management;
    }

    public async Task<Result<PlannedPaymentPageDto>> GetPageAsync(
        Guid tenantId, Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId, projectId, ViewPermission, requireWrite: false);
        if (accessError is not null) return Result<PlannedPaymentPageDto>.Failure(accessError);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.Set<PlannedPayment>().AsNoTracking()
            .Include(item => item.Installments)
            .Include(item => item.Reservation)
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted);
        var total = await query.CountAsync(cancellationToken);
        var plans = await query
            .OrderByDescending(item => item.RegisteredAtUtc ?? item.CreatedAt)
            .ThenBy(item => item.Reference)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<PlannedPaymentDto>(plans.Count);
        foreach (var plan in plans) items.Add(await ToDtoAsync(plan, cancellationToken));
        return Result<PlannedPaymentPageDto>.Success(
            new PlannedPaymentPageDto(items, page, pageSize, total));
    }

    public async Task<Result<PlannedPaymentDto>> GetByIdAsync(
        Guid tenantId, Guid projectId, Guid plannedPaymentId, CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId, projectId, ViewPermission, requireWrite: false);
        if (accessError is not null) return Result<PlannedPaymentDto>.Failure(accessError);

        var plan = await PlansQuery(tenantId, projectId)
            .FirstOrDefaultAsync(item => item.Id == plannedPaymentId, cancellationToken);
        return plan is null
            ? Result<PlannedPaymentDto>.Failure(Error.NotFound("Planned Payment not found."))
            : Result<PlannedPaymentDto>.Success(await ToDtoAsync(plan, cancellationToken));
    }

    public async Task<Result<PlannedPaymentBudgetDto>> GetBudgetAsync(
        Guid tenantId, Guid projectId, CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId, projectId, ViewPermission, requireWrite: false);
        if (accessError is not null) return Result<PlannedPaymentBudgetDto>.Failure(accessError);

        var reference = await _projectLookup.FindAsync(projectId, cancellationToken);
        if (reference is null || (reference.TenantId != Guid.Empty && reference.TenantId != tenantId))
            return Result<PlannedPaymentBudgetDto>.Failure(Error.NotFound("Project not found."));
        var currency = NormalizeCurrency(reference.Currency);
        if (!IsWellFormedCurrency(currency) || !await IsConfiguredCurrencyAsync(currency, cancellationToken))
            return Result<PlannedPaymentBudgetDto>.Failure(Error.Conflict("The project currency is not configured."));

        var lifetime = await LoadLifetimeBudgetAsync(tenantId, projectId, currency, null, cancellationToken);
        if (lifetime.Error is not null) return Result<PlannedPaymentBudgetDto>.Failure(lifetime.Error);
        var canRegister = FinancePermissionGuard.HasCurrentPermission(_httpContextAccessor, CreatePermission)
            && _management?.Current?.EffectiveReadOnly != true;
        return Result<PlannedPaymentBudgetDto>.Success(new PlannedPaymentBudgetDto(
            projectId,
            currency,
            reference.CurrentApprovedProjectBudget,
            lifetime.Consumed,
            lifetime.Reserved,
            lifetime.Available,
            canRegister));
    }

    public async Task<Result<PlannedPaymentDto>> RegisterAsync(
        Guid tenantId,
        Guid projectId,
        Guid actorId,
        string? actorName,
        CreatePlannedPaymentDto dto,
        CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId, projectId, CreatePermission, requireWrite: true);
        if (accessError is not null) return Result<PlannedPaymentDto>.Failure(accessError);
        if (dto.IdempotencyKey == Guid.Empty)
            return Result<PlannedPaymentDto>.Failure(Error.Validation("A registration idempotency key is required."));

        // A coordinated Quick One-off command may already own the serializable transaction.
        // Standalone registrations still own and commit their transaction here.
        await using var transaction = _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        if (!await LockProjectAsync(tenantId, projectId, cancellationToken))
            return Result<PlannedPaymentDto>.Failure(Error.NotFound("Project not found."));

        var actorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (actorError is not null) return Result<PlannedPaymentDto>.Failure(actorError);
        if (!await _currentManagerLookup.IsCurrentAsync(tenantId, projectId, actorId, cancellationToken))
            return Result<PlannedPaymentDto>.Failure(Error.Forbidden("Only the current Project Manager may register a Planned Payment."));

        var reference = await _projectLookup.FindAsync(projectId, cancellationToken);
        if (reference is null || (reference.TenantId != Guid.Empty && reference.TenantId != tenantId))
            return Result<PlannedPaymentDto>.Failure(Error.NotFound("Project not found."));
        var currency = NormalizeCurrency(reference.Currency);
        if (!IsWellFormedCurrency(currency) || !await IsConfiguredCurrencyAsync(currency, cancellationToken))
            return Result<PlannedPaymentDto>.Failure(Error.Conflict("The project currency is not configured."));

        var installmentsError = await ValidateInstallmentsAsync(projectId, dto.Installments, cancellationToken);
        if (installmentsError is not null) return Result<PlannedPaymentDto>.Failure(installmentsError);
        var totalAmount = dto.Installments.Sum(item => item.Amount);
        var fingerprint = RegistrationFingerprint(dto.Description, currency, dto.Installments);

        var existing = await _db.Set<PlannedPayment>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.IdempotencyKey == dto.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RegistrationFingerprint, fingerprint, StringComparison.Ordinal))
                return Result<PlannedPaymentDto>.Failure(Error.Conflict(
                    "The registration idempotency key was already used for different content."));
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            var replay = await PlansQuery(tenantId, projectId).FirstOrDefaultAsync(item => item.Id == existing.Id, cancellationToken);
            return replay is null
                ? Result<PlannedPaymentDto>.Failure(Error.NotFound("Planned Payment not found."))
                : Result<PlannedPaymentDto>.Success(await ToDtoAsync(replay, cancellationToken));
        }

        var lifetime = await LoadLifetimeBudgetAsync(tenantId, projectId, currency, null, cancellationToken);
        if (lifetime.Error is not null) return Result<PlannedPaymentDto>.Failure(lifetime.Error);
        if (!reference.CurrentApprovedProjectBudget.HasValue)
            return Result<PlannedPaymentDto>.Failure(Error.Conflict("The project has no approved lifetime budget ceiling."));
        if (totalAmount > lifetime.Available)
            return Result<PlannedPaymentDto>.Failure(Error.BusinessValidation(
                "INSUFFICIENT_LIFETIME_BUDGET",
                $"Registration requires {totalAmount:N4} {currency} but only {lifetime.Available:N4} {currency} is available.",
                "installments"));

        var description = (dto.Description ?? string.Empty).Trim();
        var plan = new PlannedPayment
        {
            TenantId = tenantId,
            ProjectId = projectId,
            Reference = MakeReference(),
            Description = description,
            Currency = currency,
            TotalAmount = totalAmount,
            State = PlannedPaymentStates.Registered,
            IdempotencyKey = dto.IdempotencyKey,
            RegistrationFingerprint = fingerprint,
            RegisteredBy = actorId,
            RegisteredAtUtc = DateTime.UtcNow,
            CreatedBy = actorId.ToString("D")
        };
        for (var index = 0; index < dto.Installments.Count; index++)
        {
            var source = dto.Installments[index];
            plan.Installments.Add(new PlannedPaymentInstallment
            {
                TenantId = tenantId,
                ProjectId = projectId,
                Sequence = index + 1,
                Amount = source.Amount,
                DueDateUtc = source.DueDateUtc,
                PlanActivityId = source.PlanActivityId,
                Description = string.IsNullOrWhiteSpace(source.Description) ? null : source.Description.Trim(),
                CreatedBy = actorId.ToString("D")
            });
        }
        plan.Reservation = new PlannedPaymentLifetimeReservation
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ReservedAmount = totalAmount,
            ReleasedAmount = 0m,
            CreatedBy = actorId.ToString("D")
        };

        _db.Set<PlannedPayment>().Add(plan);
        AddAudit(plan, "Registered", "Planned Payment registered and lifetime budget reserved.", null,
            new { plan.Reference, plan.TotalAmount, plan.Currency, plan.State }, actorId, actorName);
        foreach (var installment in plan.Installments)
        {
            AddComponentAudit(plan, "PlannedPaymentInstallment", installment.Id, "Created",
                "Planned Payment installment created.", new
                {
                    plannedPaymentId = plan.Id,
                    installment.Sequence,
                    installment.Amount,
                    installment.DueDateUtc,
                    installment.PlanActivityId,
                    installment.Description
                }, "plannedPaymentId,sequence,amount,dueDateUtc,planActivityId,description", actorId, actorName);
        }
        AddComponentAudit(plan, "PlannedPaymentLifetimeReservation", plan.Reservation.Id, "Reserved",
            "Lifetime budget reserved for Planned Payment.", new
            {
                plannedPaymentId = plan.Id,
                plan.Reservation.ReservedAmount,
                plan.Reservation.ReleasedAmount,
                plan.Currency
            }, "plannedPaymentId,reservedAmount,releasedAmount,currency", actorId, actorName);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PlannedPaymentDto>.Failure(Error.Conflict("The project or Planned Payment changed. Refresh and retry."));
        }
        catch (DbUpdateException)
        {
            return Result<PlannedPaymentDto>.Failure(Error.Conflict("The registration reference or idempotency key is already in use."));
        }

        var created = await PlansQuery(tenantId, projectId).FirstOrDefaultAsync(item => item.Id == plan.Id, cancellationToken);
        return created is null
            ? Result<PlannedPaymentDto>.Failure(Error.NotFound("Planned Payment not found."))
            : Result<PlannedPaymentDto>.Success(await ToDtoAsync(created, cancellationToken));
    }

    public async Task<Result<PlannedPaymentDto>> UpdateAsync(
        Guid tenantId,
        Guid projectId,
        Guid plannedPaymentId,
        Guid actorId,
        string? actorName,
        UpdatePlannedPaymentDto dto,
        CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId, projectId, EditPermission, requireWrite: true);
        if (accessError is not null) return Result<PlannedPaymentDto>.Failure(accessError);
        if (dto.IdempotencyKey == Guid.Empty)
            return Result<PlannedPaymentDto>.Failure(Error.Validation("An amendment idempotency key is required."));

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await LockProjectAsync(tenantId, projectId, cancellationToken))
            return Result<PlannedPaymentDto>.Failure(Error.NotFound("Project not found."));

        var plan = await PlansQuery(tenantId, projectId, tracked: true)
            .FirstOrDefaultAsync(item => item.Id == plannedPaymentId, cancellationToken);
        if (plan is null) return Result<PlannedPaymentDto>.Failure(Error.NotFound("Planned Payment not found."));
        if (!MatchesVersion(plan.RowVersion, dto.RowVersion))
            return Result<PlannedPaymentDto>.Failure(Error.Conflict("The Planned Payment changed. Refresh and retry."));
        if (plan.IsLocked || plan.State is PlannedPaymentStates.Replaced or PlannedPaymentStates.Cancelled)
            return Result<PlannedPaymentDto>.Failure(Error.Conflict("The Planned Payment is financially locked and cannot be edited."));

        var actorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (actorError is not null) return Result<PlannedPaymentDto>.Failure(actorError);
        if (!await _currentManagerLookup.IsCurrentAsync(tenantId, projectId, actorId, cancellationToken))
            return Result<PlannedPaymentDto>.Failure(Error.Forbidden("Only the current Project Manager may edit a Planned Payment."));

        var reference = await _projectLookup.FindAsync(projectId, cancellationToken);
        if (reference is null || (reference.TenantId != Guid.Empty && reference.TenantId != tenantId))
            return Result<PlannedPaymentDto>.Failure(Error.NotFound("Project not found."));
        var currency = NormalizeCurrency(reference.Currency);
        if (!string.Equals(currency, NormalizeCurrency(plan.Currency), StringComparison.Ordinal)
            || !IsWellFormedCurrency(currency) || !await IsConfiguredCurrencyAsync(currency, cancellationToken))
            return Result<PlannedPaymentDto>.Failure(Error.Conflict("The project currency changed and this Planned Payment cannot be edited."));

        var installmentsError = await ValidateInstallmentsAsync(projectId, dto.Installments.Select(item => new CreatePlannedPaymentInstallmentDto(
            item.Amount, item.DueDateUtc, item.PlanActivityId, item.Description)).ToList(), cancellationToken);
        if (installmentsError is not null) return Result<PlannedPaymentDto>.Failure(installmentsError);
        var totalAmount = dto.Installments.Sum(item => item.Amount);
        var fingerprint = RegistrationFingerprint(dto.Description, currency, dto.Installments.Select(item => new CreatePlannedPaymentInstallmentDto(
            item.Amount, item.DueDateUtc, item.PlanActivityId, item.Description)).ToList());

        if (plan.UpdateIdempotencyKey.HasValue)
        {
            if (plan.UpdateIdempotencyKey.Value != dto.IdempotencyKey)
                return Result<PlannedPaymentDto>.Failure(Error.Conflict("The amendment idempotency key was already used for this Planned Payment."));
            if (!string.Equals(plan.UpdateFingerprint, fingerprint, StringComparison.Ordinal))
                return Result<PlannedPaymentDto>.Failure(Error.Conflict("The amendment idempotency key was already used for different content."));
            await transaction.CommitAsync(cancellationToken);
            return Result<PlannedPaymentDto>.Success(await ToDtoAsync(plan, cancellationToken));
        }

        var lifetime = await LoadLifetimeBudgetAsync(tenantId, projectId, currency, plan.Id, cancellationToken);
        if (lifetime.Error is not null) return Result<PlannedPaymentDto>.Failure(lifetime.Error);
        if (!reference.CurrentApprovedProjectBudget.HasValue)
            return Result<PlannedPaymentDto>.Failure(Error.Conflict("The project has no approved lifetime budget ceiling."));
        if (totalAmount > lifetime.Available)
            return Result<PlannedPaymentDto>.Failure(Error.BusinessValidation(
                "INSUFFICIENT_LIFETIME_BUDGET",
                $"The amendment requires {totalAmount:N4} {currency} but only {lifetime.Available:N4} {currency} is available.",
                "installments"));

        var before = new
        {
            plan.Reference,
            plan.Description,
            plan.TotalAmount,
            Installments = plan.Installments.OrderBy(item => item.Sequence)
                .Select(item => new { item.Id, item.Sequence, item.Amount, item.DueDateUtc, item.PlanActivityId }).ToList()
        };
        plan.Description = (dto.Description ?? string.Empty).Trim();
        plan.TotalAmount = totalAmount;
        plan.UpdateIdempotencyKey = dto.IdempotencyKey;
        plan.UpdateFingerprint = fingerprint;
        plan.UpdatedBy = actorId.ToString("D");
        plan.UpdatedAt = DateTime.UtcNow;

        var existingInstallments = plan.Installments.ToDictionary(item => item.Id);
        var retainedIds = new HashSet<Guid>();
        for (var index = 0; index < dto.Installments.Count; index++)
        {
            var source = dto.Installments[index];
            PlannedPaymentInstallment installment;
            if (source.Id.HasValue && source.Id.Value != Guid.Empty && existingInstallments.TryGetValue(source.Id.Value, out var found))
            {
                installment = found;
                retainedIds.Add(installment.Id);
            }
            else
            {
                installment = new PlannedPaymentInstallment
                {
                    TenantId = tenantId,
                    ProjectId = projectId,
                    CreatedBy = actorId.ToString("D")
                };
                // Client-generated keys are already populated; add explicitly so EF
                // tracks the row as inserted rather than as an update to a missing row.
                _db.Set<PlannedPaymentInstallment>().Add(installment);
                plan.Installments.Add(installment);
                retainedIds.Add(installment.Id);
            }
            installment.Sequence = index + 1;
            installment.Amount = source.Amount;
            installment.DueDateUtc = source.DueDateUtc;
            installment.PlanActivityId = source.PlanActivityId;
            installment.Description = string.IsNullOrWhiteSpace(source.Description) ? null : source.Description.Trim();
            installment.UpdatedBy = actorId.ToString("D");
            installment.UpdatedAt = DateTime.UtcNow;
        }
        foreach (var removed in plan.Installments.Where(item => !retainedIds.Contains(item.Id)).ToList())
            plan.Installments.Remove(removed);

        if (plan.Reservation is not null) plan.Reservation.ReservedAmount = totalAmount;
        AddAudit(plan, "Amended", "Planned Payment financial terms amended before first request.", before,
            new { plan.Reference, plan.Description, plan.TotalAmount }, actorId, actorName);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PlannedPaymentDto>.Failure(Error.Conflict("The Planned Payment changed. Refresh and retry."));
        }
        catch (DbUpdateException)
        {
            return Result<PlannedPaymentDto>.Failure(Error.Conflict("The amendment idempotency key is already in use."));
        }

        var updated = await PlansQuery(tenantId, projectId).FirstOrDefaultAsync(item => item.Id == plan.Id, cancellationToken);
        return updated is null
            ? Result<PlannedPaymentDto>.Failure(Error.NotFound("Planned Payment not found."))
            : Result<PlannedPaymentDto>.Success(await ToDtoAsync(updated, cancellationToken));
    }

    public async Task<Result> ApplyPaymentRequestAsync(
        Guid tenantId,
        Guid projectId,
        Guid installmentId,
        Guid requestId,
        decimal requestAmount,
        Guid actorId,
        string? actorName,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidVersionlessAmount(requestAmount))
            return Result.Failure(Error.Validation("Amount must be positive and use no more than four decimal places."));

        var installment = await _db.Set<PlannedPaymentInstallment>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.Id == installmentId && !item.IsDeleted, cancellationToken);
        if (installment is null)
            return Result.Failure(Error.Validation("The linked Planned Payment installment is not valid for this project."));

        var plan = await _db.Set<PlannedPayment>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.Id == installment.PlannedPaymentId && !item.IsDeleted, cancellationToken);
        if (plan is null)
            return Result.Failure(Error.Validation("The linked Planned Payment was not found."));
        if (plan.State is PlannedPaymentStates.Replaced or PlannedPaymentStates.Cancelled)
            return Result.Failure(Error.Conflict("The linked Planned Payment is no longer active."));

        var activeStatuses = new[]
        {
            PaymentRequestStatuses.Pending,
            PaymentRequestStatuses.Approved
        };
        var alreadyRequested = await (
            from detail in _db.Set<ProjectPaymentRequestDetail>()
            join request in _db.Set<NEXUS.Modules.Approvals.Domain.ApprovalRequest>()
                on detail.ApprovalRequestId equals request.Id
            where detail.TenantId == tenantId && detail.ProjectId == projectId
                && detail.PlannedPaymentInstallmentId == installmentId
                && detail.ApprovalRequestId != requestId
                && activeStatuses.Contains(request.Status)
                && !request.IsDeleted
            select (decimal?)request.Amount).SumAsync(cancellationToken) ?? 0m;
        if (alreadyRequested + requestAmount > installment.Amount)
            return Result.Failure(Error.BusinessValidation(
                "INSTALLMENT_OVERCOMMITTED",
                "The Payment Request exceeds the remaining requestable amount for this installment.",
                "amount"));

        if (plan.State == PlannedPaymentStates.Locked) return Result.Success();

        plan.State = PlannedPaymentStates.Locked;
        plan.FirstRequestAtUtc = DateTime.UtcNow;
        plan.UpdatedBy = actorId.ToString("D");
        plan.UpdatedAt = DateTime.UtcNow;
        AddAudit(plan, "Locked", "Planned Payment financially locked by its first Payment Request.",
            new { plan.State, FirstRequestAtUtc = (DateTime?)null },
            new { plan.State, plan.FirstRequestAtUtc, FirstPaymentRequestId = requestId },
            actorId, actorName);
        return Result.Success();
    }

    public async Task<Result<PlannedPaymentReplacementPreviewDto>> GetReplacementPreviewAsync(
        Guid tenantId,
        Guid projectId,
        Guid plannedPaymentId,
        decimal? newRemainingCommitment,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId, projectId, EditPermission, requireWrite: true);
        if (accessError is not null) return Result<PlannedPaymentReplacementPreviewDto>.Failure(accessError);
        var actorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (actorError is not null) return Result<PlannedPaymentReplacementPreviewDto>.Failure(actorError);
        if (!await _currentManagerLookup.IsCurrentAsync(tenantId, projectId, actorId, cancellationToken))
            return Result<PlannedPaymentReplacementPreviewDto>.Failure(Error.Forbidden(
                "Only the current Project Manager may preview a Planned Payment replacement."));

        var plan = await PlansQuery(tenantId, projectId)
            .FirstOrDefaultAsync(item => item.Id == plannedPaymentId, cancellationToken);
        if (plan is null) return Result<PlannedPaymentReplacementPreviewDto>.Failure(Error.NotFound("Planned Payment not found."));
        if (plan.State is not PlannedPaymentStates.Locked || plan.ReplacedByPlannedPaymentId.HasValue)
            return Result<PlannedPaymentReplacementPreviewDto>.Failure(Error.Conflict(
                "Only an unreplaced, financially Locked Planned Payment can be previewed."));

        var state = await BuildReplacementPreviewStateAsync(
            tenantId, projectId, plan, newRemainingCommitment, cancellationToken);
        return state.IsFailure
            ? Result<PlannedPaymentReplacementPreviewDto>.Failure(state.Error
                ?? Error.Conflict("The Planned Payment replacement preview could not be calculated."))
            : Result<PlannedPaymentReplacementPreviewDto>.Success(state.Value!.Preview);
    }

    public async Task<Result<PlannedPaymentDto>> ReplaceRemainingCommitmentAsync(
        Guid tenantId,
        Guid projectId,
        Guid plannedPaymentId,
        Guid actorId,
        string? actorName,
        ReplaceRemainingCommitmentDto dto,
        CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId, projectId, EditPermission, requireWrite: true);
        if (accessError is not null) return Result<PlannedPaymentDto>.Failure(accessError);
        if (dto is null) return Result<PlannedPaymentDto>.Failure(Error.Validation("A replacement request body is required."));
        if (dto.IdempotencyKey == Guid.Empty) return Result<PlannedPaymentDto>.Failure(Error.Validation("A replacement idempotency key is required."));
        if (string.IsNullOrWhiteSpace(dto.Reason)) return Result<PlannedPaymentDto>.Failure(Error.Validation("A replacement reason is required."));
        if (dto.NewRemainingCommitment <= 0m) return Result<PlannedPaymentDto>.Failure(Error.Validation("The new remaining commitment must be positive."));
        if (dto.Installments is null || dto.Installments.Count == 0) return Result<PlannedPaymentDto>.Failure(Error.Validation("At least one replacement installment is required."));
        if (dto.Installments.Count > 200) return Result<PlannedPaymentDto>.Failure(Error.Validation("A replacement Planned Payment supports at most 200 installments."));

        var installmentTotal = dto.Installments.Sum(item => item.Amount);
        if (decimal.Round(installmentTotal, 4) != decimal.Round(dto.NewRemainingCommitment, 4))
            return Result<PlannedPaymentDto>.Failure(Error.Validation("The sum of replacement installment amounts must equal the new remaining commitment."));

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await LockProjectAsync(tenantId, projectId, cancellationToken))
            return Result<PlannedPaymentDto>.Failure(Error.NotFound("Project not found."));

        var actorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (actorError is not null) return Result<PlannedPaymentDto>.Failure(actorError);
        if (!await _currentManagerLookup.IsCurrentAsync(tenantId, projectId, actorId, cancellationToken))
            return Result<PlannedPaymentDto>.Failure(Error.Forbidden("Only the current Project Manager may replace a Planned Payment commitment."));

        var plan = await PlansQuery(tenantId, projectId, tracked: true)
            .FirstOrDefaultAsync(item => item.Id == plannedPaymentId, cancellationToken);
        if (plan is null) return Result<PlannedPaymentDto>.Failure(Error.NotFound("Planned Payment not found."));
        var replacementFingerprint = ReplacementFingerprint(dto.Reason, NormalizeCurrency(plan.Currency), dto.Installments);
        var idempotencyCheck = await _db.Set<PlannedPayment>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.IdempotencyKey == dto.IdempotencyKey, cancellationToken);
        if (idempotencyCheck is not null)
        {
            if (idempotencyCheck.ReplacesPlannedPaymentId != plan.Id
                || !string.Equals(idempotencyCheck.RegistrationFingerprint, replacementFingerprint, StringComparison.Ordinal))
                return Result<PlannedPaymentDto>.Failure(Error.Conflict("The replacement idempotency key was already used for a different Planned Payment."));
            return Result<PlannedPaymentDto>.Success(await ToDtoAsync(idempotencyCheck, cancellationToken));
        }

        if (plan.ReplacedByPlannedPaymentId.HasValue)
        {
            var existingReplacement = await PlansQuery(tenantId, projectId)
                .FirstOrDefaultAsync(item => item.Id == plan.ReplacedByPlannedPaymentId.Value
                    && item.ReplacesPlannedPaymentId == plan.Id, cancellationToken);
            return existingReplacement is not null
                && string.Equals(existingReplacement.RegistrationFingerprint, replacementFingerprint, StringComparison.Ordinal)
                ? Result<PlannedPaymentDto>.Success(await ToDtoAsync(existingReplacement, cancellationToken))
                : Result<PlannedPaymentDto>.Failure(Error.Conflict("The Planned Payment has already been replaced by a different commitment."));
        }
        if (plan.State is not PlannedPaymentStates.Locked)
            return Result<PlannedPaymentDto>.Failure(Error.Conflict("Only a financially Locked Planned Payment can have its remaining commitment replaced."));

        var stateResult = await BuildReplacementPreviewStateAsync(
            tenantId, projectId, plan, dto.NewRemainingCommitment, cancellationToken);
        if (stateResult.IsFailure)
            return Result<PlannedPaymentDto>.Failure(stateResult.Error
                ?? Error.Conflict("The Planned Payment replacement could not be calculated."));
        var replacementState = stateResult.Value!;
        var preview = replacementState.Preview;
        var totalSettled = preview.HistoricalPaid;
        var eligibleOldUnpaid = preview.CurrentOutstandingCommitment;
        var netChange = preview.NetLifetimeBudgetChange;
        var currency = preview.Currency;
        if (preview.Blockers.Count > 0)
        {
            var blockerDetails = preview.Blockers.Select(item =>
                $"Payment Request {item.PaymentRequestId} ({item.Status}) outstanding {item.OutstandingAmount:N4} {currency}").ToList();
            return Result<PlannedPaymentDto>.Failure(new Error(
                "ACTIVE_PAYMENT_REQUESTS_BLOCK_REPLACEMENT",
                "Active Payment Requests must be resolved before replacing the remaining commitment.",
                409,
                null,
                new Dictionary<string, object?> { ["Blockers"] = blockerDetails }));
        }
        if (!preview.HasSufficientLifetimeBudget)
            return Result<PlannedPaymentDto>.Failure(Error.BusinessValidation(
                "INSUFFICIENT_LIFETIME_BUDGET",
                $"The replacement increases the lifetime reservation by {netChange:N4} {currency} but only {preview.AvailableLifetimeBudgetBefore:N4} {currency} is available.",
                "installments"));

        var outstandingInstallments = plan.Installments
            .Where(item => item.Amount - replacementState.SettledByInstallment.GetValueOrDefault(item.Id) > 0m)
            .OrderBy(item => item.Sequence)
            .ToList();

        var installmentsError = await ValidateReplacementInstallmentsAsync(projectId, dto.Installments, cancellationToken);
        if (installmentsError is not null) return Result<PlannedPaymentDto>.Failure(installmentsError);

        var oldBefore = new
        {
            plan.State,
            plan.TotalAmount,
            plan.Reservation?.ReservedAmount,
            plan.Reservation?.ReleasedAmount,
            EligibleOldUnpaid = eligibleOldUnpaid,
            Installments = plan.Installments.OrderBy(item => item.Sequence)
                .Select(item =>
                {
                    var actualPaid = replacementState.SettledByInstallment.GetValueOrDefault(item.Id);
                    return new { item.Id, item.Sequence, item.Amount, ActualPaid = actualPaid, Outstanding = item.Amount - actualPaid };
                }).ToList()
        };

        plan.State = PlannedPaymentStates.Replaced;
        plan.UpdatedBy = actorId.ToString("D");
        plan.UpdatedAt = DateTime.UtcNow;

        var unpaidReserved = Math.Max(0m, (plan.Reservation?.ReservedAmount ?? 0m) - (plan.Reservation?.ReleasedAmount ?? 0m) - totalSettled);
        if (plan.Reservation is not null)
        {
            plan.Reservation.ReleasedAmount = plan.Reservation.ReleasedAmount + unpaidReserved;
            plan.Reservation.UpdatedBy = actorId.ToString("D");
            plan.Reservation.UpdatedAt = DateTime.UtcNow;
        }

        AddAudit(plan, "Replaced", "Planned Payment replaced with a revised remaining commitment.",
            oldBefore,
            new
            {
                plan.State,
                plan.TotalAmount,
                plan.ReplacedByPlannedPaymentId,
                Reservation = new { plan.Reservation?.ReservedAmount, plan.Reservation?.ReleasedAmount },
                NetChange = netChange,
                Reason = dto.Reason
            },
            actorId, actorName);

        var replacement = new PlannedPayment
        {
            TenantId = tenantId,
            ProjectId = projectId,
            Reference = MakeReference(),
            Description = dto.Reason.Trim(),
            Currency = currency,
            TotalAmount = dto.NewRemainingCommitment,
            State = PlannedPaymentStates.Registered,
            ReplacesPlannedPaymentId = plan.Id,
            IdempotencyKey = dto.IdempotencyKey,
            RegistrationFingerprint = replacementFingerprint,
            RegisteredBy = actorId,
            RegisteredAtUtc = DateTime.UtcNow,
            CreatedBy = actorId.ToString("D")
        };

        for (var index = 0; index < dto.Installments.Count; index++)
        {
            var source = dto.Installments[index];
            replacement.Installments.Add(new PlannedPaymentInstallment
            {
                TenantId = tenantId,
                ProjectId = projectId,
                Sequence = index + 1,
                Amount = source.Amount,
                DueDateUtc = source.DueDateUtc,
                PlanActivityId = source.PlanActivityId,
                Description = string.IsNullOrWhiteSpace(source.Description) ? null : source.Description.Trim(),
                CreatedBy = actorId.ToString("D")
            });
        }

        replacement.Reservation = new PlannedPaymentLifetimeReservation
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ReservedAmount = dto.NewRemainingCommitment,
            ReleasedAmount = 0m,
            CreatedBy = actorId.ToString("D")
        };

        _db.Set<PlannedPayment>().Add(replacement);

        plan.ReplacedByPlannedPaymentId = replacement.Id;

        AddAudit(replacement, "Registered", "Planned Payment registered as replacement for remaining commitment of a replaced obligation.",
            null,
            new { replacement.Reference, replacement.TotalAmount, replacement.Currency, replacement.State, replacement.ReplacesPlannedPaymentId },
            actorId, actorName);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PlannedPaymentDto>.Failure(Error.Conflict("The Planned Payment changed. Refresh and retry."));
        }
        catch (DbUpdateException)
        {
            return Result<PlannedPaymentDto>.Failure(Error.Conflict("The replacement reference or idempotency key is already in use."));
        }

        var created = await PlansQuery(tenantId, projectId).FirstOrDefaultAsync(item => item.Id == replacement.Id, cancellationToken);
        return created is null
            ? Result<PlannedPaymentDto>.Failure(Error.NotFound("Planned Payment not found."))
            : Result<PlannedPaymentDto>.Success(await ToDtoAsync(created, cancellationToken));
    }

    private async Task<Result<ReplacementPreviewState>> BuildReplacementPreviewStateAsync(
        Guid tenantId,
        Guid projectId,
        PlannedPayment plan,
        decimal? requestedNewAmount,
        CancellationToken cancellationToken)
    {
        var installmentIds = plan.Installments.Select(item => item.Id).ToArray();
        var settledByInstallment = await ActualPaidSettlementCalculator.LoadForInstallmentsAsync(
            _db, tenantId, projectId, installmentIds, ignoreQueryFilters: true, cancellationToken);
        foreach (var installment in plan.Installments)
        {
            var settled = settledByInstallment.GetValueOrDefault(installment.Id);
            if (settled < 0m || settled > installment.Amount)
                return Result<ReplacementPreviewState>.Failure(Error.Conflict(
                    "Settlement history does not reconcile to a Planned Payment installment. Reconcile the source records before replacement."));
        }

        var historicalPaid = settledByInstallment.Values.Sum();
        var currentOutstanding = plan.Installments.Sum(item => item.Amount - settledByInstallment.GetValueOrDefault(item.Id));
        if (currentOutstanding <= 0m)
            return Result<ReplacementPreviewState>.Failure(Error.Conflict("The Planned Payment has no remaining commitment to replace."));
        if (decimal.Round(plan.Installments.Sum(item => item.Amount), 4) != decimal.Round(plan.TotalAmount, 4))
            return Result<ReplacementPreviewState>.Failure(Error.Conflict(
                "Planned Payment installments do not reconcile to the registered commitment."));

        var newAmount = requestedNewAmount ?? currentOutstanding;
        if (newAmount <= 0m || decimal.Round(newAmount, 4) != newAmount)
            return Result<ReplacementPreviewState>.Failure(Error.Validation(
                "The new remaining commitment must be positive and use no more than four decimal places."));

        var currency = NormalizeCurrency(plan.Currency);
        if (!IsWellFormedCurrency(currency) || !await IsConfiguredCurrencyAsync(currency, cancellationToken))
            return Result<ReplacementPreviewState>.Failure(Error.Conflict("The project currency is not configured."));
        var reference = await _projectLookup.FindAsync(projectId, cancellationToken);
        if (reference is null || (reference.TenantId != Guid.Empty && reference.TenantId != tenantId))
            return Result<ReplacementPreviewState>.Failure(Error.NotFound("Project not found."));
        if (!reference.CurrentApprovedProjectBudget.HasValue)
            return Result<ReplacementPreviewState>.Failure(Error.Conflict("The project has no approved lifetime budget ceiling."));

        // "Before" is the project's availability today, with the plan still active. "After" is
        // the availability once the replacement commits: the plan's eligible unpaid reservation
        // is released (availability excluding this plan, i.e. the settled/consumed portion stays
        // consumed) and the new commitment is reserved once, so after = before + currentOutstanding - newAmount.
        var currentLifetime = await LoadLifetimeBudgetAsync(tenantId, projectId, currency, null, cancellationToken);
        if (currentLifetime.Error is not null) return Result<ReplacementPreviewState>.Failure(currentLifetime.Error);
        var blockerResult = await LoadReplacementBlockersAsync(tenantId, projectId, installmentIds, currency, cancellationToken);
        if (blockerResult.Error is not null) return Result<ReplacementPreviewState>.Failure(blockerResult.Error);

        var netChange = newAmount - currentOutstanding;
        var preview = new PlannedPaymentReplacementPreviewDto(
            plan.Id,
            currency,
            plan.TotalAmount,
            historicalPaid,
            currentOutstanding,
            newAmount,
            netChange,
            currentLifetime.Available,
            currentLifetime.Available - netChange,
            netChange <= currentLifetime.Available,
            blockerResult.Blockers);
        return Result<ReplacementPreviewState>.Success(new ReplacementPreviewState(preview, settledByInstallment));
    }

    private async Task<(Error? Error, IReadOnlyList<ReplacementPaymentRequestBlockerDto> Blockers)> LoadReplacementBlockersAsync(
        Guid tenantId,
        Guid projectId,
        IReadOnlyCollection<Guid> installmentIds,
        string currency,
        CancellationToken cancellationToken)
    {
        var activeStatuses = new[] { PaymentRequestStatuses.Pending, PaymentRequestStatuses.Approved };
        var requests = await (
            from detail in _db.Set<ProjectPaymentRequestDetail>().IgnoreQueryFilters().AsNoTracking()
            join request in _db.Set<ApprovalRequest>().IgnoreQueryFilters().AsNoTracking()
                on detail.ApprovalRequestId equals request.Id
            where detail.TenantId == tenantId && detail.ProjectId == projectId
                && detail.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                && detail.PlannedPaymentInstallmentId.HasValue
                && installmentIds.Contains(detail.PlannedPaymentInstallmentId.Value)
                && request.TenantId == tenantId && !request.IsDeleted
                && request.Type == PaymentRequestType
                && activeStatuses.Contains(request.Status)
            select new
            {
                RequestId = request.Id,
                request.Status,
                request.Amount,
                request.Currency,
                request.EntityNumber,
                request.Title
            }).ToListAsync(cancellationToken);
        if (requests.Count == 0) return (null, []);

        var requestIds = requests.Select(item => item.RequestId).ToArray();
        var settledRows = await _db.Set<PaymentSettlement>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId
                && !item.IsDeleted && item.ProjectPaymentRequestId.HasValue
                && requestIds.Contains(item.ProjectPaymentRequestId.Value))
            .GroupBy(item => item.ProjectPaymentRequestId!.Value)
            .Select(group => new { RequestId = group.Key, Amount = group.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);
        var settledByRequest = settledRows.ToDictionary(item => item.RequestId, item => item.Amount);

        var reversals = await (
            from detail in _db.Set<ProjectPaymentRequestDetail>().IgnoreQueryFilters().AsNoTracking()
            join reversal in _db.Set<ApprovalRequest>().IgnoreQueryFilters().AsNoTracking()
                on detail.ApprovalRequestId equals reversal.Id
            where detail.TenantId == tenantId && detail.ProjectId == projectId
                && detail.RequestKind == ProjectPaymentRequestKinds.ReversalRequest
                && detail.ReversesApprovalRequestId.HasValue
                && requestIds.Contains(detail.ReversesApprovalRequestId.Value)
                && reversal.TenantId == tenantId && !reversal.IsDeleted
                && (reversal.Status == PaymentRequestStatuses.Approved || reversal.Status == PaymentRequestStatuses.Pending)
            select new
            {
                OriginalRequestId = detail.ReversesApprovalRequestId!.Value,
                reversal.Status,
                reversal.Amount,
                reversal.Currency,
                reversal.EntityType,
                reversal.EntityId
            }).ToListAsync(cancellationToken);

        var blockers = new List<ReplacementPaymentRequestBlockerDto>();
        foreach (var request in requests)
        {
            if (request.Amount is not > 0m || decimal.Round(request.Amount.Value, 4) != request.Amount.Value
                || !string.Equals(NormalizeCurrency(request.Currency), currency, StringComparison.Ordinal))
                return (Error.Conflict("An active Payment Request has an invalid amount or currency."), []);

            var requestReversals = reversals.Where(item => item.OriginalRequestId == request.RequestId).ToArray();
            decimal approvedRelease = 0m;
            decimal pendingRelease = 0m;
            foreach (var reversal in requestReversals)
            {
                if (reversal.Amount is not > 0m || decimal.Round(reversal.Amount.Value, 4) != reversal.Amount.Value
                    || reversal.EntityType != "Project" || reversal.EntityId != projectId
                    || !string.Equals(NormalizeCurrency(reversal.Currency), currency, StringComparison.Ordinal))
                    return (Error.Conflict("An approved or pending Reversal Request is inconsistent with its Payment Request."), []);
                if (reversal.Status == PaymentRequestStatuses.Approved) approvedRelease += reversal.Amount.Value;
                else pendingRelease += reversal.Amount.Value;
            }

            var settled = settledByRequest.GetValueOrDefault(request.RequestId);
            if (request.Status == PaymentRequestStatuses.Pending && settled > 0m)
                return (Error.Conflict("A pending Payment Request already has settlement history."), []);
            if (approvedRelease + settled > request.Amount.Value)
                return (Error.Conflict("Approved reversals and settlements exceed an active Payment Request."), []);

            var outstanding = request.Amount.Value - approvedRelease - settled;
            if (request.Status == PaymentRequestStatuses.Pending || outstanding > 0m)
            {
                blockers.Add(new ReplacementPaymentRequestBlockerDto(
                    request.RequestId,
                    request.EntityNumber ?? request.Title,
                    request.Status,
                    outstanding,
                    approvedRelease,
                    pendingRelease));
            }
        }

        return (null, blockers);
    }

    private Error? ValidateAccess(Guid tenantId, Guid projectId, string permission, bool requireWrite)
    {
        var guardError = FinancePermissionGuard.Require(_httpContextAccessor, permission);
        if (guardError is not null) return guardError;
        var current = _management?.Current;
        if (tenantId == Guid.Empty || projectId == Guid.Empty || current is null
            || current.TenantId != tenantId || !current.Scope.ProjectIds.Contains(projectId))
            return Error.NotFound("Project not found.");
        if (current.Scope.OrganizationIds.Count > 0
            && current.Scope.ProjectOrganizationIds.TryGetValue(projectId, out var organizationId)
            && !current.Scope.OrganizationIds.Contains(organizationId))
            return Error.NotFound("Project not found.");
        if (requireWrite && current.EffectiveReadOnly)
            return Error.Forbidden("This operation is unavailable in a read-only management context.");
        return null;
    }

    private IQueryable<PlannedPayment> PlansQuery(Guid tenantId, Guid projectId, bool tracked = false)
    {
        var query = _db.Set<PlannedPayment>()
            .Include(item => item.Installments)
            .Include(item => item.Reservation)
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted);
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<PlannedPaymentDto> ToDtoAsync(PlannedPayment plan, CancellationToken cancellationToken)
    {
        var installmentIds = plan.Installments.Select(item => item.Id).ToList();
        var requestedByInstallment = await LoadRequestedByInstallmentAsync(installmentIds, cancellationToken);
        var settledByInstallment = await LoadSettledByInstallmentAsync(plan.TenantId, plan.ProjectId, installmentIds, cancellationToken);
        var milestones = new Dictionary<Guid, string?>();
        foreach (var milestoneId in plan.Installments.Where(item => item.PlanActivityId.HasValue)
                     .Select(item => item.PlanActivityId!.Value).Distinct())
            milestones[milestoneId] = await _milestoneLookup.FindActiveMilestoneNameForProjectAsync(plan.ProjectId, milestoneId, cancellationToken);

        var installments = plan.Installments.OrderBy(item => item.Sequence).Select(item =>
        {
            var requested = requestedByInstallment.GetValueOrDefault(item.Id);
            var settled = settledByInstallment.GetValueOrDefault(item.Id);
            var actualPaid = settled;
            var outstandingBalance = Math.Max(0m, item.Amount - actualPaid);
            return new PlannedPaymentInstallmentDto(
                item.Id,
                item.Sequence,
                item.Amount,
                item.DueDateUtc,
                item.PlanActivityId,
                item.PlanActivityId.HasValue ? milestones.GetValueOrDefault(item.PlanActivityId.Value) : null,
                requested,
                settled,
                actualPaid,
                outstandingBalance,
                Math.Max(0m, item.Amount - Math.Max(requested, settled)),
                item.RowVersion);
        }).ToList();

        var planSettled = installments.Sum(item => item.SettledAmount);
        var reserved = plan.Reservation?.ReservedAmount ?? 0m;
        var released = plan.Reservation?.ReleasedAmount ?? 0m;
        return new PlannedPaymentDto(
            plan.Id,
            plan.ProjectId,
            plan.Reference,
            plan.Description,
            plan.Currency,
            plan.TotalAmount,
            plan.State,
            plan.IsLocked,
            plan.PaymentStatus,
            reserved,
            released,
            Math.Max(0m, reserved - released - planSettled),
            planSettled,
            plan.RegisteredAtUtc,
            plan.FirstRequestAtUtc,
            plan.ReplacesPlannedPaymentId,
            plan.ReplacedByPlannedPaymentId,
            installments,
            plan.RowVersion,
            !plan.IsLocked && FinancePermissionGuard.HasCurrentPermission(_httpContextAccessor, EditPermission)
                && _management?.Current?.EffectiveReadOnly != true);
    }

    private async Task<Dictionary<Guid, decimal>> LoadRequestedByInstallmentAsync(
        IReadOnlyCollection<Guid> installmentIds, CancellationToken cancellationToken)
    {
        if (installmentIds.Count == 0) return new Dictionary<Guid, decimal>();
        var statuses = new[] { PaymentRequestStatuses.Pending, PaymentRequestStatuses.Approved };
        var rows = await (
            from detail in _db.Set<ProjectPaymentRequestDetail>()
            join request in _db.Set<NEXUS.Modules.Approvals.Domain.ApprovalRequest>()
                on detail.ApprovalRequestId equals request.Id
            where detail.PlannedPaymentInstallmentId.HasValue
                && installmentIds.Contains(detail.PlannedPaymentInstallmentId.Value)
                && statuses.Contains(request.Status)
                && !request.IsDeleted
            group request by detail.PlannedPaymentInstallmentId!.Value into grouped
            select new { InstallmentId = grouped.Key, Amount = grouped.Sum(item => (decimal?)item.Amount) ?? 0m }
        ).ToListAsync(cancellationToken);
        return rows.ToDictionary(item => item.InstallmentId, item => item.Amount);
    }

    private async Task<Dictionary<Guid, decimal>> LoadSettledByInstallmentAsync(
        Guid tenantId,
        Guid projectId,
        IReadOnlyCollection<Guid> installmentIds,
        CancellationToken cancellationToken)
    {
        var values = await ActualPaidSettlementCalculator.LoadForInstallmentsAsync(
            _db, tenantId, projectId, installmentIds, ignoreQueryFilters: true, cancellationToken);
        return values.ToDictionary(item => item.Key, item => item.Value);
    }

    private async Task<(Error? Error, decimal Consumed, decimal Reserved, decimal Available)> LoadLifetimeBudgetAsync(
        Guid tenantId, Guid projectId, string currency, Guid? excludePlanId, CancellationToken cancellationToken)
    {
        var facts = await ActualPaidSettlementCalculator.LoadForProjectAsync(_db, tenantId, projectId, cancellationToken: cancellationToken);
        if (ActualPaidSettlementCalculator.HasUnresolvedCurrency(facts)
            || ActualPaidSettlementCalculator.HasForeignCurrency(facts, currency))
            return (Error.Conflict($"A confirmed settlement uses a currency other than the project currency {currency}."), 0m, 0m, 0m);
        var consumed = ActualPaidSettlementCalculator.SumActualPaid(facts, currency);

        var settledByPlan = await LoadSettledByPlanAsync(facts, tenantId, projectId, cancellationToken);
        var reservations = await (
            from plan in _db.Set<PlannedPayment>().AsNoTracking()
            join reservation in _db.Set<PlannedPaymentLifetimeReservation>().AsNoTracking()
                on new { plan.TenantId, plan.ProjectId, PlanId = plan.Id }
                equals new { reservation.TenantId, reservation.ProjectId, PlanId = reservation.PlannedPaymentId }
            where plan.TenantId == tenantId && plan.ProjectId == projectId && !plan.IsDeleted
                && plan.State != PlannedPaymentStates.Cancelled && plan.State != PlannedPaymentStates.Replaced
                && (!excludePlanId.HasValue || plan.Id != excludePlanId.Value)
            select new { plan.Id, reservation.ReservedAmount, reservation.ReleasedAmount }
        ).ToListAsync(cancellationToken);
        var reserved = reservations.Sum(item =>
            Math.Max(0m, item.ReservedAmount - item.ReleasedAmount - settledByPlan.GetValueOrDefault(item.Id)));
        var reference = await _projectLookup.FindAsync(projectId, cancellationToken);
        var ceiling = reference?.CurrentApprovedProjectBudget;
        var available = ceiling.HasValue ? Math.Max(0m, ceiling.Value - consumed - reserved) : 0m;
        return (null, consumed, reserved, available);
    }

    private async Task<Dictionary<Guid, decimal>> LoadSettledByPlanAsync(
        IReadOnlyList<ActualPaidSettlementCalculator.SettlementFact> facts,
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var requestIds = facts.Where(item => item.ProjectPaymentRequestId.HasValue)
            .Select(item => item.ProjectPaymentRequestId!.Value).Distinct().ToList();
        if (requestIds.Count == 0) return new Dictionary<Guid, decimal>();
        var installmentPlans = await (
            from detail in _db.Set<ProjectPaymentRequestDetail>().IgnoreQueryFilters().AsNoTracking()
            join installment in _db.Set<PlannedPaymentInstallment>().IgnoreQueryFilters().AsNoTracking()
                on new { detail.TenantId, detail.ProjectId, InstallmentId = detail.PlannedPaymentInstallmentId }
                equals new { installment.TenantId, installment.ProjectId, InstallmentId = (Guid?)installment.Id }
            where detail.TenantId == tenantId && detail.ProjectId == projectId
                && requestIds.Contains(detail.ApprovalRequestId)
            select new { detail.ApprovalRequestId, installment.PlannedPaymentId }
        ).ToListAsync(cancellationToken);
        var map = installmentPlans.ToDictionary(item => item.ApprovalRequestId, item => item.PlannedPaymentId);
        var result = new Dictionary<Guid, decimal>();
        foreach (var fact in facts)
        {
            if (!fact.ProjectPaymentRequestId.HasValue
                || !map.TryGetValue(fact.ProjectPaymentRequestId.Value, out var planId)) continue;
            result[planId] = result.GetValueOrDefault(planId) + fact.Amount;
        }
        return result;
    }

    private async Task<Error?> ValidateInstallmentsAsync(
        Guid projectId,
        IReadOnlyList<CreatePlannedPaymentInstallmentDto> installments,
        CancellationToken cancellationToken)
    {
        if (installments is null || installments.Count == 0)
            return Error.Validation("At least one installment is required.");
        if (installments.Count > 200)
            return Error.Validation("A Planned Payment supports at most 200 installments.");
        var descriptions = new List<string?>();
        foreach (var installment in installments)
        {
            if (!IsValidVersionlessAmount(installment.Amount))
                return Error.Validation("Every installment amount must be positive and use no more than four decimal places.", "installments");
            if (!installment.DueDateUtc.HasValue)
                return Error.Validation("Every installment requires a due date.", "installments");
            if (installment.DueDateUtc.Value.Kind != DateTimeKind.Utc)
                return Error.Validation("Installment due dates must be UTC.", "installments");
            if (installment.PlanActivityId.HasValue
                && !await _milestoneLookup.IsActiveMilestoneForProjectAsync(projectId, installment.PlanActivityId.Value, cancellationToken))
                return Error.Validation("A selected milestone is not active in a working plan for this project.", "installments");
            var description = string.IsNullOrWhiteSpace(installment.Description) ? null : installment.Description.Trim();
            if (description?.Length > 2000) return Error.Validation("Installment description is too long.", "installments");
            descriptions.Add(description);
        }
        return null;
    }

    private async Task<bool> LockProjectAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        if (_db.Database.ProviderName != "Microsoft.EntityFrameworkCore.SqlServer") return true;
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT [Id] FROM [Projects] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = @projectId AND [TenantId] = @tenantId AND [IsDeleted] = 0";
        AddParameter(command, "@projectId", projectId);
        AddParameter(command, "@tenantId", tenantId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static void AddParameter(DbCommand command, string name, Guid value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private async Task<Error?> ValidateActiveActorAsync(Guid tenantId, Guid actorId, CancellationToken cancellationToken) =>
        actorId == Guid.Empty || !await _db.Set<User>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.Id == actorId && item.TenantId == tenantId && item.IsActive && !item.IsDeleted, cancellationToken)
            ? Error.Forbidden("An active tenant user is required.")
            : null;

    private async Task<bool> IsConfiguredCurrencyAsync(string currency, CancellationToken cancellationToken) =>
        await _db.Set<Currency>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.Code == currency && !item.IsDeleted, cancellationToken);

    private void AddAudit(PlannedPayment plan, string action, string description, object? before, object? after,
        Guid actorId, string? actorName)
    {
        _db.Set<AuditEntry>().Add(new AuditEntry
        {
            TenantId = plan.TenantId,
            ProjectId = plan.ProjectId,
            EntityType = EntityType,
            EntityId = plan.Id,
            Action = action,
            UserId = actorId,
            UserName = string.IsNullOrWhiteSpace(actorName) ? null : actorName.Trim(),
            Timestamp = DateTime.UtcNow,
            Description = description,
            Changes = JsonSerializer.Serialize(new { before, after }),
            OriginalValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = after is null ? null : JsonSerializer.Serialize(after),
            ChangedFields = "reference,description,totalAmount,state,installments",
            Source = "User",
            CreatedBy = actorId.ToString("D")
        });
    }

    private void AddComponentAudit(PlannedPayment plan, string entityType, Guid entityId, string action,
        string description, object after, string changedFields, Guid actorId, string? actorName)
    {
        _db.Set<AuditEntry>().Add(new AuditEntry
        {
            TenantId = plan.TenantId,
            ProjectId = plan.ProjectId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = actorId,
            UserName = string.IsNullOrWhiteSpace(actorName) ? null : actorName.Trim(),
            Timestamp = DateTime.UtcNow,
            Description = description,
            Changes = JsonSerializer.Serialize(new { before = (object?)null, after }),
            NewValues = JsonSerializer.Serialize(after),
            ChangedFields = changedFields,
            Source = "User",
            CreatedBy = actorId.ToString("D")
        });
    }

    private static string RegistrationFingerprint<T>(
        string description,
        string currency,
        IReadOnlyList<T> installments)
        where T : class
    {
        var canonical = JsonSerializer.Serialize(new
        {
            description = (description ?? string.Empty).Trim(),
            currency,
            installments = installments.Select(item => new
            {
                amount = GetAmount(item),
                dueDateUtc = GetDueDate(item),
                planActivityId = GetMilestone(item),
                description = string.IsNullOrWhiteSpace(GetDescription(item)) ? null : GetDescription(item)!.Trim()
            }).ToList()
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static decimal GetAmount(object item) => item switch
    {
        CreatePlannedPaymentInstallmentDto create => create.Amount,
        UpdatePlannedPaymentInstallmentDto update => update.Amount,
        _ => 0m
    };

    private static DateTime? GetDueDate(object item) => item switch
    {
        CreatePlannedPaymentInstallmentDto create => create.DueDateUtc,
        UpdatePlannedPaymentInstallmentDto update => update.DueDateUtc,
        _ => null
    };

    private static Guid? GetMilestone(object item) => item switch
    {
        CreatePlannedPaymentInstallmentDto create => create.PlanActivityId,
        UpdatePlannedPaymentInstallmentDto update => update.PlanActivityId,
        _ => null
    };

    private static string? GetDescription(object item) => item switch
    {
        CreatePlannedPaymentInstallmentDto create => create.Description,
        UpdatePlannedPaymentInstallmentDto update => update.Description,
        _ => null
    };

    private static bool IsValidVersionlessAmount(decimal amount) =>
        amount > 0m && amount < 1_000_000_000_000_000m && decimal.Round(amount, 4) == amount;

    private static bool MatchesVersion(byte[] current, byte[] expected) =>
        current is { Length: > 0 } && expected is { Length: > 0 } && current.SequenceEqual(expected);

    private static string NormalizeCurrency(string? currency) => (currency ?? string.Empty).Trim().ToUpperInvariant();
    private static bool IsWellFormedCurrency(string currency) =>
        currency.Length == 3 && currency.All(character => character is >= 'A' and <= 'Z');

    private static string MakeReference() => $"PP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40].ToUpperInvariant();

    private async Task<Error?> ValidateReplacementInstallmentsAsync(
        Guid projectId,
        IReadOnlyList<ReplacePlannedPaymentInstallmentDto> installments,
        CancellationToken cancellationToken)
    {
        if (installments is null || installments.Count == 0)
            return Error.Validation("At least one replacement installment is required.", "installments");
        if (installments.Count > 200)
            return Error.Validation("A replacement Planned Payment supports at most 200 installments.", "installments");
        foreach (var installment in installments)
        {
            if (!IsValidVersionlessAmount(installment.Amount))
                return Error.Validation("Every replacement installment amount must be positive and use no more than four decimal places.", "installments");
            if (!installment.DueDateUtc.HasValue)
                return Error.Validation("Every replacement installment requires a due date.", "installments");
            if (installment.DueDateUtc.Value.Kind != DateTimeKind.Utc)
                return Error.Validation("Replacement installment due dates must be UTC.", "installments");
            if (installment.PlanActivityId.HasValue
                && !await _milestoneLookup.IsActiveMilestoneForProjectAsync(projectId, installment.PlanActivityId.Value, cancellationToken))
                return Error.Validation("A selected milestone is not active in a working plan for this project.", "installments");
            var description = string.IsNullOrWhiteSpace(installment.Description) ? null : installment.Description.Trim();
            if (description?.Length > 2000) return Error.Validation("Replacement installment description is too long.", "installments");
        }
        return null;
    }

    private static string ReplacementFingerprint(
        string reason,
        string currency,
        IReadOnlyList<ReplacePlannedPaymentInstallmentDto> installments)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            reason = (reason ?? string.Empty).Trim(),
            currency,
            installments = installments.Select(item => new
            {
                amount = item.Amount,
                dueDateUtc = item.DueDateUtc,
                planActivityId = item.PlanActivityId,
                description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim()
            }).ToList()
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record ReplacementPreviewState(
        PlannedPaymentReplacementPreviewDto Preview,
        IReadOnlyDictionary<Guid, decimal> SettledByInstallment);
}
