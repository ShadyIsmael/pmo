using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;

namespace NEXUS.Modules.Portfolio.Application;

public sealed class ManagerHandoverService : IManagerHandoverService
{
    private readonly DbContext _db;
    private readonly INotificationService _notifications;
    private readonly IUserEffectivePermissionService _effectivePermissions;
    private readonly IResourceReferenceLookup? _resourceLookup;
    private readonly IManagementContextAccessor _management;

    public ManagerHandoverService(DbContext db, INotificationService notifications, IUserEffectivePermissionService effectivePermissions,
        IManagementContextAccessor management, IResourceReferenceLookup? resourceLookup = null)
    {
        _db = db;
        _notifications = notifications;
        _effectivePermissions = effectivePermissions;
        _management = management;
        _resourceLookup = resourceLookup;
    }

    public async Task<Result<IReadOnlyList<ManagerHandoverManagerDto>>> GetEligibleProgramManagersAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var program = await _db.Set<Program>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (program is null || !await IsSameTenantAsync(userId, program.TenantId, ct)) return Result<IReadOnlyList<ManagerHandoverManagerDto>>.Failure(Error.NotFound("Program not found"));
        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(userId, program.TenantId, ct);
        var canManagerInitiate = permissions.Contains("Handover.Manage") && program.ProgramManagerId == userId;
        var canAdminInitiate = permissions.Contains("Handover.AdminInitiate");
        if ((!canManagerInitiate && !canAdminInitiate)
            || !HasProgramScope(userId, program.TenantId, program, requireWrite: true))
            return Result<IReadOnlyList<ManagerHandoverManagerDto>>.Failure(Error.Forbidden("You do not have permission or scope to initiate handovers for this Program"));
        return Result<IReadOnlyList<ManagerHandoverManagerDto>>.Success(await GetEligibleManagersAsync(
            program.TenantId, program.ProgramManagerId, ct, "Program.View", "ProgramManager"));
    }

    public async Task<Result<IReadOnlyList<ManagerHandoverManagerDto>>> GetEligiblePortfolioManagersAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var portfolio = await _db.Set<PortfolioEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (portfolio is null || !await IsSameTenantAsync(userId, portfolio.TenantId, ct)) return Result<IReadOnlyList<ManagerHandoverManagerDto>>.Failure(Error.NotFound("Portfolio not found"));
        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(userId, portfolio.TenantId, ct);
        var canManagerInitiate = permissions.Contains("Handover.Manage") && portfolio.OwnerId == userId;
        var canAdminInitiate = permissions.Contains("Handover.AdminInitiate");
        if ((!canManagerInitiate && !canAdminInitiate)
            || !HasPortfolioScope(userId, portfolio.TenantId, portfolio.Id, requireWrite: true))
            return Result<IReadOnlyList<ManagerHandoverManagerDto>>.Failure(Error.Forbidden("You do not have permission or scope to initiate handovers for this Portfolio"));
        return Result<IReadOnlyList<ManagerHandoverManagerDto>>.Success(await GetEligibleManagersAsync(
            portfolio.TenantId, portfolio.OwnerId, ct, "Portfolio.View", "PortfolioManager"));
    }

    private async Task<IReadOnlyList<ManagerHandoverManagerDto>> GetEligibleManagersAsync(
        Guid tenantId, Guid? currentManagerId, CancellationToken ct, string requiredPermission, string managerType)
    {
        var users = await _db.Set<User>().AsNoTracking()
            .Where(user => user.TenantId == tenantId && user.IsActive && !user.IsDeleted
                && (!currentManagerId.HasValue || user.Id != currentManagerId.Value)
                && _db.Set<UserRole>().Any(assignment => assignment.UserId == user.Id
                    && assignment.TenantId == tenantId && !assignment.IsDeleted
                    && _db.Set<Role>().Any(role => role.Id == assignment.RoleId
                        && role.TenantId == tenantId && !role.IsDeleted && role.Category == "Management")))
            .OrderBy(user => user.FirstName).ThenBy(user => user.LastName)
            .ToListAsync(ct);

        var approvers = new List<User>();
        foreach (var user in users)
        {
            var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(user.Id, tenantId, ct);
            if (permissions.Contains("Handover.Approve") && permissions.Contains(requiredPermission)) approvers.Add(user);
        }

        var eligibleIds = approvers.Select(user => user.Id).ToArray();
        var resourceTitles = eligibleIds.Length == 0 || _resourceLookup is null
            ? new Dictionary<Guid, string?>()
            : (await _resourceLookup.GetDetailsByUserIdsAsync(eligibleIds, ct))
                .Where(pair => pair.Value.IsActive)
                .ToDictionary(pair => pair.Key, pair => pair.Value.JobTitle);

        return approvers.Select(user => new ManagerHandoverManagerDto
        {
            Id = user.Id,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email,
            JobTitle = resourceTitles.GetValueOrDefault(user.Id),
            ManagerTypes = new[] { managerType }
        }).ToList();
    }

    public Task<Result<ManagerHandoverRequestDto>> CreateProgramAsync(Guid id, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default) =>
        CreateProgramCoreAsync(id, dto, userId, ManagerHandoverInitiationType.ManagerInitiated, ct);

    public Task<Result<ManagerHandoverRequestDto>> CreatePortfolioAsync(Guid id, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default) =>
        CreatePortfolioCoreAsync(id, dto, userId, ManagerHandoverInitiationType.ManagerInitiated, ct);

    public Task<Result<ManagerHandoverRequestDto>> CreateAdminProgramAsync(Guid id, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default) =>
        CreateProgramCoreAsync(id, dto, userId, ManagerHandoverInitiationType.AdminInitiated, ct);

    public Task<Result<ManagerHandoverRequestDto>> CreateAdminPortfolioAsync(Guid id, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default) =>
        CreatePortfolioCoreAsync(id, dto, userId, ManagerHandoverInitiationType.AdminInitiated, ct);

    public Task<Result<ManagerHandoverRequestDto>> CreateInitialProgramAssignmentAsync(Guid id, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default) =>
        CreateProgramCoreAsync(id, dto, userId, ManagerHandoverInitiationType.InitialAssignment, ct);

    public Task<Result<ManagerHandoverRequestDto>> CreateInitialPortfolioAssignmentAsync(Guid id, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default) =>
        CreatePortfolioCoreAsync(id, dto, userId, ManagerHandoverInitiationType.InitialAssignment, ct);

    private async Task<Result<ManagerHandoverRequestDto>> CreateProgramCoreAsync(Guid id, CreateManagerHandoverRequestDto dto, Guid userId, string initiationType, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var program = await _db.Set<Program>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (program is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Program not found"));
        var requiredPermission = initiationType == ManagerHandoverInitiationType.ManagerInitiated ? "Handover.Manage" : "Handover.AdminInitiate";
        if (!await IsSameTenantAsync(userId, program.TenantId, ct) || !await HasPermissionAsync(userId, program.TenantId, requiredPermission, ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to initiate this handover"));
        if (!HasWritableActorContext(userId, program.TenantId))
            return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Read-only management views cannot initiate handovers"));
        if (!HasProgramScope(userId, program.TenantId, program))
            return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Program not found"));
        if (initiationType == ManagerHandoverInitiationType.ManagerInitiated && program.ProgramManagerId != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the current Program Manager can request a handover"));
        if (initiationType == ManagerHandoverInitiationType.AdminInitiated && !program.ProgramManagerId.HasValue) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("This Program has no assigned manager. Use initial assignment instead."));
        if (initiationType == ManagerHandoverInitiationType.InitialAssignment && program.ProgramManagerId.HasValue) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("This Program already has a manager. Use administrative handover instead."));
        var validation = Validate(dto, program.ProgramManagerId, initiationType == ManagerHandoverInitiationType.InitialAssignment);
        if (validation is not null) return Result<ManagerHandoverRequestDto>.Failure(validation);
        if (!(await GetEligibleManagersAsync(program.TenantId, program.ProgramManagerId, ct, "Program.View", "ProgramManager")).Any(x => x.Id == dto.NewManagerId)) return Result<ManagerHandoverRequestDto>.Failure(Error.Validation("Select an eligible active manager in this tenant who can approve the handover"));
        if (await _db.Set<ProgramHandoverRequest>().AnyAsync(x => x.ProgramId == id && x.TenantId == program.TenantId && x.Status == ManagerHandoverStatus.Pending, ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("A pending handover request already exists"));
        var request = new ProgramHandoverRequest { TenantId = program.TenantId, ProgramId = id, CurrentProgramManagerId = program.ProgramManagerId, NewProgramManagerId = dto.NewManagerId, InitiationType = initiationType, EffectiveHandoverDate = dto.EffectiveHandoverDate.Date, Reason = dto.Reason.Trim(), Notes = dto.Notes?.Trim(), RequestedBy = userId, CreatedBy = userId.ToString() };
        _db.Set<ProgramHandoverRequest>().Add(request);
        AddAudit("Program", id, "ManagerHandoverRequested", userId, userId, request.Reason, program.TenantId, request.InitiationType, request.CurrentProgramManagerId);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await NotifyRequestedAsync("Program", id, program.Name, program.ProgramManagerId, dto.NewManagerId, request.Id, ct);
        return Result<ManagerHandoverRequestDto>.Success(await MapProgramRequestAsync(request, ct));
    }

    private async Task<Result<ManagerHandoverRequestDto>> CreatePortfolioCoreAsync(Guid id, CreateManagerHandoverRequestDto dto, Guid userId, string initiationType, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var portfolio = await _db.Set<PortfolioEntity>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (portfolio is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Portfolio not found"));
        var requiredPermission = initiationType == ManagerHandoverInitiationType.ManagerInitiated ? "Handover.Manage" : "Handover.AdminInitiate";
        if (!await IsSameTenantAsync(userId, portfolio.TenantId, ct) || !await HasPermissionAsync(userId, portfolio.TenantId, requiredPermission, ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to initiate this handover"));
        if (!HasWritableActorContext(userId, portfolio.TenantId))
            return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Read-only management views cannot initiate handovers"));
        if (!HasPortfolioScope(userId, portfolio.TenantId, portfolio.Id))
            return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Portfolio not found"));
        if (initiationType == ManagerHandoverInitiationType.ManagerInitiated && portfolio.OwnerId != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the current Portfolio Manager can request a handover"));
        if (initiationType == ManagerHandoverInitiationType.AdminInitiated && !portfolio.OwnerId.HasValue) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("This Portfolio has no assigned manager. Use initial assignment instead."));
        if (initiationType == ManagerHandoverInitiationType.InitialAssignment && portfolio.OwnerId.HasValue) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("This Portfolio already has a manager. Use administrative handover instead."));
        var validation = Validate(dto, portfolio.OwnerId, initiationType == ManagerHandoverInitiationType.InitialAssignment);
        if (validation is not null) return Result<ManagerHandoverRequestDto>.Failure(validation);
        if (!(await GetEligibleManagersAsync(portfolio.TenantId, portfolio.OwnerId, ct, "Portfolio.View", "PortfolioManager")).Any(x => x.Id == dto.NewManagerId)) return Result<ManagerHandoverRequestDto>.Failure(Error.Validation("Select an eligible active manager in this tenant who can approve the handover"));
        if (await _db.Set<PortfolioHandoverRequest>().AnyAsync(x => x.PortfolioId == id && x.TenantId == portfolio.TenantId && x.Status == ManagerHandoverStatus.Pending, ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("A pending handover request already exists"));
        var request = new PortfolioHandoverRequest { TenantId = portfolio.TenantId, PortfolioId = id, CurrentPortfolioManagerId = portfolio.OwnerId, NewPortfolioManagerId = dto.NewManagerId, InitiationType = initiationType, EffectiveHandoverDate = dto.EffectiveHandoverDate.Date, Reason = dto.Reason.Trim(), Notes = dto.Notes?.Trim(), RequestedBy = userId, CreatedBy = userId.ToString() };
        _db.Set<PortfolioHandoverRequest>().Add(request);
        AddAudit("Portfolio", id, "ManagerHandoverRequested", userId, userId, request.Reason, portfolio.TenantId, request.InitiationType, request.CurrentPortfolioManagerId);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await NotifyRequestedAsync("Portfolio", id, portfolio.Name, portfolio.OwnerId, dto.NewManagerId, request.Id, ct);
        return Result<ManagerHandoverRequestDto>.Success(await MapPortfolioRequestAsync(request, ct));
    }

    public async Task<Result<ManagerHandoverCapabilitiesDto>> GetProgramCapabilitiesAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var program = await _db.Set<Program>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (program is null || !await IsSameTenantAsync(userId, program.TenantId, ct)) return Result<ManagerHandoverCapabilitiesDto>.Failure(Error.NotFound("Program not found"));
        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(userId, program.TenantId, ct);
        if (!HasProgramScope(userId, program.TenantId, program))
            return Result<ManagerHandoverCapabilitiesDto>.Failure(Error.NotFound("Program not found"));
        var writableScope = HasProgramScope(userId, program.TenantId, program, requireWrite: true);
        var pending = await _db.Set<ProgramHandoverRequest>().AsNoTracking().FirstOrDefaultAsync(x => x.ProgramId == id && x.TenantId == program.TenantId && x.Status == ManagerHandoverStatus.Pending && !x.IsDeleted, ct);
        var requesterCanCancel = pending is not null && pending.RequestedBy == userId
            && permissions.Contains(pending.InitiationType == ManagerHandoverInitiationType.ManagerInitiated ? "Handover.Manage" : "Handover.AdminInitiate");
        return Result<ManagerHandoverCapabilitiesDto>.Success(new ManagerHandoverCapabilitiesDto
        {
            CurrentManagerId = program.ProgramManagerId,
            CurrentManagerName = await ResolveUserNameAsync(program.ProgramManagerId, program.TenantId, ct),
            CanViewRequests = permissions.Contains("Handover.View"),
            CanInitiate = writableScope && pending is null && program.ProgramManagerId == userId && permissions.Contains("Handover.Manage"),
            CanAdminInitiate = writableScope && pending is null && program.ProgramManagerId.HasValue && permissions.Contains("Handover.AdminInitiate"),
            CanAssignInitial = writableScope && pending is null && !program.ProgramManagerId.HasValue && permissions.Contains("Handover.AdminInitiate"),
            CanApproveIncoming = writableScope && pending?.NewProgramManagerId == userId && permissions.Contains("Handover.Approve")
                && await IsActiveManagementUserAsync(userId, program.TenantId, ct),
            CanCancelOwnPending = writableScope && requesterCanCancel,
            HasPendingRequest = pending is not null
        });
    }

    public async Task<Result<ManagerHandoverCapabilitiesDto>> GetPortfolioCapabilitiesAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var portfolio = await _db.Set<PortfolioEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (portfolio is null || !await IsSameTenantAsync(userId, portfolio.TenantId, ct)) return Result<ManagerHandoverCapabilitiesDto>.Failure(Error.NotFound("Portfolio not found"));
        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(userId, portfolio.TenantId, ct);
        if (!HasPortfolioScope(userId, portfolio.TenantId, portfolio.Id))
            return Result<ManagerHandoverCapabilitiesDto>.Failure(Error.NotFound("Portfolio not found"));
        var writableScope = HasPortfolioScope(userId, portfolio.TenantId, portfolio.Id, requireWrite: true);
        var pending = await _db.Set<PortfolioHandoverRequest>().AsNoTracking().FirstOrDefaultAsync(x => x.PortfolioId == id && x.TenantId == portfolio.TenantId && x.Status == ManagerHandoverStatus.Pending && !x.IsDeleted, ct);
        var requesterCanCancel = pending is not null && pending.RequestedBy == userId
            && permissions.Contains(pending.InitiationType == ManagerHandoverInitiationType.ManagerInitiated ? "Handover.Manage" : "Handover.AdminInitiate");
        return Result<ManagerHandoverCapabilitiesDto>.Success(new ManagerHandoverCapabilitiesDto
        {
            CurrentManagerId = portfolio.OwnerId,
            CurrentManagerName = await ResolveUserNameAsync(portfolio.OwnerId, portfolio.TenantId, ct),
            CanViewRequests = permissions.Contains("Handover.View"),
            CanInitiate = writableScope && pending is null && portfolio.OwnerId == userId && permissions.Contains("Handover.Manage"),
            CanAdminInitiate = writableScope && pending is null && portfolio.OwnerId.HasValue && permissions.Contains("Handover.AdminInitiate"),
            CanAssignInitial = writableScope && pending is null && !portfolio.OwnerId.HasValue && permissions.Contains("Handover.AdminInitiate"),
            CanApproveIncoming = writableScope && pending?.NewPortfolioManagerId == userId && permissions.Contains("Handover.Approve")
                && await IsActiveManagementUserAsync(userId, portfolio.TenantId, ct),
            CanCancelOwnPending = writableScope && requesterCanCancel,
            HasPendingRequest = pending is not null
        });
    }

    public async Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetProgramRequestsAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var program = await _db.Set<Program>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (program is null || !await IsSameTenantAsync(userId, program.TenantId, ct)) return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Failure(Error.NotFound("Program not found"));
        if (!await HasPermissionAsync(userId, program.TenantId, "Handover.View", ct)) return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Failure(Error.Forbidden("You do not have permission to view handovers"));
        if (!HasProgramScope(userId, program.TenantId, program)) return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Failure(Error.NotFound("Program not found"));
        var requests = await _db.Set<ProgramHandoverRequest>().Where(x => x.ProgramId == id && x.TenantId == program.TenantId && !x.IsDeleted).OrderByDescending(x => x.RequestedAt).ToListAsync(ct);
        return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Success(await MapProgramRequestsAsync(requests, ct));
    }

    public async Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetPortfolioRequestsAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var portfolio = await _db.Set<PortfolioEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (portfolio is null || !await IsSameTenantAsync(userId, portfolio.TenantId, ct)) return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Failure(Error.NotFound("Portfolio not found"));
        if (!await HasPermissionAsync(userId, portfolio.TenantId, "Handover.View", ct)) return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Failure(Error.Forbidden("You do not have permission to view handovers"));
        if (!HasPortfolioScope(userId, portfolio.TenantId, portfolio.Id)) return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Failure(Error.NotFound("Portfolio not found"));
        var requests = await _db.Set<PortfolioHandoverRequest>().Where(x => x.PortfolioId == id && x.TenantId == portfolio.TenantId && !x.IsDeleted).OrderByDescending(x => x.RequestedAt).ToListAsync(ct);
        return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Success(await MapPortfolioRequestsAsync(requests, ct));
    }

    public async Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetPendingForMeAsync(Guid userId, CancellationToken ct = default)
    {
        var tenantId = await ResolveTenantAsync(userId, ct);
        if (!tenantId.HasValue || !await HasPermissionAsync(userId, tenantId.Value, "Handover.Approve", ct)
            || !await IsActiveManagementUserAsync(userId, tenantId.Value, ct)) return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Failure(Error.Forbidden("You do not have permission to approve handovers"));
        var programs = await _db.Set<ProgramHandoverRequest>().Where(x => x.TenantId == tenantId && x.NewProgramManagerId == userId && x.Status == ManagerHandoverStatus.Pending && !x.IsDeleted).OrderByDescending(x => x.RequestedAt).ToListAsync(ct);
        var portfolios = await _db.Set<PortfolioHandoverRequest>().Where(x => x.TenantId == tenantId && x.NewPortfolioManagerId == userId && x.Status == ManagerHandoverStatus.Pending && !x.IsDeleted).OrderByDescending(x => x.RequestedAt).ToListAsync(ct);
        var result = new List<ManagerHandoverRequestDto>(programs.Count + portfolios.Count);
        result.AddRange(await MapProgramRequestsAsync(programs, ct));
        result.AddRange(await MapPortfolioRequestsAsync(portfolios, ct));
        return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Success(result.OrderByDescending(x => x.RequestedAt).ToList());
    }

    public async Task<Result<ApprovalReviewDto>> GetProgramApprovalReviewAsync(Guid requestId, Guid userId, CancellationToken ct = default)
    {
        var tenantId = await ResolveTenantAsync(userId, ct);
        if (!tenantId.HasValue || !await HasPermissionAsync(userId, tenantId.Value, "Handover.Approve", ct)
            || !await IsActiveManagementUserAsync(userId, tenantId.Value, ct)
            || !await IsSameTenantAsync(userId, tenantId.Value, ct))
            return Result<ApprovalReviewDto>.Failure(Error.NotFound("This approval is not available to the current user."));

        var request = await _db.Set<ProgramHandoverRequest>().AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id == requestId && item.TenantId == tenantId.Value && item.NewProgramManagerId == userId
            && item.Status == ManagerHandoverStatus.Pending && !item.IsDeleted, ct);
        if (request is null) return Result<ApprovalReviewDto>.Failure(Error.NotFound("This approval is no longer available."));
        var program = await _db.Set<Program>().AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id == request.ProgramId && item.TenantId == tenantId.Value && !item.IsDeleted, ct);
        if (program is null) return Result<ApprovalReviewDto>.Failure(Error.NotFound("The Program for this approval is unavailable."));
        var details = await MapProgramRequestAsync(request, ct);

        return Result<ApprovalReviewDto>.Success(new ApprovalReviewDto
        {
            ApprovalId = request.Id,
            ApprovalType = "ProgramHandover",
            BusinessObjectType = "Program",
            BusinessObjectName = program.Name,
            BusinessObjectCode = program.Code,
            BusinessObjectStatus = program.Status,
            Title = "Program Manager Handover",
            Reason = request.Reason,
            Notes = request.Notes,
            RequestedByName = details.RequestedByName,
            RequestedAt = request.RequestedAt,
            Status = request.Status,
            EffectiveDate = request.EffectiveHandoverDate,
            CurrentAssigneeName = details.CurrentManagerName,
            ProposedAssigneeName = details.NewManagerName,
            IsReadOnly = true,
            CanDecide = true
        });
    }

    public async Task<Result<ApprovalReviewDto>> GetPortfolioApprovalReviewAsync(Guid requestId, Guid userId, CancellationToken ct = default)
    {
        var tenantId = await ResolveTenantAsync(userId, ct);
        if (!tenantId.HasValue || !await HasPermissionAsync(userId, tenantId.Value, "Handover.Approve", ct)
            || !await IsActiveManagementUserAsync(userId, tenantId.Value, ct)
            || !await IsSameTenantAsync(userId, tenantId.Value, ct))
            return Result<ApprovalReviewDto>.Failure(Error.NotFound("This approval is not available to the current user."));

        var request = await _db.Set<PortfolioHandoverRequest>().AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id == requestId && item.TenantId == tenantId.Value && item.NewPortfolioManagerId == userId
            && item.Status == ManagerHandoverStatus.Pending && !item.IsDeleted, ct);
        if (request is null) return Result<ApprovalReviewDto>.Failure(Error.NotFound("This approval is no longer available."));
        var portfolio = await _db.Set<PortfolioEntity>().AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id == request.PortfolioId && item.TenantId == tenantId.Value && !item.IsDeleted, ct);
        if (portfolio is null) return Result<ApprovalReviewDto>.Failure(Error.NotFound("The Portfolio for this approval is unavailable."));
        var details = await MapPortfolioRequestAsync(request, ct);

        return Result<ApprovalReviewDto>.Success(new ApprovalReviewDto
        {
            ApprovalId = request.Id,
            ApprovalType = "PortfolioHandover",
            BusinessObjectType = "Portfolio",
            BusinessObjectName = portfolio.Name,
            BusinessObjectCode = portfolio.Code,
            BusinessObjectStatus = portfolio.Status,
            Title = "Portfolio Manager Handover",
            Reason = request.Reason,
            Notes = request.Notes,
            RequestedByName = details.RequestedByName,
            RequestedAt = request.RequestedAt,
            Status = request.Status,
            EffectiveDate = request.EffectiveHandoverDate,
            CurrentAssigneeName = details.CurrentManagerName,
            ProposedAssigneeName = details.NewManagerName,
            IsReadOnly = true,
            CanDecide = true
        });
    }

    public async Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetPendingProgramForMeAsync(Guid programId, Guid userId, CancellationToken ct = default)
    {
        var tenantId = await ResolveTenantAsync(userId, ct);
        if (!tenantId.HasValue || !await HasPermissionAsync(userId, tenantId.Value, "Handover.Approve", ct)
            || !await IsActiveManagementUserAsync(userId, tenantId.Value, ct)) return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Failure(Error.Forbidden("You do not have permission to approve handovers"));
        var program = await _db.Set<Program>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == programId, ct);
        if (program is null || !await IsSameTenantAsync(userId, program.TenantId, ct)) return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Failure(Error.NotFound("Program not found"));
        var requests = await _db.Set<ProgramHandoverRequest>()
            .Where(x => x.ProgramId == programId && x.TenantId == program.TenantId && x.NewProgramManagerId == userId && x.Status == ManagerHandoverStatus.Pending && !x.IsDeleted)
            .OrderByDescending(x => x.RequestedAt).ToListAsync(ct);
        return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Success(await MapProgramRequestsAsync(requests, ct));
    }

    public async Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetPendingPortfolioForMeAsync(Guid portfolioId, Guid userId, CancellationToken ct = default)
    {
        var tenantId = await ResolveTenantAsync(userId, ct);
        if (!tenantId.HasValue || !await HasPermissionAsync(userId, tenantId.Value, "Handover.Approve", ct)
            || !await IsActiveManagementUserAsync(userId, tenantId.Value, ct)) return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Failure(Error.Forbidden("You do not have permission to approve handovers"));
        var portfolio = await _db.Set<PortfolioEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == portfolioId, ct);
        if (portfolio is null || !await IsSameTenantAsync(userId, portfolio.TenantId, ct)) return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Failure(Error.NotFound("Portfolio not found"));
        var requests = await _db.Set<PortfolioHandoverRequest>()
            .Where(x => x.PortfolioId == portfolioId && x.TenantId == portfolio.TenantId && x.NewPortfolioManagerId == userId && x.Status == ManagerHandoverStatus.Pending && !x.IsDeleted)
            .OrderByDescending(x => x.RequestedAt).ToListAsync(ct);
        return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Success(await MapPortfolioRequestsAsync(requests, ct));
    }

    public Task<Result<ManagerHandoverRequestDto>> AcceptProgramAsync(Guid id, Guid userId, CancellationToken ct = default) => AcceptProgramCoreAsync(id, userId, ct);
    public Task<Result<ManagerHandoverRequestDto>> AcceptPortfolioAsync(Guid id, Guid userId, CancellationToken ct = default) => AcceptPortfolioCoreAsync(id, userId, ct);

    private async Task<Result<ManagerHandoverRequestDto>> AcceptProgramCoreAsync(Guid id, Guid userId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await _db.Set<ProgramHandoverRequest>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (request is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Handover request not found"));
        if (request.Status != ManagerHandoverStatus.Pending) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("Only pending handover requests can be accepted"));
        if (request.NewProgramManagerId != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the selected new manager can accept this request"));
        if (!await IsSameTenantAsync(userId, request.TenantId, ct) || !await HasPermissionAsync(userId, request.TenantId, "Handover.Approve", ct)
            || !await IsActiveManagementUserAsync(userId, request.TenantId, ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to approve handovers"));
        if (!HasWritableActorContext(userId, request.TenantId)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Read-only management views cannot accept handovers"));
        var program = await _db.Set<Program>().FirstOrDefaultAsync(x => x.Id == request.ProgramId && x.TenantId == request.TenantId && !x.IsDeleted, ct);
        if (program is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Program not found"));
        if (program.ProgramManagerId != request.CurrentProgramManagerId) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("The current Program Manager has changed"));
        ProgramManagerAssignment? current = null;
        if (request.CurrentProgramManagerId.HasValue)
        {
            current = await EnsureProgramAssignmentAsync(program, userId, ct);
            if (current.ProgramManagerId != request.CurrentProgramManagerId.Value) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("The current Program Manager assignment has changed"));
            current.EndDate = request.EffectiveHandoverDate; current.IsCurrent = false; current.HandoverDate = request.EffectiveHandoverDate; current.HandoverReason = request.Reason; current.HandoverNotes = request.Notes; current.UpdatedBy = userId.ToString();
        }
        else if (await _db.Set<ProgramManagerAssignment>().AnyAsync(x => x.TenantId == program.TenantId && x.ProgramId == program.Id && x.IsCurrent && !x.IsDeleted, ct))
            return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("The Program has an inconsistent active manager assignment. Resolve it before initial assignment."));
        var number = await NextProgramSnapshotAsync(program.Id, program.TenantId, ct);
        var snapshot = new ProgramSnapshot { TenantId = program.TenantId, ProgramId = program.Id, SnapshotNumber = number, Name = $"{program.Name} - Handover #{number}", EffectiveEndDate = request.EffectiveHandoverDate, PreviousProgramManagerId = current?.ProgramManagerId, NewProgramManagerId = request.NewProgramManagerId, HandoverReason = request.Reason, HandoverNotes = request.Notes, Data = JsonSerializer.Serialize(new { program.Name, program.Code, program.Description, program.Status, program.PortfolioId, program.Budget, program.ActualSpend, program.Health }), CreatedBy = userId.ToString() };
        _db.Set<ProgramSnapshot>().Add(snapshot);
        _db.Set<ProgramManagerAssignment>().Add(new ProgramManagerAssignment { TenantId = program.TenantId, ProgramId = program.Id, ProgramManagerId = request.NewProgramManagerId, StartDate = request.EffectiveHandoverDate, IsCurrent = true, HandoverFromProgramManagerId = current?.ProgramManagerId, HandoverDate = request.EffectiveHandoverDate, HandoverReason = request.Reason, HandoverNotes = request.Notes, CreatedBy = userId.ToString() });
        program.ProgramManagerId = request.NewProgramManagerId; program.UpdatedBy = userId.ToString();
        request.Status = ManagerHandoverStatus.Accepted; request.RespondedBy = userId; request.RespondedAt = DateTime.UtcNow; request.SnapshotId = snapshot.Id; request.UpdatedBy = userId.ToString();
        AddAudit("Program", program.Id, "ManagerHandoverAccepted", request.RequestedBy, userId, request.Reason, program.TenantId, request.InitiationType, request.CurrentProgramManagerId);
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await NotifyCompletedAsync("Program", program.Id, program.Name, request.CurrentProgramManagerId, request.NewProgramManagerId, request.Id, ManagerHandoverStatus.Accepted, program.TenantId, ct);
        return Result<ManagerHandoverRequestDto>.Success(await MapProgramRequestAsync(request, ct));
    }

    private async Task<Result<ManagerHandoverRequestDto>> AcceptPortfolioCoreAsync(Guid id, Guid userId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await _db.Set<PortfolioHandoverRequest>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (request is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Handover request not found"));
        if (request.Status != ManagerHandoverStatus.Pending) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("Only pending handover requests can be accepted"));
        if (request.NewPortfolioManagerId != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the selected new manager can accept this request"));
        if (!await IsSameTenantAsync(userId, request.TenantId, ct) || !await HasPermissionAsync(userId, request.TenantId, "Handover.Approve", ct)
            || !await IsActiveManagementUserAsync(userId, request.TenantId, ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to approve handovers"));
        if (!HasWritableActorContext(userId, request.TenantId)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Read-only management views cannot accept handovers"));
        var portfolio = await _db.Set<PortfolioEntity>().FirstOrDefaultAsync(x => x.Id == request.PortfolioId && x.TenantId == request.TenantId && !x.IsDeleted, ct);
        if (portfolio is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Portfolio not found"));
        if (portfolio.OwnerId != request.CurrentPortfolioManagerId) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("The current Portfolio Manager has changed"));
        PortfolioManagerAssignment? current = null;
        if (request.CurrentPortfolioManagerId.HasValue)
        {
            current = await EnsurePortfolioAssignmentAsync(portfolio, userId, ct);
            if (current.PortfolioManagerId != request.CurrentPortfolioManagerId.Value) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("The current Portfolio Manager assignment has changed"));
            current.EndDate = request.EffectiveHandoverDate; current.IsCurrent = false; current.HandoverDate = request.EffectiveHandoverDate; current.HandoverReason = request.Reason; current.HandoverNotes = request.Notes; current.UpdatedBy = userId.ToString();
        }
        else if (await _db.Set<PortfolioManagerAssignment>().AnyAsync(x => x.TenantId == portfolio.TenantId && x.PortfolioId == portfolio.Id && x.IsCurrent && !x.IsDeleted, ct))
            return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("The Portfolio has an inconsistent active manager assignment. Resolve it before initial assignment."));
        var number = await NextPortfolioSnapshotAsync(portfolio.Id, portfolio.TenantId, ct);
        var snapshot = new PortfolioSnapshot { TenantId = portfolio.TenantId, PortfolioId = portfolio.Id, SnapshotNumber = number, Name = $"{portfolio.Name} - Handover #{number}", EffectiveEndDate = request.EffectiveHandoverDate, PreviousPortfolioManagerId = current?.PortfolioManagerId, NewPortfolioManagerId = request.NewPortfolioManagerId, HandoverReason = request.Reason, HandoverNotes = request.Notes, Data = JsonSerializer.Serialize(new { portfolio.Name, portfolio.Code, portfolio.Description, portfolio.Status, portfolio.OwnerId, portfolio.OrganizationId, portfolio.Currency, portfolio.Health, portfolio.Priority }), CreatedBy = userId.ToString() };
        _db.Set<PortfolioSnapshot>().Add(snapshot);
        _db.Set<PortfolioManagerAssignment>().Add(new PortfolioManagerAssignment { TenantId = portfolio.TenantId, PortfolioId = portfolio.Id, PortfolioManagerId = request.NewPortfolioManagerId, StartDate = request.EffectiveHandoverDate, IsCurrent = true, HandoverFromPortfolioManagerId = current?.PortfolioManagerId, HandoverDate = request.EffectiveHandoverDate, HandoverReason = request.Reason, HandoverNotes = request.Notes, CreatedBy = userId.ToString() });
        portfolio.OwnerId = request.NewPortfolioManagerId; portfolio.UpdatedBy = userId.ToString();
        request.Status = ManagerHandoverStatus.Accepted; request.RespondedBy = userId; request.RespondedAt = DateTime.UtcNow; request.SnapshotId = snapshot.Id; request.UpdatedBy = userId.ToString();
        AddAudit("Portfolio", portfolio.Id, "ManagerHandoverAccepted", request.RequestedBy, userId, request.Reason, portfolio.TenantId, request.InitiationType, request.CurrentPortfolioManagerId);
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await NotifyCompletedAsync("Portfolio", portfolio.Id, portfolio.Name, request.CurrentPortfolioManagerId, request.NewPortfolioManagerId, request.Id, ManagerHandoverStatus.Accepted, portfolio.TenantId, ct);
        return Result<ManagerHandoverRequestDto>.Success(await MapPortfolioRequestAsync(request, ct));
    }

    public Task<Result<ManagerHandoverRequestDto>> RejectProgramAsync(Guid id, RejectManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default) => ChangeProgramStatusAsync(id, dto.RejectionReason, ManagerHandoverStatus.Rejected, userId, ct);
    public Task<Result<ManagerHandoverRequestDto>> RejectPortfolioAsync(Guid id, RejectManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default) => ChangePortfolioStatusAsync(id, dto.RejectionReason, ManagerHandoverStatus.Rejected, userId, ct);
    public Task<Result<ManagerHandoverRequestDto>> CancelProgramAsync(Guid id, Guid userId, CancellationToken ct = default) => ChangeProgramStatusAsync(id, null, ManagerHandoverStatus.Cancelled, userId, ct);
    public Task<Result<ManagerHandoverRequestDto>> CancelPortfolioAsync(Guid id, Guid userId, CancellationToken ct = default) => ChangePortfolioStatusAsync(id, null, ManagerHandoverStatus.Cancelled, userId, ct);

    private async Task<Result<ManagerHandoverRequestDto>> ChangeProgramStatusAsync(Guid id, string? reason, string status, Guid userId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await _db.Set<ProgramHandoverRequest>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (request is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Handover request not found"));
        if (request.Status != ManagerHandoverStatus.Pending) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("Only pending handover requests can be changed"));
        if (status == ManagerHandoverStatus.Rejected && request.NewProgramManagerId != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the selected new manager can reject this request"));
        var requiredPermission = status == ManagerHandoverStatus.Rejected ? "Handover.Approve" : request.InitiationType == ManagerHandoverInitiationType.ManagerInitiated ? "Handover.Manage" : "Handover.AdminInitiate";
        if (!await IsSameTenantAsync(userId, request.TenantId, ct) || !await HasPermissionAsync(userId, request.TenantId, requiredPermission, ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to change this handover request"));
        if (!HasWritableActorContext(userId, request.TenantId)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Read-only management views cannot change handover requests"));
        if (status == ManagerHandoverStatus.Rejected && !await IsActiveManagementUserAsync(userId, request.TenantId, ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("An active Management-category role is required to approve handovers"));
        if (status == ManagerHandoverStatus.Cancelled && request.RequestedBy != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the requester can cancel this request"));
        if (status == ManagerHandoverStatus.Rejected && string.IsNullOrWhiteSpace(reason)) return Result<ManagerHandoverRequestDto>.Failure(Error.Validation("A rejection reason is required"));
        if (status == ManagerHandoverStatus.Rejected && reason!.Trim().Length > 500) return Result<ManagerHandoverRequestDto>.Failure(Error.Validation("Rejection reason cannot exceed 500 characters"));
        request.Status = status; request.RejectionReason = reason?.Trim(); request.RespondedBy = userId; request.RespondedAt = DateTime.UtcNow; request.UpdatedBy = userId.ToString();
        AddAudit("Program", request.ProgramId, $"ManagerHandover{status}", request.RequestedBy, userId, reason ?? request.Reason, request.TenantId, request.InitiationType, request.CurrentProgramManagerId);
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        var programName = await _db.Set<Program>().AsNoTracking().Where(x => x.Id == request.ProgramId && x.TenantId == request.TenantId).Select(x => x.Name).FirstAsync(ct);
        await NotifyCompletedAsync("Program", request.ProgramId, programName, request.CurrentProgramManagerId, request.NewProgramManagerId, request.Id, status, request.TenantId, ct);
        return Result<ManagerHandoverRequestDto>.Success(await MapProgramRequestAsync(request, ct));
    }

    private async Task<Result<ManagerHandoverRequestDto>> ChangePortfolioStatusAsync(Guid id, string? reason, string status, Guid userId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await _db.Set<PortfolioHandoverRequest>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (request is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Handover request not found"));
        if (request.Status != ManagerHandoverStatus.Pending) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("Only pending handover requests can be changed"));
        if (status == ManagerHandoverStatus.Rejected && request.NewPortfolioManagerId != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the selected new manager can reject this request"));
        var requiredPermission = status == ManagerHandoverStatus.Rejected ? "Handover.Approve" : request.InitiationType == ManagerHandoverInitiationType.ManagerInitiated ? "Handover.Manage" : "Handover.AdminInitiate";
        if (!await IsSameTenantAsync(userId, request.TenantId, ct) || !await HasPermissionAsync(userId, request.TenantId, requiredPermission, ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to change this handover request"));
        if (!HasWritableActorContext(userId, request.TenantId)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Read-only management views cannot change handover requests"));
        if (status == ManagerHandoverStatus.Rejected && !await IsActiveManagementUserAsync(userId, request.TenantId, ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("An active Management-category role is required to approve handovers"));
        if (status == ManagerHandoverStatus.Cancelled && request.RequestedBy != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the requester can cancel this request"));
        if (status == ManagerHandoverStatus.Rejected && string.IsNullOrWhiteSpace(reason)) return Result<ManagerHandoverRequestDto>.Failure(Error.Validation("A rejection reason is required"));
        if (status == ManagerHandoverStatus.Rejected && reason!.Trim().Length > 500) return Result<ManagerHandoverRequestDto>.Failure(Error.Validation("Rejection reason cannot exceed 500 characters"));
        request.Status = status; request.RejectionReason = reason?.Trim(); request.RespondedBy = userId; request.RespondedAt = DateTime.UtcNow; request.UpdatedBy = userId.ToString();
        AddAudit("Portfolio", request.PortfolioId, $"ManagerHandover{status}", request.RequestedBy, userId, reason ?? request.Reason, request.TenantId, request.InitiationType, request.CurrentPortfolioManagerId);
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        var portfolioName = await _db.Set<PortfolioEntity>().AsNoTracking().Where(x => x.Id == request.PortfolioId && x.TenantId == request.TenantId).Select(x => x.Name).FirstAsync(ct);
        await NotifyCompletedAsync("Portfolio", request.PortfolioId, portfolioName, request.CurrentPortfolioManagerId, request.NewPortfolioManagerId, request.Id, status, request.TenantId, ct);
        return Result<ManagerHandoverRequestDto>.Success(await MapPortfolioRequestAsync(request, ct));
    }

    public async Task<Result<IReadOnlyList<ManagerAssignmentDto>>> GetProgramHistoryAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var program = await _db.Set<Program>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (program is null || !await IsSameTenantAsync(userId, program.TenantId, ct)) return Result<IReadOnlyList<ManagerAssignmentDto>>.Failure(Error.NotFound("Program not found"));
        if (!await HasPermissionAsync(userId, program.TenantId, "Handover.View", ct)) return Result<IReadOnlyList<ManagerAssignmentDto>>.Failure(Error.Forbidden("You do not have permission to view handover history"));
        if (!HasProgramScope(userId, program.TenantId, program)) return Result<IReadOnlyList<ManagerAssignmentDto>>.Failure(Error.NotFound("Program not found"));
        var items = await _db.Set<ProgramManagerAssignment>().Where(x => x.TenantId == program.TenantId && x.ProgramId == id && !x.IsDeleted).OrderByDescending(x => x.StartDate).ToListAsync(ct);
        return Result<IReadOnlyList<ManagerAssignmentDto>>.Success(await MapAssignmentsAsync(program.TenantId, items.Select(x => (x.Id, id, x.ProgramManagerId, x.StartDate, x.EndDate, x.IsCurrent, x.HandoverDate, x.HandoverReason, x.HandoverNotes)).ToList(), ct));
    }

    public async Task<Result<IReadOnlyList<ManagerAssignmentDto>>> GetPortfolioHistoryAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var portfolio = await _db.Set<PortfolioEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (portfolio is null || !await IsSameTenantAsync(userId, portfolio.TenantId, ct)) return Result<IReadOnlyList<ManagerAssignmentDto>>.Failure(Error.NotFound("Portfolio not found"));
        if (!await HasPermissionAsync(userId, portfolio.TenantId, "Handover.View", ct)) return Result<IReadOnlyList<ManagerAssignmentDto>>.Failure(Error.Forbidden("You do not have permission to view handover history"));
        if (!HasPortfolioScope(userId, portfolio.TenantId, portfolio.Id)) return Result<IReadOnlyList<ManagerAssignmentDto>>.Failure(Error.NotFound("Portfolio not found"));
        var items = await _db.Set<PortfolioManagerAssignment>().Where(x => x.TenantId == portfolio.TenantId && x.PortfolioId == id && !x.IsDeleted).OrderByDescending(x => x.StartDate).ToListAsync(ct);
        return Result<IReadOnlyList<ManagerAssignmentDto>>.Success(await MapAssignmentsAsync(portfolio.TenantId, items.Select(x => (x.Id, id, x.PortfolioManagerId, x.StartDate, x.EndDate, x.IsCurrent, x.HandoverDate, x.HandoverReason, x.HandoverNotes)).ToList(), ct));
    }

    public async Task<Result<IReadOnlyList<ManagerSnapshotDto>>> GetProgramSnapshotsAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var program = await _db.Set<Program>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (program is null || !await IsSameTenantAsync(userId, program.TenantId, ct)) return Result<IReadOnlyList<ManagerSnapshotDto>>.Failure(Error.NotFound("Program not found"));
        if (!await HasPermissionAsync(userId, program.TenantId, "Handover.View", ct)) return Result<IReadOnlyList<ManagerSnapshotDto>>.Failure(Error.Forbidden("You do not have permission to view handover snapshots"));
        if (!HasProgramScope(userId, program.TenantId, program)) return Result<IReadOnlyList<ManagerSnapshotDto>>.Failure(Error.NotFound("Program not found"));
        var items = await _db.Set<ProgramSnapshot>().Where(x => x.TenantId == program.TenantId && x.ProgramId == id && !x.IsDeleted).OrderByDescending(x => x.SnapshotNumber).ToListAsync(ct);
        return Result<IReadOnlyList<ManagerSnapshotDto>>.Success(await MapSnapshotsAsync(program.TenantId, items.Select(x => (x.Id, id, x.SnapshotNumber, x.Name, x.SnapshotDate, x.EffectiveEndDate, x.PreviousProgramManagerId, x.NewProgramManagerId, x.HandoverReason, x.HandoverNotes, x.Data)).ToList(), ct));
    }

    public async Task<Result<IReadOnlyList<ManagerSnapshotDto>>> GetPortfolioSnapshotsAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var portfolio = await _db.Set<PortfolioEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (portfolio is null || !await IsSameTenantAsync(userId, portfolio.TenantId, ct)) return Result<IReadOnlyList<ManagerSnapshotDto>>.Failure(Error.NotFound("Portfolio not found"));
        if (!await HasPermissionAsync(userId, portfolio.TenantId, "Handover.View", ct)) return Result<IReadOnlyList<ManagerSnapshotDto>>.Failure(Error.Forbidden("You do not have permission to view handover snapshots"));
        if (!HasPortfolioScope(userId, portfolio.TenantId, portfolio.Id)) return Result<IReadOnlyList<ManagerSnapshotDto>>.Failure(Error.NotFound("Portfolio not found"));
        var items = await _db.Set<PortfolioSnapshot>().Where(x => x.TenantId == portfolio.TenantId && x.PortfolioId == id && !x.IsDeleted).OrderByDescending(x => x.SnapshotNumber).ToListAsync(ct);
        return Result<IReadOnlyList<ManagerSnapshotDto>>.Success(await MapSnapshotsAsync(portfolio.TenantId, items.Select(x => (x.Id, id, x.SnapshotNumber, x.Name, x.SnapshotDate, x.EffectiveEndDate, x.PreviousPortfolioManagerId, x.NewPortfolioManagerId, x.HandoverReason, x.HandoverNotes, x.Data)).ToList(), ct));
    }

    private bool HasWritableActorContext(Guid userId, Guid tenantId)
    {
        var context = _management.Current;
        return context is not null && context.UserId == userId && context.TenantId == tenantId
            && !context.EffectiveReadOnly;
    }

    private bool HasPortfolioScope(Guid userId, Guid tenantId, Guid portfolioId, bool requireWrite = false)
    {
        var context = _management.Current;
        return context is not null && context.UserId == userId && context.TenantId == tenantId
            && (!requireWrite || !context.EffectiveReadOnly)
            && ((!requireWrite && context.CanReadAllTenantPortfolioProgramData)
                || context.Scope.PortfolioIds.Contains(portfolioId));
    }

    private bool HasProgramScope(Guid userId, Guid tenantId, Program program, bool requireWrite = false)
    {
        var context = _management.Current;
        return context is not null && context.UserId == userId && context.TenantId == tenantId
            && (!requireWrite || !context.EffectiveReadOnly)
            && ((!requireWrite && context.CanReadAllTenantPortfolioProgramData)
                || context.Scope.ProgramIds.Contains(program.Id)
                || context.Scope.PortfolioIds.Contains(program.PortfolioId));
    }

    private static Error? Validate(CreateManagerHandoverRequestDto dto, Guid? currentId, bool initialAssignment) =>
        (!initialAssignment && (!currentId.HasValue || currentId == Guid.Empty))
            ? Error.Validation("The entity must have a current manager")
            : dto.NewManagerId == Guid.Empty || dto.NewManagerId == currentId
                ? Error.Validation("A different eligible manager is required")
                : dto.EffectiveHandoverDate == default
                    ? Error.Validation("Effective handover date is required")
                    : string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Trim().Length > 500
                        ? Error.Validation("A reason is required and limited to 500 characters")
                        : dto.Notes?.Trim().Length > 2000
                            ? Error.Validation("Handover notes are limited to 2000 characters")
                            : null;
    private async Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permission, CancellationToken ct)
    {
        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, ct);
        return permissions.Contains(permission);
    }

    private async Task<Guid?> ResolveTenantAsync(Guid userId, CancellationToken ct) =>
        await _db.Set<User>().AsNoTracking().Where(x => x.Id == userId && x.IsActive && !x.IsDeleted).Select(x => (Guid?)x.TenantId).FirstOrDefaultAsync(ct);

    private async Task<bool> IsSameTenantAsync(Guid userId, Guid tenantId, CancellationToken ct) =>
        await ResolveTenantAsync(userId, ct) == tenantId;

    private async Task<bool> IsActiveManagementUserAsync(Guid userId, Guid tenantId, CancellationToken ct) =>
        await _db.Set<User>().AnyAsync(user => user.Id == userId && user.TenantId == tenantId
            && user.IsActive && !user.IsDeleted
            && _db.Set<UserRole>().Any(assignment => assignment.UserId == user.Id
                && assignment.TenantId == tenantId && !assignment.IsDeleted
                && _db.Set<Role>().Any(role => role.Id == assignment.RoleId
                    && role.TenantId == tenantId && !role.IsDeleted && role.Category == "Management")), ct);

    private async Task<bool> HasAnyPermissionAsync(Guid userId, Guid tenantId, CancellationToken ct, params string[] permissions)
    {
        var effective = await _effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, ct);
        return permissions.Any(effective.Contains);
    }

    private static bool HasAny(IReadOnlySet<string> permissions, params string[] names) => names.Any(permissions.Contains);

    private async Task<string?> ResolveUserNameAsync(Guid? userId, Guid tenantId, CancellationToken ct)
    {
        if (!userId.HasValue) return null;
        return await _db.Set<User>().AsNoTracking()
            .Where(x => x.Id == userId.Value && x.TenantId == tenantId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<ProgramManagerAssignment> EnsureProgramAssignmentAsync(Program program, Guid actor, CancellationToken ct)
    {
        var current = await _db.Set<ProgramManagerAssignment>().FirstOrDefaultAsync(x => x.TenantId == program.TenantId && x.ProgramId == program.Id && x.IsCurrent && !x.IsDeleted, ct);
        if (current is not null) return current;
        current = new ProgramManagerAssignment { TenantId = program.TenantId, ProgramId = program.Id, ProgramManagerId = program.ProgramManagerId!.Value, StartDate = program.StartDate ?? program.CreatedAt, IsCurrent = true, CreatedBy = actor.ToString() };
        _db.Set<ProgramManagerAssignment>().Add(current); return current;
    }

    private async Task<PortfolioManagerAssignment> EnsurePortfolioAssignmentAsync(PortfolioEntity portfolio, Guid actor, CancellationToken ct)
    {
        var current = await _db.Set<PortfolioManagerAssignment>().FirstOrDefaultAsync(x => x.TenantId == portfolio.TenantId && x.PortfolioId == portfolio.Id && x.IsCurrent && !x.IsDeleted, ct);
        if (current is not null) return current;
        current = new PortfolioManagerAssignment { TenantId = portfolio.TenantId, PortfolioId = portfolio.Id, PortfolioManagerId = portfolio.OwnerId!.Value, StartDate = portfolio.StartDate ?? portfolio.CreatedAt, IsCurrent = true, CreatedBy = actor.ToString() };
        _db.Set<PortfolioManagerAssignment>().Add(current); return current;
    }

    private Task<int> NextProgramSnapshotAsync(Guid id, Guid tenantId, CancellationToken ct) => _db.Set<ProgramSnapshot>().Where(x => x.TenantId == tenantId && x.ProgramId == id).Select(x => (int?)x.SnapshotNumber).MaxAsync(ct).ContinueWith(t => (t.Result ?? 0) + 1, ct);
    private Task<int> NextPortfolioSnapshotAsync(Guid id, Guid tenantId, CancellationToken ct) => _db.Set<PortfolioSnapshot>().Where(x => x.TenantId == tenantId && x.PortfolioId == id).Select(x => (int?)x.SnapshotNumber).MaxAsync(ct).ContinueWith(t => (t.Result ?? 0) + 1, ct);

    private void AddAudit(string type, Guid entityId, string action, Guid requestedBy, Guid actor, string reason, Guid tenantId, string? initiationType = null, Guid? previousManagerId = null) =>
        _db.Set<AuditEntry>().Add(new AuditEntry
        {
            TenantId = tenantId,
            EntityType = type,
            EntityId = entityId,
            Action = action,
            OriginalValues = JsonSerializer.Serialize(new { requestedBy, reason, initiationType, previousManagerId }),
            UserId = actor,
            Timestamp = DateTime.UtcNow,
            CreatedBy = actor.ToString()
        });

    private async Task NotifyRequestedAsync(string type, Guid entityId, string name, Guid? currentId, Guid newId, Guid requestId, CancellationToken ct)
    {
        var tenantId = await ResolveEntityTenantAsync(type, entityId, ct);
        var organizationId = await ResolveEntityOrganizationIdAsync(type, entityId, tenantId, ct);
        await SendNotificationAsync(newId, tenantId, organizationId, $"{type} manager handover requested", $"You have a pending manager handover request for {name}.", type + "HandoverRequest", requestId, ct);
        if (currentId.HasValue && currentId.Value != newId) await SendNotificationAsync(currentId.Value, tenantId, organizationId, $"{type} manager handover submitted", $"A manager handover request for {name} is pending your selected manager's response.", type, entityId, ct);
    }

    private async Task NotifyCompletedAsync(string type, Guid entityId, string name, Guid? currentId, Guid newId, Guid requestId, string status, Guid tenantId, CancellationToken ct)
    {
        var organizationId = await ResolveEntityOrganizationIdAsync(type, entityId, tenantId, ct);
        if (currentId.HasValue && currentId.Value != newId)
            await SendNotificationAsync(currentId.Value, tenantId, organizationId, $"{type} manager handover {status.ToLowerInvariant()}", $"The manager handover request for {name} is now {status}.", type, entityId, ct);
        await SendNotificationAsync(newId, tenantId, organizationId, $"{type} manager handover {status.ToLowerInvariant()}", $"The manager handover request for {name} is now {status}.", type, entityId, ct);
    }

    private async Task<Guid> ResolveEntityTenantAsync(string type, Guid entityId, CancellationToken ct) => type == "Program"
        ? await _db.Set<Program>().Where(x => x.Id == entityId).Select(x => x.TenantId).FirstAsync(ct)
        : await _db.Set<PortfolioEntity>().Where(x => x.Id == entityId).Select(x => x.TenantId).FirstAsync(ct);

    private async Task<Guid?> ResolveEntityOrganizationIdAsync(string type, Guid entityId, Guid tenantId, CancellationToken ct)
    {
        Guid? portfolioId = type switch
        {
            "Program" => await _db.Set<Program>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == entityId && item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => (Guid?)item.PortfolioId)
                .SingleOrDefaultAsync(ct),
            "Portfolio" => entityId,
            _ => null,
        };
        if (!portfolioId.HasValue) return null;

        return await _db.Set<PortfolioEntity>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == portfolioId.Value && item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => item.OrganizationId)
            .SingleOrDefaultAsync(ct);
    }

    private Task SendNotificationAsync(Guid userId, Guid tenantId, Guid? organizationId, string title, string body, string referenceType, Guid referenceId, CancellationToken ct) =>
        _notifications.SendAsync(new Notification { TenantId = tenantId, OrganizationId = organizationId, UserId = userId, Title = title, Body = body, Type = referenceType.StartsWith("Program", StringComparison.Ordinal) ? NotificationTypes.ProgramHandover : NotificationTypes.PortfolioHandover, ReferenceType = referenceType, ReferenceId = referenceId, CreatedBy = userId.ToString() }, ct);

    private async Task<ManagerHandoverRequestDto> MapProgramRequestAsync(ProgramHandoverRequest x, CancellationToken ct) => await MapRequestAsync(x.TenantId, x.Id, "Program", x.ProgramId, x.CurrentProgramManagerId, x.NewProgramManagerId, x.InitiationType, x.EffectiveHandoverDate, x.Reason, x.Notes, x.Status, x.RequestedBy, x.RequestedAt, x.RespondedBy, x.RespondedAt, x.RejectionReason, x.SnapshotId, ct);
    private async Task<ManagerHandoverRequestDto> MapPortfolioRequestAsync(PortfolioHandoverRequest x, CancellationToken ct) => await MapRequestAsync(x.TenantId, x.Id, "Portfolio", x.PortfolioId, x.CurrentPortfolioManagerId, x.NewPortfolioManagerId, x.InitiationType, x.EffectiveHandoverDate, x.Reason, x.Notes, x.Status, x.RequestedBy, x.RequestedAt, x.RespondedBy, x.RespondedAt, x.RejectionReason, x.SnapshotId, ct);
    private async Task<IReadOnlyList<ManagerHandoverRequestDto>> MapProgramRequestsAsync(IEnumerable<ProgramHandoverRequest> items, CancellationToken ct)
    {
        var result = new List<ManagerHandoverRequestDto>();
        foreach (var item in items) result.Add(await MapProgramRequestAsync(item, ct));
        return result;
    }

    private async Task<IReadOnlyList<ManagerHandoverRequestDto>> MapPortfolioRequestsAsync(IEnumerable<PortfolioHandoverRequest> items, CancellationToken ct)
    {
        var result = new List<ManagerHandoverRequestDto>();
        foreach (var item in items) result.Add(await MapPortfolioRequestAsync(item, ct));
        return result;
    }

    private async Task<ManagerHandoverRequestDto> MapRequestAsync(Guid tenantId, Guid requestId, string type, Guid entityId, Guid? currentId, Guid newId, string initiationType, DateTime effectiveDate, string reason, string? notes, string status, Guid requestedBy, DateTime requestedAt, Guid? respondedBy, DateTime? respondedAt, string? rejectionReason, Guid? snapshotId, CancellationToken ct)
    {
        var ids = (currentId.HasValue ? new[] { currentId.Value } : Array.Empty<Guid>()).Concat(new[] { newId, requestedBy }).Concat(respondedBy.HasValue ? new[] { respondedBy.Value } : Array.Empty<Guid>()).Distinct().ToArray();
        var names = await _db.Set<User>().AsNoTracking().Where(x => ids.Contains(x.Id) && x.TenantId == tenantId).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var entityName = type == "Program" ? await _db.Set<Program>().Where(x => x.Id == entityId && x.TenantId == tenantId).Select(x => x.Name).FirstOrDefaultAsync(ct) : await _db.Set<PortfolioEntity>().Where(x => x.Id == entityId && x.TenantId == tenantId).Select(x => x.Name).FirstOrDefaultAsync(ct);
        return new ManagerHandoverRequestDto { Id = requestId, EntityType = type, EntityId = entityId, EntityName = entityName ?? string.Empty, CurrentManagerId = currentId, CurrentManagerName = currentId.HasValue ? names.GetValueOrDefault(currentId.Value) ?? string.Empty : string.Empty, NewManagerId = newId, NewManagerName = names.GetValueOrDefault(newId) ?? string.Empty, InitiationType = initiationType, EffectiveHandoverDate = effectiveDate, Reason = reason, Notes = notes, Status = status, RequestedBy = requestedBy, RequestedByName = names.GetValueOrDefault(requestedBy) ?? string.Empty, RequestedAt = requestedAt, RespondedBy = respondedBy, RespondedByName = respondedBy.HasValue ? names.GetValueOrDefault(respondedBy.Value) : null, RespondedAt = respondedAt, RejectionReason = rejectionReason, SnapshotId = snapshotId };
    }

    private async Task<IReadOnlyList<ManagerAssignmentDto>> MapAssignmentsAsync(Guid tenantId, List<(Guid Id, Guid EntityId, Guid ManagerId, DateTime StartDate, DateTime? EndDate, bool IsCurrent, DateTime? HandoverDate, string? HandoverReason, string? HandoverNotes)> items, CancellationToken ct)
    {
        var ids = items.Select(x => x.ManagerId).Distinct().ToArray(); var names = await _db.Set<User>().AsNoTracking().Where(x => ids.Contains(x.Id) && x.TenantId == tenantId).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        return items.Select(x => new ManagerAssignmentDto { Id = x.Id, EntityId = x.EntityId, ManagerId = x.ManagerId, ManagerName = names.GetValueOrDefault(x.ManagerId) ?? string.Empty, StartDate = x.StartDate, EndDate = x.EndDate, IsCurrent = x.IsCurrent, HandoverDate = x.HandoverDate, HandoverReason = x.HandoverReason, HandoverNotes = x.HandoverNotes }).ToList();
    }

    private async Task<IReadOnlyList<ManagerSnapshotDto>> MapSnapshotsAsync(Guid tenantId, List<(Guid Id, Guid EntityId, int SnapshotNumber, string Name, DateTime SnapshotDate, DateTime EffectiveEndDate, Guid? PreviousManagerId, Guid NewManagerId, string HandoverReason, string? HandoverNotes, string? Data)> items, CancellationToken ct)
    {
        var ids = items.SelectMany(x => x.PreviousManagerId.HasValue ? new[] { x.PreviousManagerId.Value, x.NewManagerId } : new[] { x.NewManagerId }).Distinct().ToArray(); var names = await _db.Set<User>().AsNoTracking().Where(x => ids.Contains(x.Id) && x.TenantId == tenantId).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        return items.Select(x => new ManagerSnapshotDto { Id = x.Id, EntityId = x.EntityId, SnapshotNumber = x.SnapshotNumber, Name = x.Name, SnapshotDate = x.SnapshotDate, EffectiveEndDate = x.EffectiveEndDate, PreviousManagerId = x.PreviousManagerId, PreviousManagerName = x.PreviousManagerId.HasValue ? names.GetValueOrDefault(x.PreviousManagerId.Value) ?? string.Empty : string.Empty, NewManagerId = x.NewManagerId, NewManagerName = names.GetValueOrDefault(x.NewManagerId) ?? string.Empty, HandoverReason = x.HandoverReason, HandoverNotes = x.HandoverNotes, Data = x.Data }).ToList();
    }
}
