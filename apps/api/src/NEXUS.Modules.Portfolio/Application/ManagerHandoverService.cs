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
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;

namespace NEXUS.Modules.Portfolio.Application;

public sealed class ManagerHandoverService : IManagerHandoverService
{
    private readonly DbContext _db;
    private readonly INotificationService _notifications;
    private readonly IUserEffectivePermissionService _effectivePermissions;

    public ManagerHandoverService(DbContext db, INotificationService notifications, IUserEffectivePermissionService effectivePermissions)
    {
        _db = db;
        _notifications = notifications;
        _effectivePermissions = effectivePermissions;
    }

    public async Task<Result<IReadOnlyList<ManagerHandoverManagerDto>>> GetEligibleProgramManagersAsync(Guid id, CancellationToken ct = default)
    {
        var program = await _db.Set<Program>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (program is null) return Result<IReadOnlyList<ManagerHandoverManagerDto>>.Failure(Error.NotFound("Program not found"));
        return Result<IReadOnlyList<ManagerHandoverManagerDto>>.Success(await GetEligibleManagersAsync(program.TenantId, program.ProgramManagerId, ct));
    }

    public async Task<Result<IReadOnlyList<ManagerHandoverManagerDto>>> GetEligiblePortfolioManagersAsync(Guid id, CancellationToken ct = default)
    {
        var portfolio = await _db.Set<PortfolioEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (portfolio is null) return Result<IReadOnlyList<ManagerHandoverManagerDto>>.Failure(Error.NotFound("Portfolio not found"));
        return Result<IReadOnlyList<ManagerHandoverManagerDto>>.Success(await GetEligibleManagersAsync(portfolio.TenantId, portfolio.OwnerId, ct));
    }

