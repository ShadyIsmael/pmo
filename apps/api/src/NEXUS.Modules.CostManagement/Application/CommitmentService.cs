using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.CostManagement.Application;

public sealed class CommitmentService : ICommitmentService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IProjectReferenceLookup _projectLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor? _managementContext;

    public CommitmentService(DbContext context, IAuditService auditService, IProjectReferenceLookup projectLookup, IHttpContextAccessor httpContextAccessor, IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _auditService = auditService;
        _projectLookup = projectLookup;
        _httpContextAccessor = httpContextAccessor;
        _managementContext = managementContext;
    }

    public async Task<Result<PurchaseCommitmentDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "Commitment.View");
        if (accessError is not null) return Result<PurchaseCommitmentDto>.Failure(accessError);
        var commitment = await ApplyManagementScope(_context.Set<PurchaseCommitment>().AsNoTracking()
            .Where(item => item.TenantId == CurrentTenant()))
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return commitment is null
            ? Result<PurchaseCommitmentDto>.Failure(Error.NotFound("Commitment not found"))
            : Result<PurchaseCommitmentDto>.Success(commitment.ToDto());
    }

    public async Task<PagedResult<PurchaseCommitmentDto>> GetPagedAsync(
        int page, int pageSize, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        if (FinancePermissionGuard.Require(_httpContextAccessor, "Commitment.View") is not null)
            return new PagedResult<PurchaseCommitmentDto>(Array.Empty<PurchaseCommitmentDto>(), page, pageSize, 0);
        var query = ApplyManagementScope(_context.Set<PurchaseCommitment>().AsNoTracking()
            .Where(c => c.TenantId == CurrentTenant() && !c.IsDeleted));
        if (projectId.HasValue) query = query.Where(c => c.ProjectId == projectId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(c => c.CommitmentDate)
            .ThenBy(c => c.PurchaseOrderNumber)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<PurchaseCommitmentDto>(items.Select(c => c.ToDto()).ToList(), page, pageSize, total);
    }

    public async Task<Result<PurchaseCommitmentDto>> CreateAsync(
        CreateCommitmentDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "Commitment.Create");
        if (accessError is not null) return Result<PurchaseCommitmentDto>.Failure(accessError);
        if (!CanAccessProject(dto.ProjectId))
            return Result<PurchaseCommitmentDto>.Failure(Error.Forbidden("The commitment is outside the actor's current project scope."));
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        if (dto.ProjectId == Guid.Empty)
            return Result<PurchaseCommitmentDto>.Failure(Error.Validation("ProjectId is required"));

        if (dto.Amount <= 0)
            return Result<PurchaseCommitmentDto>.Failure(Error.Validation("Amount must be greater than zero"));

        if (string.IsNullOrWhiteSpace(dto.PurchaseOrderNumber))
            return Result<PurchaseCommitmentDto>.Failure(Error.Validation("Purchase order number is required"));

        var projectRef = await _projectLookup.FindAsync(dto.ProjectId, cancellationToken);
        if (projectRef is null)
            return Result<PurchaseCommitmentDto>.Failure(Error.NotFound("Project not found"));

        if (dto.BudgetId.HasValue && !await _context.Set<Budget>().AnyAsync(
                budget => budget.Id == dto.BudgetId.Value && budget.TenantId == CurrentTenant() && budget.ProjectId == dto.ProjectId && !budget.IsDeleted,
                cancellationToken))
            return Result<PurchaseCommitmentDto>.Failure(Error.NotFound("Budget not found for this project"));

        var currency = projectRef.Currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
            return Result<PurchaseCommitmentDto>.Failure(Error.Validation("The project must have a valid configured currency before a commitment can be created."));

        var capacityError = await BudgetAuthority.ValidateCommitmentAsync(_context, _projectLookup, dto.ProjectId, null, dto.Amount, cancellationToken);
        if (capacityError is not null) return Result<PurchaseCommitmentDto>.Failure(capacityError);
        var commitment = new PurchaseCommitment
        {
            TenantId = CurrentTenant(),
            CreatedBy = userId,
            ProjectId = dto.ProjectId,
            BudgetId = dto.BudgetId,
            PurchaseOrderNumber = dto.PurchaseOrderNumber.Trim(),
            Description = dto.Description?.Trim(),
            VendorName = dto.VendorName?.Trim(),
            Amount = dto.Amount,
            Currency = currency,
            CommitmentDate = dto.CommitmentDate,
            ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
            Status = "Open",
            RowVersion = NewVersion()
        };

        _context.Set<PurchaseCommitment>().Add(commitment);
        await AuditAsync("PurchaseCommitment", commitment.Id, "Create", userId, null, dto, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<PurchaseCommitmentDto>.Success(commitment.ToDto());
    }

    private IQueryable<PurchaseCommitment> ApplyManagementScope(IQueryable<PurchaseCommitment> query)
    {
        var management = _managementContext?.Current;
        if (management is null || management.TenantId != CurrentTenant() || management.Scope is null)
            return query.Where(_ => false);
        var projectIds = management.Scope.ProjectIds.ToArray();
        return query.Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value));
    }

    private bool CanAccessProject(Guid projectId)
    {
        var management = _managementContext?.Current;
        return management is not null && management.TenantId == CurrentTenant() && management.Scope is not null
            && management.Scope.ProjectIds.Contains(projectId);
    }

    public async Task<Result<PurchaseCommitmentDto>> UpdateAsync(
        Guid id, UpdateCommitmentDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "Commitment.Edit");
        if (accessError is not null) return Result<PurchaseCommitmentDto>.Failure(accessError);
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var commitment = await ApplyManagementScope(_context.Set<PurchaseCommitment>().Where(item => item.TenantId == CurrentTenant()))
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
        if (commitment is null)
            return Result<PurchaseCommitmentDto>.Failure(Error.NotFound("Commitment not found"));

        if (!IsEditableStatus(commitment.Status))
            return Result<PurchaseCommitmentDto>.Failure(Error.Validation("Only open or active commitments can be edited"));

        if (!MatchesVersion(commitment.RowVersion, dto.RowVersion))
            return Result<PurchaseCommitmentDto>.Failure(Error.Conflict("Commitment was changed by another user"));

        if (dto.Amount <= 0)
            return Result<PurchaseCommitmentDto>.Failure(Error.Validation("Amount must be greater than zero"));

        if (string.IsNullOrWhiteSpace(dto.PurchaseOrderNumber))
            return Result<PurchaseCommitmentDto>.Failure(Error.Validation("Purchase order number is required"));

        if (dto.Amount > commitment.Amount)
        {
            var capacityError = await BudgetAuthority.ValidateCommitmentAsync(_context, _projectLookup, commitment.ProjectId!.Value, id, dto.Amount, cancellationToken);
            if (capacityError is not null) return Result<PurchaseCommitmentDto>.Failure(capacityError);
        }
        var original = JsonSerializer.Serialize(new { commitment.Amount, commitment.PurchaseOrderNumber, commitment.VendorName });

        commitment.PurchaseOrderNumber = dto.PurchaseOrderNumber.Trim();
        commitment.Description = dto.Description?.Trim();
        commitment.VendorName = dto.VendorName?.Trim();
        commitment.Amount = dto.Amount;
        commitment.CommitmentDate = dto.CommitmentDate;
        commitment.ExpectedDeliveryDate = dto.ExpectedDeliveryDate;
        commitment.UpdatedBy = userId;
        commitment.UpdatedAt = DateTime.UtcNow;
        commitment.RowVersion = NewVersion();

        await AuditAsync("PurchaseCommitment", commitment.Id, "Update", userId, original, dto, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<PurchaseCommitmentDto>.Success(commitment.ToDto());
    }

    public async Task<Result> CloseAsync(
        Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "Commitment.Close");
        if (accessError is not null) return Result.Failure(accessError);
        var commitment = await ApplyManagementScope(_context.Set<PurchaseCommitment>().Where(item => item.TenantId == CurrentTenant()))
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
        if (commitment is null)
            return Result.Failure(Error.NotFound("Commitment not found"));

        if (!IsTransitionAllowed(commitment.Status, "Closed"))
            return Result.Failure(Error.Validation($"Cannot close a commitment with status '{commitment.Status}'"));

        if (!MatchesVersion(commitment.RowVersion, rowVersion))
            return Result.Failure(Error.Conflict("Commitment was changed by another user"));

        var original = commitment.Status;
        commitment.Status = "Closed";
        commitment.UpdatedBy = userId;
        commitment.UpdatedAt = DateTime.UtcNow;
        commitment.RowVersion = NewVersion();

        await AuditAsync("PurchaseCommitment", commitment.Id, "Close", userId, original, "Closed", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CancelAsync(
        Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "Commitment.Cancel");
        if (accessError is not null) return Result.Failure(accessError);
        var commitment = await ApplyManagementScope(_context.Set<PurchaseCommitment>().Where(item => item.TenantId == CurrentTenant()))
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
        if (commitment is null)
            return Result.Failure(Error.NotFound("Commitment not found"));

        if (!IsTransitionAllowed(commitment.Status, "Cancelled"))
            return Result.Failure(Error.Validation($"Cannot cancel a commitment with status '{commitment.Status}'"));

        if (!MatchesVersion(commitment.RowVersion, rowVersion))
            return Result.Failure(Error.Conflict("Commitment was changed by another user"));

        var original = commitment.Status;
        commitment.Status = "Cancelled";
        commitment.UpdatedBy = userId;
        commitment.UpdatedAt = DateTime.UtcNow;
        commitment.RowVersion = NewVersion();

        await AuditAsync("PurchaseCommitment", commitment.Id, "Cancel", userId, original, "Cancelled", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(
        Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "Commitment.Delete");
        if (accessError is not null) return Result.Failure(accessError);
        var commitment = await ApplyManagementScope(_context.Set<PurchaseCommitment>().Where(item => item.TenantId == CurrentTenant()))
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
        if (commitment is null)
            return Result.Failure(Error.NotFound("Commitment not found"));

        if (!IsEditableStatus(commitment.Status))
            return Result.Failure(Error.Validation("Only open or active commitments can be deleted"));

        if (!MatchesVersion(commitment.RowVersion, rowVersion))
            return Result.Failure(Error.Conflict("Commitment was changed by another user"));

        var original = JsonSerializer.Serialize(commitment.ToDto());
        await AuditAsync("PurchaseCommitment", commitment.Id, "Delete", userId, original, null, cancellationToken);
        commitment.IsDeleted = true;
        commitment.UpdatedBy = userId;
        commitment.UpdatedAt = DateTime.UtcNow;
        commitment.RowVersion = NewVersion();

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static bool IsEditableStatus(string status) =>
        status is "Open" or "Active";

    private static bool IsTransitionAllowed(string current, string target) => (current, target) switch
    {
        ("Open", "Closed") => true,
        ("Open", "Cancelled") => true,
        ("Active", "Closed") => true,
        ("Active", "Cancelled") => true,
        _ => false
    };

    private Guid CurrentTenant() =>
        Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value, out var tenant)
            ? tenant : Guid.Empty;

    private static byte[] NewVersion() => Guid.NewGuid().ToByteArray();

    private static bool MatchesVersion(byte[] current, byte[] requested) =>
        requested.Length == 0 || current.Length == 0 || current.SequenceEqual(requested);

    private async Task AuditAsync(string type, Guid id, string action, string userId,
        string? original, object? values, CancellationToken cancellationToken)
    {
        var actorName = _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? _httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value;
        var result = await _auditService.AppendAsync(FinancialAuditEntryFactory.ForCurrentRecord(
            _context, type, id, action, userId, actorName, original, values), cancellationToken);
        if (result.IsFailure) throw new InvalidOperationException(result.Error?.Message ?? "Financial audit append failed.");
    }
}