    private async Task<IReadOnlyList<ManagerHandoverManagerDto>> GetEligibleManagersAsync(Guid tenantId, Guid? currentManagerId, CancellationToken ct)
    {
        var roleAssignments = await (
            from assignment in _db.Set<UserRole>().AsNoTracking()
            join role in _db.Set<Role>().AsNoTracking() on assignment.RoleId equals role.Id
            where assignment.TenantId == tenantId && !assignment.IsDeleted
                && role.TenantId == tenantId && !role.IsDeleted
                && (role.Name == "project.manager" || role.Name == "portfolio.manager")
            select new { assignment.UserId, role.Name }
        ).ToListAsync(ct);
        var projectManagerIds = roleAssignments.Where(x => x.Name == "project.manager").Select(x => x.UserId).ToHashSet();
        var portfolioManagerIds = roleAssignments.Where(x => x.Name == "portfolio.manager").Select(x => x.UserId).ToHashSet();
        var assignedProgramManagerIds = await _db.Set<ProgramManagerAssignment>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsCurrent && !x.IsDeleted)
            .Select(x => x.ProgramManagerId).Distinct().ToListAsync(ct);
        var directProgramManagerIds = await _db.Set<Program>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.ProgramManagerId.HasValue)
            .Select(x => x.ProgramManagerId!.Value).Distinct().ToListAsync(ct);
        var assignedPortfolioManagerIds = await _db.Set<PortfolioManagerAssignment>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsCurrent && !x.IsDeleted)
            .Select(x => x.PortfolioManagerId).Distinct().ToListAsync(ct);
        var directPortfolioManagerIds = await _db.Set<PortfolioEntity>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.OwnerId.HasValue)
            .Select(x => x.OwnerId!.Value).Distinct().ToListAsync(ct);
        var programManagerIds = assignedProgramManagerIds.Concat(directProgramManagerIds).ToHashSet();
        var portfolioAssignmentIds = assignedPortfolioManagerIds.Concat(directPortfolioManagerIds).ToHashSet();
        var candidateIds = projectManagerIds.Concat(portfolioManagerIds).Concat(programManagerIds).Concat(portfolioAssignmentIds)
            .Where(id => id != currentManagerId).Distinct().ToArray();
        if (candidateIds.Length == 0) return Array.Empty<ManagerHandoverManagerDto>();

        var users = await _db.Set<User>().AsNoTracking()
            .Where(user => candidateIds.Contains(user.Id) && user.TenantId == tenantId && user.IsActive && !user.IsDeleted
                && (user.UserRoles.Any(assignment => assignment.TenantId == tenantId && !assignment.IsDeleted
                    && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted
                    && assignment.Role.Permissions.Any(permission => permission.TenantId == tenantId && !permission.IsDeleted && permission.Name == "Handover.Approve"))
                    || user.PermissionOverrides.Any(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.Permission.TenantId == tenantId && !item.Permission.IsDeleted
                        && item.Permission.Name == "Handover.Approve" && item.Effect == PermissionOverrideEffect.Allow))
                && !user.PermissionOverrides.Any(item => item.TenantId == tenantId && !item.IsDeleted
                    && item.Permission.TenantId == tenantId && !item.Permission.IsDeleted
                    && item.Permission.Name == "Handover.Approve" && item.Effect == PermissionOverrideEffect.Deny))
            .OrderBy(user => user.FirstName).ThenBy(user => user.LastName)
            .Select(user => new { user.Id, user.FirstName, user.LastName, user.Email, user.JobTitle })
            .ToListAsync(ct);

        return users.Select(user => new ManagerHandoverManagerDto
        {
            Id = user.Id,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email,
            JobTitle = user.JobTitle,
            ManagerTypes = Array.Empty<string>()
                .Concat(projectManagerIds.Contains(user.Id) ? new[] { "ProjectManager" } : Array.Empty<string>())
                .Concat(portfolioManagerIds.Contains(user.Id) || portfolioAssignmentIds.Contains(user.Id) ? new[] { "PortfolioManager" } : Array.Empty<string>())
                .Concat(programManagerIds.Contains(user.Id) ? new[] { "ProgramManager" } : Array.Empty<string>())
                .ToArray()
        }).ToList();
    }

    public Task<Result<ManagerHandoverRequestDto>> CreateProgramAsync(Guid id, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default) =>
        CreateProgramCoreAsync(id, dto, userId, ct);

    public Task<Result<ManagerHandoverRequestDto>> CreatePortfolioAsync(Guid id, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default) =>
        CreatePortfolioCoreAsync(id, dto, userId, ct);

    private async Task<Result<ManagerHandoverRequestDto>> CreateProgramCoreAsync(Guid id, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct)
    {
        var program = await _db.Set<Program>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (program is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Program not found"));
        if (!await HasPermissionAsync(userId, program.TenantId, "Handover.Manage", ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to manage handovers"));
        var validation = Validate(dto, program.ProgramManagerId);
        if (validation is not null) return Result<ManagerHandoverRequestDto>.Failure(validation);
        if (program.ProgramManagerId != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the current Program Manager can request a handover"));
        if (!(await GetEligibleManagersAsync(program.TenantId, program.ProgramManagerId, ct)).Any(x => x.Id == dto.NewManagerId)) return Result<ManagerHandoverRequestDto>.Failure(Error.Validation("Select an eligible active manager in this tenant who can approve the handover"));
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await _db.Set<ProgramHandoverRequest>().AnyAsync(x => x.ProgramId == id && x.TenantId == program.TenantId && x.Status == ManagerHandoverStatus.Pending, ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("A pending handover request already exists"));
        await EnsureProgramAssignmentAsync(program, userId, ct);
        var request = new ProgramHandoverRequest { TenantId = program.TenantId, ProgramId = id, CurrentProgramManagerId = userId, NewProgramManagerId = dto.NewManagerId, EffectiveHandoverDate = dto.EffectiveHandoverDate, Reason = dto.Reason.Trim(), Notes = dto.Notes?.Trim(), RequestedBy = userId, CreatedBy = userId.ToString() };
        _db.Set<ProgramHandoverRequest>().Add(request);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await NotifyRequestedAsync("Program", id, program.Name, userId, dto.NewManagerId, request.Id, ct);
        return Result<ManagerHandoverRequestDto>.Success(await MapProgramRequestAsync(request, ct));
    }

    private async Task<Result<ManagerHandoverRequestDto>> CreatePortfolioCoreAsync(Guid id, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct)
    {
        var portfolio = await _db.Set<PortfolioEntity>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (portfolio is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Portfolio not found"));
        if (!await HasPermissionAsync(userId, portfolio.TenantId, "Handover.Manage", ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to manage handovers"));
        var validation = Validate(dto, portfolio.OwnerId);
        if (validation is not null) return Result<ManagerHandoverRequestDto>.Failure(validation);
        if (portfolio.OwnerId != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the current Portfolio Manager can request a handover"));
        if (!(await GetEligibleManagersAsync(portfolio.TenantId, portfolio.OwnerId, ct)).Any(x => x.Id == dto.NewManagerId)) return Result<ManagerHandoverRequestDto>.Failure(Error.Validation("Select an eligible active manager in this tenant who can approve the handover"));
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await _db.Set<PortfolioHandoverRequest>().AnyAsync(x => x.PortfolioId == id && x.TenantId == portfolio.TenantId && x.Status == ManagerHandoverStatus.Pending, ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("A pending handover request already exists"));
        await EnsurePortfolioAssignmentAsync(portfolio, userId, ct);
        var request = new PortfolioHandoverRequest { TenantId = portfolio.TenantId, PortfolioId = id, CurrentPortfolioManagerId = userId, NewPortfolioManagerId = dto.NewManagerId, EffectiveHandoverDate = dto.EffectiveHandoverDate, Reason = dto.Reason.Trim(), Notes = dto.Notes?.Trim(), RequestedBy = userId, CreatedBy = userId.ToString() };
        _db.Set<PortfolioHandoverRequest>().Add(request);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await NotifyRequestedAsync("Portfolio", id, portfolio.Name, userId, dto.NewManagerId, request.Id, ct);
        return Result<ManagerHandoverRequestDto>.Success(await MapPortfolioRequestAsync(request, ct));
    }

    public async Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetProgramRequestsAsync(Guid id, CancellationToken ct = default)
    {
        var exists = await _db.Set<Program>().AnyAsync(x => x.Id == id, ct);
        if (!exists) return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Failure(Error.NotFound("Program not found"));
        var requests = await _db.Set<ProgramHandoverRequest>().Where(x => x.ProgramId == id).OrderByDescending(x => x.RequestedAt).ToListAsync(ct);
        return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Success(await MapProgramRequestsAsync(requests, ct));
    }

    public async Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetPortfolioRequestsAsync(Guid id, CancellationToken ct = default)
    {
        var exists = await _db.Set<PortfolioEntity>().AnyAsync(x => x.Id == id, ct);
        if (!exists) return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Failure(Error.NotFound("Portfolio not found"));
        var requests = await _db.Set<PortfolioHandoverRequest>().Where(x => x.PortfolioId == id).OrderByDescending(x => x.RequestedAt).ToListAsync(ct);
        return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Success(await MapPortfolioRequestsAsync(requests, ct));
    }

    public async Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetPendingForMeAsync(Guid userId, CancellationToken ct = default)
    {
        var programs = await _db.Set<ProgramHandoverRequest>().Where(x => x.NewProgramManagerId == userId && x.Status == ManagerHandoverStatus.Pending).OrderByDescending(x => x.RequestedAt).ToListAsync(ct);
        var portfolios = await _db.Set<PortfolioHandoverRequest>().Where(x => x.NewPortfolioManagerId == userId && x.Status == ManagerHandoverStatus.Pending).OrderByDescending(x => x.RequestedAt).ToListAsync(ct);
        var result = new List<ManagerHandoverRequestDto>(programs.Count + portfolios.Count);
        result.AddRange(await MapProgramRequestsAsync(programs, ct));
        result.AddRange(await MapPortfolioRequestsAsync(portfolios, ct));
        return Result<IReadOnlyList<ManagerHandoverRequestDto>>.Success(result.OrderByDescending(x => x.RequestedAt).ToList());
    }

    public Task<Result<ManagerHandoverRequestDto>> AcceptProgramAsync(Guid id, Guid userId, CancellationToken ct = default) => AcceptProgramCoreAsync(id, userId, ct);
    public Task<Result<ManagerHandoverRequestDto>> AcceptPortfolioAsync(Guid id, Guid userId, CancellationToken ct = default) => AcceptPortfolioCoreAsync(id, userId, ct);

    private async Task<Result<ManagerHandoverRequestDto>> AcceptProgramCoreAsync(Guid id, Guid userId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await _db.Set<ProgramHandoverRequest>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Handover request not found"));
        if (request.Status != ManagerHandoverStatus.Pending) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("Only pending handover requests can be accepted"));
        if (request.NewProgramManagerId != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the selected new manager can accept this request"));
        if (!await HasPermissionAsync(userId, request.TenantId, "Handover.Approve", ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to approve handovers"));
        var program = await _db.Set<Program>().FirstOrDefaultAsync(x => x.Id == request.ProgramId, ct);
        if (program is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Program not found"));
        if (program.ProgramManagerId != request.CurrentProgramManagerId) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("The current Program Manager has changed"));
        var current = await EnsureProgramAssignmentAsync(program, userId, ct);
        if (current.ProgramManagerId != request.CurrentProgramManagerId) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("The current Program Manager assignment has changed"));
        var number = await NextProgramSnapshotAsync(program.Id, ct);
        var snapshot = new ProgramSnapshot { TenantId = program.TenantId, ProgramId = program.Id, SnapshotNumber = number, Name = $"{program.Name} - Handover #{number}", EffectiveEndDate = request.EffectiveHandoverDate, PreviousProgramManagerId = current.ProgramManagerId, NewProgramManagerId = request.NewProgramManagerId, HandoverReason = request.Reason, HandoverNotes = request.Notes, Data = JsonSerializer.Serialize(new { program.Name, program.Code, program.Description, program.Status, program.PortfolioId, program.Budget, program.ActualSpend, program.Health }), CreatedBy = userId.ToString() };
        _db.Set<ProgramSnapshot>().Add(snapshot);
        current.EndDate = request.EffectiveHandoverDate; current.IsCurrent = false; current.HandoverDate = request.EffectiveHandoverDate; current.HandoverReason = request.Reason; current.HandoverNotes = request.Notes; current.UpdatedBy = userId.ToString();
        _db.Set<ProgramManagerAssignment>().Add(new ProgramManagerAssignment { TenantId = program.TenantId, ProgramId = program.Id, ProgramManagerId = request.NewProgramManagerId, StartDate = request.EffectiveHandoverDate, IsCurrent = true, HandoverFromProgramManagerId = current.ProgramManagerId, HandoverDate = request.EffectiveHandoverDate, HandoverReason = request.Reason, HandoverNotes = request.Notes, CreatedBy = userId.ToString() });
        program.ProgramManagerId = request.NewProgramManagerId; program.UpdatedBy = userId.ToString();
        request.Status = ManagerHandoverStatus.Accepted; request.RespondedBy = userId; request.RespondedAt = DateTime.UtcNow; request.SnapshotId = snapshot.Id; request.UpdatedBy = userId.ToString();
        AddAudit("Program", program.Id, "ManagerHandoverAccepted", request.CurrentProgramManagerId, userId, request.Reason, program.TenantId);
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await NotifyCompletedAsync("Program", program.Id, program.Name, request.CurrentProgramManagerId, request.NewProgramManagerId, request.Id, ManagerHandoverStatus.Accepted, ct);
        return Result<ManagerHandoverRequestDto>.Success(await MapProgramRequestAsync(request, ct));
    }

    private async Task<Result<ManagerHandoverRequestDto>> AcceptPortfolioCoreAsync(Guid id, Guid userId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await _db.Set<PortfolioHandoverRequest>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Handover request not found"));
        if (request.Status != ManagerHandoverStatus.Pending) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("Only pending handover requests can be accepted"));
        if (request.NewPortfolioManagerId != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the selected new manager can accept this request"));
        if (!await HasPermissionAsync(userId, request.TenantId, "Handover.Approve", ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to approve handovers"));
        var portfolio = await _db.Set<PortfolioEntity>().FirstOrDefaultAsync(x => x.Id == request.PortfolioId, ct);
        if (portfolio is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Portfolio not found"));
        if (portfolio.OwnerId != request.CurrentPortfolioManagerId) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("The current Portfolio Manager has changed"));
        var current = await EnsurePortfolioAssignmentAsync(portfolio, userId, ct);
        if (current.PortfolioManagerId != request.CurrentPortfolioManagerId) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("The current Portfolio Manager assignment has changed"));
        var number = await NextPortfolioSnapshotAsync(portfolio.Id, ct);
        var snapshot = new PortfolioSnapshot { TenantId = portfolio.TenantId, PortfolioId = portfolio.Id, SnapshotNumber = number, Name = $"{portfolio.Name} - Handover #{number}", EffectiveEndDate = request.EffectiveHandoverDate, PreviousPortfolioManagerId = current.PortfolioManagerId, NewPortfolioManagerId = request.NewPortfolioManagerId, HandoverReason = request.Reason, HandoverNotes = request.Notes, Data = JsonSerializer.Serialize(new { portfolio.Name, portfolio.Code, portfolio.Description, portfolio.Status, portfolio.OwnerId, portfolio.OrganizationId, portfolio.Currency, portfolio.Health, portfolio.Priority }), CreatedBy = userId.ToString() };
        _db.Set<PortfolioSnapshot>().Add(snapshot);
        current.EndDate = request.EffectiveHandoverDate; current.IsCurrent = false; current.HandoverDate = request.EffectiveHandoverDate; current.HandoverReason = request.Reason; current.HandoverNotes = request.Notes; current.UpdatedBy = userId.ToString();
        _db.Set<PortfolioManagerAssignment>().Add(new PortfolioManagerAssignment { TenantId = portfolio.TenantId, PortfolioId = portfolio.Id, PortfolioManagerId = request.NewPortfolioManagerId, StartDate = request.EffectiveHandoverDate, IsCurrent = true, HandoverFromPortfolioManagerId = current.PortfolioManagerId, HandoverDate = request.EffectiveHandoverDate, HandoverReason = request.Reason, HandoverNotes = request.Notes, CreatedBy = userId.ToString() });
        portfolio.OwnerId = request.NewPortfolioManagerId; portfolio.UpdatedBy = userId.ToString();
        request.Status = ManagerHandoverStatus.Accepted; request.RespondedBy = userId; request.RespondedAt = DateTime.UtcNow; request.SnapshotId = snapshot.Id; request.UpdatedBy = userId.ToString();
        AddAudit("Portfolio", portfolio.Id, "ManagerHandoverAccepted", request.CurrentPortfolioManagerId, userId, request.Reason, portfolio.TenantId);
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await NotifyCompletedAsync("Portfolio", portfolio.Id, portfolio.Name, request.CurrentPortfolioManagerId, request.NewPortfolioManagerId, request.Id, ManagerHandoverStatus.Accepted, ct);
        return Result<ManagerHandoverRequestDto>.Success(await MapPortfolioRequestAsync(request, ct));
    }

    public Task<Result<ManagerHandoverRequestDto>> RejectProgramAsync(Guid id, RejectManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default) => ChangeProgramStatusAsync(id, dto.RejectionReason, ManagerHandoverStatus.Rejected, userId, ct);
    public Task<Result<ManagerHandoverRequestDto>> RejectPortfolioAsync(Guid id, RejectManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default) => ChangePortfolioStatusAsync(id, dto.RejectionReason, ManagerHandoverStatus.Rejected, userId, ct);
    public Task<Result<ManagerHandoverRequestDto>> CancelProgramAsync(Guid id, Guid userId, CancellationToken ct = default) => ChangeProgramStatusAsync(id, null, ManagerHandoverStatus.Cancelled, userId, ct);
    public Task<Result<ManagerHandoverRequestDto>> CancelPortfolioAsync(Guid id, Guid userId, CancellationToken ct = default) => ChangePortfolioStatusAsync(id, null, ManagerHandoverStatus.Cancelled, userId, ct);

    private async Task<Result<ManagerHandoverRequestDto>> ChangeProgramStatusAsync(Guid id, string? reason, string status, Guid userId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await _db.Set<ProgramHandoverRequest>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Handover request not found"));
        if (request.Status != ManagerHandoverStatus.Pending) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("Only pending handover requests can be changed"));
        if (status == ManagerHandoverStatus.Rejected && request.NewProgramManagerId != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the selected new manager can reject this request"));
        var requiredPermission = status == ManagerHandoverStatus.Rejected ? "Handover.Approve" : "Handover.Manage";
        if (!await HasPermissionAsync(userId, request.TenantId, requiredPermission, ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to change this handover request"));
        if (status == ManagerHandoverStatus.Cancelled && request.RequestedBy != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the requester can cancel this request"));
        if (status == ManagerHandoverStatus.Rejected && string.IsNullOrWhiteSpace(reason)) return Result<ManagerHandoverRequestDto>.Failure(Error.Validation("A rejection reason is required"));
        request.Status = status; request.RejectionReason = reason?.Trim(); request.RespondedBy = userId; request.RespondedAt = DateTime.UtcNow; request.UpdatedBy = userId.ToString();
        AddAudit("Program", request.ProgramId, $"ManagerHandover{status}", request.RequestedBy, userId, reason ?? request.Reason, request.TenantId);
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await NotifyCompletedAsync("Program", request.ProgramId, (await _db.Set<Program>().AsNoTracking().FirstAsync(x => x.Id == request.ProgramId, ct)).Name, request.CurrentProgramManagerId, request.NewProgramManagerId, request.Id, status, ct);
        return Result<ManagerHandoverRequestDto>.Success(await MapProgramRequestAsync(request, ct));
    }

    private async Task<Result<ManagerHandoverRequestDto>> ChangePortfolioStatusAsync(Guid id, string? reason, string status, Guid userId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await _db.Set<PortfolioHandoverRequest>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return Result<ManagerHandoverRequestDto>.Failure(Error.NotFound("Handover request not found"));
        if (request.Status != ManagerHandoverStatus.Pending) return Result<ManagerHandoverRequestDto>.Failure(Error.Conflict("Only pending handover requests can be changed"));
        if (status == ManagerHandoverStatus.Rejected && request.NewPortfolioManagerId != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the selected new manager can reject this request"));
        var requiredPermission = status == ManagerHandoverStatus.Rejected ? "Handover.Approve" : "Handover.Manage";
        if (!await HasPermissionAsync(userId, request.TenantId, requiredPermission, ct)) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to change this handover request"));
        if (status == ManagerHandoverStatus.Cancelled && request.RequestedBy != userId) return Result<ManagerHandoverRequestDto>.Failure(Error.Forbidden("Only the requester can cancel this request"));
        if (status == ManagerHandoverStatus.Rejected && string.IsNullOrWhiteSpace(reason)) return Result<ManagerHandoverRequestDto>.Failure(Error.Validation("A rejection reason is required"));
        request.Status = status; request.RejectionReason = reason?.Trim(); request.RespondedBy = userId; request.RespondedAt = DateTime.UtcNow; request.UpdatedBy = userId.ToString();
        AddAudit("Portfolio", request.PortfolioId, $"ManagerHandover{status}", request.RequestedBy, userId, reason ?? request.Reason, request.TenantId);
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await NotifyCompletedAsync("Portfolio", request.PortfolioId, (await _db.Set<PortfolioEntity>().AsNoTracking().FirstAsync(x => x.Id == request.PortfolioId, ct)).Name, request.CurrentPortfolioManagerId, request.NewPortfolioManagerId, request.Id, status, ct);
        return Result<ManagerHandoverRequestDto>.Success(await MapPortfolioRequestAsync(request, ct));
    }

    public async Task<Result<IReadOnlyList<ManagerAssignmentDto>>> GetProgramHistoryAsync(Guid id, CancellationToken ct = default)
    {
        var items = await _db.Set<ProgramManagerAssignment>().Where(x => x.ProgramId == id).OrderByDescending(x => x.StartDate).ToListAsync(ct);
        return Result<IReadOnlyList<ManagerAssignmentDto>>.Success(await MapAssignmentsAsync(items.Select(x => (x.Id, id, x.ProgramManagerId, x.StartDate, x.EndDate, x.IsCurrent, x.HandoverDate, x.HandoverReason, x.HandoverNotes)).ToList(), ct));
    }

    public async Task<Result<IReadOnlyList<ManagerAssignmentDto>>> GetPortfolioHistoryAsync(Guid id, CancellationToken ct = default)
    {
        var items = await _db.Set<PortfolioManagerAssignment>().Where(x => x.PortfolioId == id).OrderByDescending(x => x.StartDate).ToListAsync(ct);
        return Result<IReadOnlyList<ManagerAssignmentDto>>.Success(await MapAssignmentsAsync(items.Select(x => (x.Id, id, x.PortfolioManagerId, x.StartDate, x.EndDate, x.IsCurrent, x.HandoverDate, x.HandoverReason, x.HandoverNotes)).ToList(), ct));
    }

    public async Task<Result<IReadOnlyList<ManagerSnapshotDto>>> GetProgramSnapshotsAsync(Guid id, CancellationToken ct = default)
    {
        var items = await _db.Set<ProgramSnapshot>().Where(x => x.ProgramId == id).OrderByDescending(x => x.SnapshotNumber).ToListAsync(ct);
        return Result<IReadOnlyList<ManagerSnapshotDto>>.Success(await MapSnapshotsAsync(items.Select(x => (x.Id, id, x.SnapshotNumber, x.Name, x.SnapshotDate, x.EffectiveEndDate, x.PreviousProgramManagerId, x.NewProgramManagerId, x.HandoverReason, x.HandoverNotes, x.Data)).ToList(), ct));
    }

    public async Task<Result<IReadOnlyList<ManagerSnapshotDto>>> GetPortfolioSnapshotsAsync(Guid id, CancellationToken ct = default)
    {
        var items = await _db.Set<PortfolioSnapshot>().Where(x => x.PortfolioId == id).OrderByDescending(x => x.SnapshotNumber).ToListAsync(ct);
        return Result<IReadOnlyList<ManagerSnapshotDto>>.Success(await MapSnapshotsAsync(items.Select(x => (x.Id, id, x.SnapshotNumber, x.Name, x.SnapshotDate, x.EffectiveEndDate, x.PreviousPortfolioManagerId, x.NewPortfolioManagerId, x.HandoverReason, x.HandoverNotes, x.Data)).ToList(), ct));
    }

    private static Error? Validate(CreateManagerHandoverRequestDto dto, Guid? currentId) => currentId is null || currentId == Guid.Empty ? Error.Validation("The entity must have a current manager") : dto.NewManagerId == Guid.Empty || dto.NewManagerId == currentId ? Error.Validation("A different new manager is required") : dto.EffectiveHandoverDate == default ? Error.Validation("Effective handover date is required") : string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Trim().Length > 500 ? Error.Validation("Handover reason is required and limited to 500 characters") : dto.Notes?.Trim().Length > 2000 ? Error.Validation("Handover notes are limited to 2000 characters") : null;
    private async Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permission, CancellationToken ct)
    {
        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, ct);
        return permissions.Contains(permission);
    }

    private async Task<ProgramManagerAssignment> EnsureProgramAssignmentAsync(Program program, Guid actor, CancellationToken ct)
    {
        var current = await _db.Set<ProgramManagerAssignment>().FirstOrDefaultAsync(x => x.ProgramId == program.Id && x.IsCurrent, ct);
        if (current is not null) return current;
        current = new ProgramManagerAssignment { TenantId = program.TenantId, ProgramId = program.Id, ProgramManagerId = program.ProgramManagerId!.Value, StartDate = program.StartDate ?? program.CreatedAt, IsCurrent = true, CreatedBy = actor.ToString() };
        _db.Set<ProgramManagerAssignment>().Add(current); return current;
    }

    private async Task<PortfolioManagerAssignment> EnsurePortfolioAssignmentAsync(PortfolioEntity portfolio, Guid actor, CancellationToken ct)
    {
        var current = await _db.Set<PortfolioManagerAssignment>().FirstOrDefaultAsync(x => x.PortfolioId == portfolio.Id && x.IsCurrent, ct);
        if (current is not null) return current;
        current = new PortfolioManagerAssignment { TenantId = portfolio.TenantId, PortfolioId = portfolio.Id, PortfolioManagerId = portfolio.OwnerId!.Value, StartDate = portfolio.StartDate ?? portfolio.CreatedAt, IsCurrent = true, CreatedBy = actor.ToString() };
        _db.Set<PortfolioManagerAssignment>().Add(current); return current;
    }

    private Task<int> NextProgramSnapshotAsync(Guid id, CancellationToken ct) => _db.Set<ProgramSnapshot>().Where(x => x.ProgramId == id).Select(x => (int?)x.SnapshotNumber).MaxAsync(ct).ContinueWith(t => (t.Result ?? 0) + 1, ct);
    private Task<int> NextPortfolioSnapshotAsync(Guid id, CancellationToken ct) => _db.Set<PortfolioSnapshot>().Where(x => x.PortfolioId == id).Select(x => (int?)x.SnapshotNumber).MaxAsync(ct).ContinueWith(t => (t.Result ?? 0) + 1, ct);

    private void AddAudit(string type, Guid entityId, string action, Guid requestedBy, Guid actor, string reason, Guid tenantId) => _db.Set<AuditEntry>().Add(new AuditEntry { TenantId = tenantId, EntityType = type, EntityId = entityId, Action = action, OriginalValues = JsonSerializer.Serialize(new { requestedBy, reason }), UserId = actor, Timestamp = DateTime.UtcNow, CreatedBy = actor.ToString() });

    private async Task NotifyRequestedAsync(string type, Guid entityId, string name, Guid currentId, Guid newId, Guid requestId, CancellationToken ct)
    {
        await SendNotificationAsync(newId, $"{type} manager handover requested", $"You have a pending manager handover request for {name}.", type, entityId, requestId, ct);
        if (currentId != newId) await SendNotificationAsync(currentId, $"{type} manager handover submitted", $"A manager handover request for {name} is pending your selected manager's response.", type, entityId, requestId, ct);
    }

    private async Task NotifyCompletedAsync(string type, Guid entityId, string name, Guid currentId, Guid newId, Guid requestId, string status, CancellationToken ct)
    {
        await SendNotificationAsync(currentId, $"{type} manager handover {status.ToLowerInvariant()}", $"The manager handover request for {name} is now {status}.", type, entityId, requestId, ct);
        if (newId != currentId) await SendNotificationAsync(newId, $"{type} manager handover {status.ToLowerInvariant()}", $"The manager handover request for {name} is now {status}.", type, entityId, requestId, ct);
    }

    private async Task SendNotificationAsync(Guid userId, string title, string body, string type, Guid entityId, Guid requestId, CancellationToken ct) => await _notifications.SendAsync(new Notification { TenantId = (await _db.Set<User>().AsNoTracking().Where(x => x.Id == userId).Select(x => (Guid?)x.TenantId).FirstOrDefaultAsync(ct)) ?? Guid.Empty, UserId = userId, Title = title, Body = body, Type = type == "Program" ? NotificationTypes.ProgramHandover : NotificationTypes.PortfolioHandover, ReferenceType = type, ReferenceId = requestId, CreatedBy = userId.ToString() }, ct);

    private async Task<ManagerHandoverRequestDto> MapProgramRequestAsync(ProgramHandoverRequest x, CancellationToken ct) => await MapRequestAsync(x.Id, "Program", x.ProgramId, x.CurrentProgramManagerId, x.NewProgramManagerId, x.EffectiveHandoverDate, x.Reason, x.Notes, x.Status, x.RequestedBy, x.RequestedAt, x.RespondedBy, x.RespondedAt, x.RejectionReason, x.SnapshotId, ct);
    private async Task<ManagerHandoverRequestDto> MapPortfolioRequestAsync(PortfolioHandoverRequest x, CancellationToken ct) => await MapRequestAsync(x.Id, "Portfolio", x.PortfolioId, x.CurrentPortfolioManagerId, x.NewPortfolioManagerId, x.EffectiveHandoverDate, x.Reason, x.Notes, x.Status, x.RequestedBy, x.RequestedAt, x.RespondedBy, x.RespondedAt, x.RejectionReason, x.SnapshotId, ct);
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

    private async Task<ManagerHandoverRequestDto> MapRequestAsync(Guid requestId, string type, Guid entityId, Guid currentId, Guid newId, DateTime effectiveDate, string reason, string? notes, string status, Guid requestedBy, DateTime requestedAt, Guid? respondedBy, DateTime? respondedAt, string? rejectionReason, Guid? snapshotId, CancellationToken ct)
    {
        var ids = new[] { currentId, newId, requestedBy }.Concat(respondedBy.HasValue ? new[] { respondedBy.Value } : Array.Empty<Guid>()).Distinct().ToArray();
        var names = await _db.Set<User>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var entityName = type == "Program" ? await _db.Set<Program>().Where(x => x.Id == entityId).Select(x => x.Name).FirstOrDefaultAsync(ct) : await _db.Set<PortfolioEntity>().Where(x => x.Id == entityId).Select(x => x.Name).FirstOrDefaultAsync(ct);
        return new ManagerHandoverRequestDto { Id = requestId, EntityType = type, EntityId = entityId, EntityName = entityName ?? string.Empty, CurrentManagerId = currentId, CurrentManagerName = names.GetValueOrDefault(currentId) ?? string.Empty, NewManagerId = newId, NewManagerName = names.GetValueOrDefault(newId) ?? string.Empty, EffectiveHandoverDate = effectiveDate, Reason = reason, Notes = notes, Status = status, RequestedBy = requestedBy, RequestedByName = names.GetValueOrDefault(requestedBy) ?? string.Empty, RequestedAt = requestedAt, RespondedBy = respondedBy, RespondedByName = respondedBy.HasValue ? names.GetValueOrDefault(respondedBy.Value) : null, RespondedAt = respondedAt, RejectionReason = rejectionReason, SnapshotId = snapshotId };
    }

    private async Task<IReadOnlyList<ManagerAssignmentDto>> MapAssignmentsAsync(List<(Guid Id, Guid EntityId, Guid ManagerId, DateTime StartDate, DateTime? EndDate, bool IsCurrent, DateTime? HandoverDate, string? HandoverReason, string? HandoverNotes)> items, CancellationToken ct)
    {
        var ids = items.Select(x => x.ManagerId).Distinct().ToArray(); var names = await _db.Set<User>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        return items.Select(x => new ManagerAssignmentDto { Id = x.Id, EntityId = x.EntityId, ManagerId = x.ManagerId, ManagerName = names.GetValueOrDefault(x.ManagerId) ?? string.Empty, StartDate = x.StartDate, EndDate = x.EndDate, IsCurrent = x.IsCurrent, HandoverDate = x.HandoverDate, HandoverReason = x.HandoverReason, HandoverNotes = x.HandoverNotes }).ToList();
    }

    private async Task<IReadOnlyList<ManagerSnapshotDto>> MapSnapshotsAsync(List<(Guid Id, Guid EntityId, int SnapshotNumber, string Name, DateTime SnapshotDate, DateTime EffectiveEndDate, Guid PreviousManagerId, Guid NewManagerId, string HandoverReason, string? HandoverNotes, string? Data)> items, CancellationToken ct)
    {
        var ids = items.SelectMany(x => new[] { x.PreviousManagerId, x.NewManagerId }).Distinct().ToArray(); var names = await _db.Set<User>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        return items.Select(x => new ManagerSnapshotDto { Id = x.Id, EntityId = x.EntityId, SnapshotNumber = x.SnapshotNumber, Name = x.Name, SnapshotDate = x.SnapshotDate, EffectiveEndDate = x.EffectiveEndDate, PreviousManagerId = x.PreviousManagerId, PreviousManagerName = names.GetValueOrDefault(x.PreviousManagerId) ?? string.Empty, NewManagerId = x.NewManagerId, NewManagerName = names.GetValueOrDefault(x.NewManagerId) ?? string.Empty, HandoverReason = x.HandoverReason, HandoverNotes = x.HandoverNotes, Data = x.Data }).ToList();
    }
}
