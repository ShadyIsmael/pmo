using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.ProjectManagement.Application;

public class ProjectHandoverRequestService : IProjectHandoverRequestService
{
    private readonly DbContext _context;
    private readonly IProjectSnapshotService _snapshotService;
    private readonly INotificationService _notificationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserEffectivePermissionService _effectivePermissions;

    public ProjectHandoverRequestService(
        DbContext context,
        IProjectSnapshotService snapshotService,
        INotificationService notificationService,
        IHttpContextAccessor httpContextAccessor,
        IUserEffectivePermissionService effectivePermissions)
    {
        _context = context;
        _snapshotService = snapshotService;
        _notificationService = notificationService;
        _httpContextAccessor = httpContextAccessor;
        _effectivePermissions = effectivePermissions;
    }

    public async Task<Result<IReadOnlyList<ProjectHandoverManagerDto>>> GetEligibleManagersAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var tenantResult = GetRequiredCurrentTenantId();
        if (tenantResult.IsFailure)
        {
            return Result<IReadOnlyList<ProjectHandoverManagerDto>>.Failure(tenantResult.Error!);
        }
        var tenantId = tenantResult.Value;
        var project = await _context.Set<Project>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == tenantId && !p.IsDeleted, cancellationToken);
        if (project is null)
        {
            return Result<IReadOnlyList<ProjectHandoverManagerDto>>.Failure(Error.NotFound("Project not found"));
        }

        var actorId = TryParseGuid(_httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty);
        if (!actorId.HasValue || !await IsActiveTenantUserAsync(actorId.Value, tenantId, cancellationToken)
            || !await HasAnyPermissionAsync(actorId.Value, tenantId, cancellationToken, "Handover.Manage", "Handover.AdminInitiate"))
            return Result<IReadOnlyList<ProjectHandoverManagerDto>>.Failure(Error.Forbidden("You do not have permission to initiate handovers"));

        var roleAssignments = await (
            from userRole in _context.Set<UserRole>().AsNoTracking()
            join role in _context.Set<Role>().AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.TenantId == tenantId && !userRole.IsDeleted
                && role.TenantId == tenantId && !role.IsDeleted
                && (role.Name == "project.manager" || role.Name == "portfolio.manager")
            select new { userRole.UserId, role.Name }
        ).ToListAsync(cancellationToken);

        var projectManagerIds = roleAssignments
            .Where(item => item.Name == "project.manager")
            .Select(item => item.UserId)
            .ToHashSet();
        var portfolioManagerIds = roleAssignments
            .Where(item => item.Name == "portfolio.manager")
            .Select(item => item.UserId)
            .ToHashSet();

        var assignedProgramManagerIds = await _context.Set<ProgramManagerAssignment>().AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId && !assignment.IsDeleted && assignment.IsCurrent
                && _context.Set<Program>().Any(program => program.Id == assignment.ProgramId
                    && program.TenantId == tenantId && !program.IsDeleted))
            .Select(assignment => assignment.ProgramManagerId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var directProgramManagerIds = await _context.Set<Program>().AsNoTracking()
            .Where(program => program.TenantId == tenantId && !program.IsDeleted && program.ProgramManagerId.HasValue)
            .Select(program => program.ProgramManagerId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var programManagerIds = assignedProgramManagerIds.Concat(directProgramManagerIds).ToHashSet();

        var candidateIds = projectManagerIds.Concat(portfolioManagerIds).Concat(programManagerIds)
            .Where(id => id != project.ProjectManagerId)
            .Distinct()
            .ToArray();
        if (candidateIds.Length == 0)
        {
            return Result<IReadOnlyList<ProjectHandoverManagerDto>>.Success(Array.Empty<ProjectHandoverManagerDto>());
        }

        var users = await _context.Set<User>().AsNoTracking()
            .Where(user => candidateIds.Contains(user.Id) && user.TenantId == tenantId && user.IsActive && !user.IsDeleted)
            .OrderBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .ToListAsync(cancellationToken);

        var approvers = new List<User>();
        foreach (var user in users)
        {
            var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(user.Id, tenantId, cancellationToken);
            if (permissions.Contains("Handover.Approve")) approvers.Add(user);
        }

        IReadOnlyList<ProjectHandoverManagerDto> managers = approvers.Select(user => new ProjectHandoverManagerDto
        {
            Id = user.Id,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email,
            JobTitle = user.JobTitle,
            ManagerTypes = Array.Empty<string>()
                .Concat(projectManagerIds.Contains(user.Id) ? new[] { "ProjectManager" } : Array.Empty<string>())
                .Concat(portfolioManagerIds.Contains(user.Id) ? new[] { "PortfolioManager" } : Array.Empty<string>())
                .Concat(programManagerIds.Contains(user.Id) ? new[] { "ProgramManager" } : Array.Empty<string>())
                .ToArray()
        }).ToArray();

        return Result<IReadOnlyList<ProjectHandoverManagerDto>>.Success(managers);
    }

    public Task<Result<ProjectHandoverRequestDto>> CreateAsync(Guid projectId, CreateProjectHandoverRequestDto dto, string userId, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(projectId, dto, userId, ManagerHandoverInitiationType.ManagerInitiated, cancellationToken);

    public Task<Result<ProjectHandoverRequestDto>> CreateAdminInitiatedAsync(Guid projectId, CreateProjectHandoverRequestDto dto, string userId, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(projectId, dto, userId, ManagerHandoverInitiationType.AdminInitiated, cancellationToken);

    public Task<Result<ProjectHandoverRequestDto>> CreateInitialAssignmentAsync(Guid projectId, CreateProjectHandoverRequestDto dto, string userId, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(projectId, dto, userId, ManagerHandoverInitiationType.InitialAssignment, cancellationToken);

    private async Task<Result<ProjectHandoverRequestDto>> CreateCoreAsync(Guid projectId, CreateProjectHandoverRequestDto dto, string userId, string initiationType, CancellationToken cancellationToken)
    {
        var tenantResult = GetRequiredCurrentTenantId();
        if (tenantResult.IsFailure)
        {
            return Result<ProjectHandoverRequestDto>.Failure(tenantResult.Error!);
        }
        var tenantId = tenantResult.Value;
        var actorId = TryParseGuid(userId);
        if (!actorId.HasValue) return Result<ProjectHandoverRequestDto>.Failure(Error.Unauthorized("Authenticated user ID is missing or invalid."));
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var project = await _context.Set<Project>()
            .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == tenantId && !p.IsDeleted, cancellationToken);
        if (project is null) return Result<ProjectHandoverRequestDto>.Failure(Error.NotFound("Project not found"));

        var currentManagerId = project.ProjectManagerId;
        var requiredPermission = initiationType == ManagerHandoverInitiationType.ManagerInitiated ? "Handover.Manage" : "Handover.AdminInitiate";
        if (!await HasPermissionAsync(actorId.Value, tenantId, requiredPermission, cancellationToken))
            return Result<ProjectHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to initiate this handover"));
        if (initiationType == ManagerHandoverInitiationType.ManagerInitiated && currentManagerId != actorId.Value)
            return Result<ProjectHandoverRequestDto>.Failure(Error.Forbidden("Only the current Project Manager can request a handover"));
        if (initiationType == ManagerHandoverInitiationType.AdminInitiated && !currentManagerId.HasValue)
            return Result<ProjectHandoverRequestDto>.Failure(Error.Conflict("This Project has no assigned manager. Use initial assignment instead."));
        if (initiationType == ManagerHandoverInitiationType.InitialAssignment && currentManagerId.HasValue)
            return Result<ProjectHandoverRequestDto>.Failure(Error.Conflict("This Project already has a manager. Use administrative handover instead."));
        if (currentManagerId == dto.NewProjectManagerId)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Validation("The new project manager must be different from the current project manager"));
        }

        var actor = await _context.Set<User>().FirstOrDefaultAsync(u => u.Id == actorId.Value && u.TenantId == tenantId && u.IsActive && !u.IsDeleted, cancellationToken);
        if (actor is null) return Result<ProjectHandoverRequestDto>.Failure(Error.Forbidden("The authenticated user is not an active user in this tenant"));

        var newManager = await _context.Set<User>().FirstOrDefaultAsync(u => u.Id == dto.NewProjectManagerId
            && u.TenantId == tenantId && u.IsActive && !u.IsDeleted, cancellationToken);
        if (newManager is null)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Validation("The new project manager could not be found"));
        }
        if (!await HasPermissionAsync(newManager.Id, tenantId, "Handover.Approve", cancellationToken))
            return Result<ProjectHandoverRequestDto>.Failure(Error.Validation("Select an active tenant manager who can approve the handover"));
        var eligibleManagers = await GetEligibleManagersAsync(projectId, cancellationToken);
        if (eligibleManagers.IsFailure || !eligibleManagers.Value!.Any(candidate => candidate.Id == newManager.Id))
            return Result<ProjectHandoverRequestDto>.Failure(Error.Validation("Select an eligible manager in this tenant who can approve the handover"));

        var effectiveDate = dto.EffectiveHandoverDate.Date;
        var reason = dto.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Validation("Handover reason is required"));
        }
        if (reason.Length > 500)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Validation("Handover reason cannot exceed 500 characters"));
        }
        if (!string.IsNullOrWhiteSpace(dto.Notes) && dto.Notes!.Length > 2000)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Validation("Handover notes cannot exceed 2000 characters"));
        }
        if (project.StartDate.HasValue && effectiveDate < project.StartDate.Value.Date)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Validation("Handover date cannot be before the project start date"));
        }
        if (project.EndDate.HasValue && effectiveDate > project.EndDate.Value.Date)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Validation("Handover date cannot be after the project end date"));
        }

        var pendingExists = await _context.Set<ProjectHandoverRequest>()
            .AnyAsync(r => r.ProjectId == projectId && r.TenantId == tenantId && r.Status == ProjectHandoverRequest.Statuses.Pending && !r.IsDeleted, cancellationToken);
        if (pendingExists)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Conflict("A pending handover request already exists for this project"));
        }

        var request = new ProjectHandoverRequest
        {
            TenantId = tenantId,
            ProjectId = projectId,
            CurrentProjectManagerId = currentManagerId,
            NewProjectManagerId = dto.NewProjectManagerId,
            InitiationType = initiationType,
            EffectiveHandoverDate = effectiveDate,
            Reason = reason,
            Notes = dto.Notes,
            Status = ProjectHandoverRequest.Statuses.Pending,
            RequestedBy = actorId.Value,
            RequestedAt = DateTime.UtcNow,
            CreatedBy = actorId.Value.ToString()
        };
        _context.Set<ProjectHandoverRequest>().Add(request);
        _context.Set<AuditEntry>().Add(new AuditEntry
        {
            TenantId = tenantId,
            EntityType = "Project",
            EntityId = projectId,
            Action = "ManagerHandoverRequested",
            UserId = actorId.Value,
            Timestamp = DateTime.UtcNow,
            OriginalValues = JsonSerializer.Serialize(new { request.RequestedBy, request.CurrentProjectManagerId, request.InitiationType, request.Reason }),
            CreatedBy = actorId.Value.ToString()
        });
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await SendRequestCreatedNotificationsAsync(request, project, cancellationToken);

        var dtoResult = await ToDtoAsync(request, cancellationToken);
        return Result<ProjectHandoverRequestDto>.Success(dtoResult);
    }

    public async Task<Result<ManagerHandoverCapabilitiesDto>> GetCapabilitiesAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
    {
        var tenantResult = GetRequiredCurrentTenantId();
        if (tenantResult.IsFailure) return Result<ManagerHandoverCapabilitiesDto>.Failure(tenantResult.Error!);
        var actorId = TryParseGuid(userId);
        if (!actorId.HasValue) return Result<ManagerHandoverCapabilitiesDto>.Failure(Error.Unauthorized("Authenticated user ID is missing or invalid."));
        var tenantId = tenantResult.Value;
        var project = await _context.Set<Project>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId && x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        if (project is null) return Result<ManagerHandoverCapabilitiesDto>.Failure(Error.NotFound("Project not found"));
        if (!await IsActiveTenantUserAsync(actorId.Value, tenantId, cancellationToken)) return Result<ManagerHandoverCapabilitiesDto>.Failure(Error.Forbidden("The authenticated user is not active in this tenant"));
        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorId.Value, tenantId, cancellationToken);
        var pending = await _context.Set<ProjectHandoverRequest>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.TenantId == tenantId && x.Status == ProjectHandoverRequest.Statuses.Pending && !x.IsDeleted, cancellationToken);
        var previousManagerName = project.ProjectManagerId.HasValue
            ? await _context.Set<User>().AsNoTracking().Where(x => x.Id == project.ProjectManagerId.Value && x.TenantId == tenantId).Select(x => x.FullName).FirstOrDefaultAsync(cancellationToken)
            : null;
        var requesterPermission = pending?.InitiationType == ManagerHandoverInitiationType.ManagerInitiated ? "Handover.Manage" : "Handover.AdminInitiate";
        return Result<ManagerHandoverCapabilitiesDto>.Success(new ManagerHandoverCapabilitiesDto
        {
            CurrentManagerId = project.ProjectManagerId,
            CurrentManagerName = previousManagerName,
            CanViewRequests = HasAny(permissions, "Handover.View", "Handover.Manage", "Handover.AdminInitiate"),
            CanInitiate = pending is null && project.ProjectManagerId == actorId && permissions.Contains("Handover.Manage"),
            CanAdminInitiate = pending is null && project.ProjectManagerId.HasValue && permissions.Contains("Handover.AdminInitiate"),
            CanAssignInitial = pending is null && !project.ProjectManagerId.HasValue && permissions.Contains("Handover.AdminInitiate"),
            CanApproveIncoming = pending?.NewProjectManagerId == actorId && permissions.Contains("Handover.Approve"),
            CanCancelOwnPending = pending?.RequestedBy == actorId && permissions.Contains(requesterPermission),
            HasPendingRequest = pending is not null
        });
    }

    public async Task<Result<IReadOnlyList<ProjectHandoverRequestDto>>> GetForProjectAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
    {
        var tenantResult = GetRequiredCurrentTenantId();
        if (tenantResult.IsFailure) return Result<IReadOnlyList<ProjectHandoverRequestDto>>.Failure(tenantResult.Error!);
        var actorId = TryParseGuid(userId);
        if (!actorId.HasValue) return Result<IReadOnlyList<ProjectHandoverRequestDto>>.Failure(Error.Unauthorized("Authenticated user ID is missing or invalid."));
        if (!await IsActiveTenantUserAsync(actorId.Value, tenantResult.Value, cancellationToken)) return Result<IReadOnlyList<ProjectHandoverRequestDto>>.Failure(Error.Forbidden("The authenticated user is not active in this tenant"));
        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorId.Value, tenantResult.Value, cancellationToken);
        if (!HasAny(permissions, "Handover.View", "Handover.Manage", "Handover.AdminInitiate")) return Result<IReadOnlyList<ProjectHandoverRequestDto>>.Failure(Error.Forbidden("You do not have permission to view handovers"));
        if (!await _context.Set<Project>().AnyAsync(p => p.Id == projectId && p.TenantId == tenantResult.Value && !p.IsDeleted, cancellationToken))
            return Result<IReadOnlyList<ProjectHandoverRequestDto>>.Failure(Error.NotFound("Project not found"));
        var requests = await _context.Set<ProjectHandoverRequest>()
            .Where(r => r.ProjectId == projectId && r.TenantId == tenantResult.Value && !r.IsDeleted)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);

        var dtos = new List<ProjectHandoverRequestDto>();
        foreach (var request in requests)
        {
            dtos.Add(await ToDtoAsync(request, cancellationToken));
        }
        return Result<IReadOnlyList<ProjectHandoverRequestDto>>.Success(dtos);
    }

    public async Task<Result<int>> GetPendingCountForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var tenantResult = GetRequiredCurrentTenantId();
        if (tenantResult.IsFailure) return Result<int>.Failure(tenantResult.Error!);
        var actorId = TryParseGuid(_httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty);
        if (!actorId.HasValue || !await IsActiveTenantUserAsync(actorId.Value, tenantResult.Value, cancellationToken))
            return Result<int>.Failure(Error.Forbidden("The authenticated user is not active in this tenant"));
        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorId.Value, tenantResult.Value, cancellationToken);
        if (!HasAny(permissions, "Handover.View", "Handover.Manage", "Handover.AdminInitiate"))
            return Result<int>.Failure(Error.Forbidden("You do not have permission to view handovers"));
        if (!await _context.Set<Project>().AnyAsync(project => project.Id == projectId && project.TenantId == tenantResult.Value && !project.IsDeleted, cancellationToken))
            return Result<int>.Failure(Error.NotFound("Project not found"));
        var count = await _context.Set<ProjectHandoverRequest>().CountAsync(request => request.ProjectId == projectId
            && request.TenantId == tenantResult.Value && request.Status == ProjectHandoverRequest.Statuses.Pending && !request.IsDeleted, cancellationToken);
        return Result<int>.Success(count);
    }

    public async Task<Result<IReadOnlyList<ProjectHandoverRequestDto>>> GetPendingForMeAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!TryParseGuid(userId).HasValue)
        {
            return Result<IReadOnlyList<ProjectHandoverRequestDto>>.Success(Array.Empty<ProjectHandoverRequestDto>());
        }

        var id = TryParseGuid(userId)!.Value;
        var tenantResult = GetRequiredCurrentTenantId();
        if (tenantResult.IsFailure) return Result<IReadOnlyList<ProjectHandoverRequestDto>>.Failure(tenantResult.Error!);
        if (!await IsActiveTenantUserAsync(id, tenantResult.Value, cancellationToken)) return Result<IReadOnlyList<ProjectHandoverRequestDto>>.Failure(Error.Forbidden("The authenticated user is not active in this tenant"));
        if (!await HasPermissionAsync(id, tenantResult.Value, "Handover.Approve", cancellationToken)) return Result<IReadOnlyList<ProjectHandoverRequestDto>>.Failure(Error.Forbidden("You do not have permission to approve handovers"));
        var requests = await _context.Set<ProjectHandoverRequest>()
            .Where(r => r.TenantId == tenantResult.Value && r.NewProjectManagerId == id && r.Status == ProjectHandoverRequest.Statuses.Pending && !r.IsDeleted)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);

        var dtos = new List<ProjectHandoverRequestDto>();
        foreach (var request in requests)
        {
            dtos.Add(await ToDtoAsync(request, cancellationToken));
        }
        return Result<IReadOnlyList<ProjectHandoverRequestDto>>.Success(dtos);
    }

    public async Task<Result<ApprovalReviewDto>> GetApprovalReviewAsync(Guid requestId, string userId, CancellationToken cancellationToken = default)
    {
        var tenant = GetRequiredCurrentTenantId();
        var actorId = TryParseGuid(userId);
        if (tenant.IsFailure) return Result<ApprovalReviewDto>.Failure(tenant.Error!);
        if (!actorId.HasValue) return Result<ApprovalReviewDto>.Failure(Error.Unauthorized("Authenticated user ID is missing or invalid."));
        if (!await IsActiveTenantUserAsync(actorId.Value, tenant.Value, cancellationToken)
            || !await HasPermissionAsync(actorId.Value, tenant.Value, "Handover.Approve", cancellationToken))
            return Result<ApprovalReviewDto>.Failure(Error.NotFound("This approval is not available to the current user."));

        var request = await _context.Set<ProjectHandoverRequest>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == requestId && item.TenantId == tenant.Value
                && item.NewProjectManagerId == actorId.Value && item.Status == ProjectHandoverRequest.Statuses.Pending
                && !item.IsDeleted, cancellationToken);
        if (request is null) return Result<ApprovalReviewDto>.Failure(Error.NotFound("This approval is no longer available."));

        var project = await _context.Set<Project>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.ProjectId && item.TenantId == tenant.Value && !item.IsDeleted, cancellationToken);
        if (project is null) return Result<ApprovalReviewDto>.Failure(Error.NotFound("The project for this approval is unavailable."));
        var userIds = new[] { request.RequestedBy, request.CurrentProjectManagerId, request.NewProjectManagerId }
            .Where(value => value.HasValue).Select(value => value!.Value).Distinct().ToArray();
        var names = await _context.Set<User>().AsNoTracking()
            .Where(user => userIds.Contains(user.Id) && user.TenantId == tenant.Value && !user.IsDeleted)
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        return Result<ApprovalReviewDto>.Success(new ApprovalReviewDto
        {
            ApprovalId = request.Id,
            ApprovalType = "ProjectHandover",
            BusinessObjectType = "Project",
            BusinessObjectPublicId = project.PublicId,
            BusinessObjectName = project.Name,
            BusinessObjectCode = project.Code,
            BusinessObjectStatus = project.Status,
            Title = "Project Manager Handover",
            Reason = request.Reason,
            Notes = request.Notes,
            RequestedByName = names.GetValueOrDefault(request.RequestedBy) ?? string.Empty,
            RequestedAt = request.RequestedAt,
            Status = request.Status,
            EffectiveDate = request.EffectiveHandoverDate,
            CurrentAssigneeName = request.CurrentProjectManagerId.HasValue ? names.GetValueOrDefault(request.CurrentProjectManagerId.Value) : null,
            ProposedAssigneeName = names.GetValueOrDefault(request.NewProjectManagerId),
            IsReadOnly = true,
            CanDecide = true
        });
    }

    public async Task<Result<ProjectHandoverRequestDto>> AcceptAsync(Guid requestId, string userId, CancellationToken cancellationToken = default)
    {
        var tenantResult = GetRequiredCurrentTenantId();
        if (tenantResult.IsFailure) return Result<ProjectHandoverRequestDto>.Failure(tenantResult.Error!);
        var request = await _context.Set<ProjectHandoverRequest>()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TenantId == tenantResult.Value && !r.IsDeleted, cancellationToken);
        if (request is null) return Result<ProjectHandoverRequestDto>.Failure(Error.NotFound("Handover request not found"));

        if (request.Status != ProjectHandoverRequest.Statuses.Pending)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Conflict("The handover request is no longer pending"));
        }
        if (!TryParseGuid(userId).HasValue || request.NewProjectManagerId != TryParseGuid(userId)!.Value)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Forbidden("Only the selected new project manager can accept this request"));
        }
        if (!await HasPermissionAsync(TryParseGuid(userId)!.Value, request.TenantId, "Handover.Approve", cancellationToken))
            return Result<ProjectHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to approve handovers"));
        if (!await IsActiveTenantUserAsync(TryParseGuid(userId)!.Value, request.TenantId, cancellationToken))
            return Result<ProjectHandoverRequestDto>.Failure(Error.Forbidden("The selected manager is no longer an active user in this tenant"));

        var handoverResult = await _snapshotService.CompleteHandoverAsync(request.ProjectId, request, userId, cancellationToken);
        if (handoverResult.IsFailure)
        {
            return Result<ProjectHandoverRequestDto>.Failure(handoverResult.Error!);
        }

        await SendAcceptedNotificationsAsync(request, cancellationToken);

        var dtoResult = await ToDtoAsync(request, cancellationToken);
        return Result<ProjectHandoverRequestDto>.Success(dtoResult);
    }

    public async Task<Result<ProjectHandoverRequestDto>> RejectAsync(Guid requestId, RejectProjectHandoverRequestDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var tenantResult = GetRequiredCurrentTenantId();
        if (tenantResult.IsFailure) return Result<ProjectHandoverRequestDto>.Failure(tenantResult.Error!);
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var request = await _context.Set<ProjectHandoverRequest>()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TenantId == tenantResult.Value && !r.IsDeleted, cancellationToken);
        if (request is null) return Result<ProjectHandoverRequestDto>.Failure(Error.NotFound("Handover request not found"));

        if (request.Status != ProjectHandoverRequest.Statuses.Pending)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Conflict("The handover request is no longer pending"));
        }
        if (!TryParseGuid(userId).HasValue || request.NewProjectManagerId != TryParseGuid(userId)!.Value)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Forbidden("Only the selected new project manager can reject this request"));
        }
        if (!await HasPermissionAsync(TryParseGuid(userId)!.Value, request.TenantId, "Handover.Approve", cancellationToken))
            return Result<ProjectHandoverRequestDto>.Failure(Error.Forbidden("You do not have permission to approve handovers"));
        if (!await IsActiveTenantUserAsync(TryParseGuid(userId)!.Value, request.TenantId, cancellationToken)) return Result<ProjectHandoverRequestDto>.Failure(Error.Forbidden("The selected manager is no longer an active user in this tenant"));

        var rejectionReason = dto.RejectionReason?.Trim();
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Validation("Rejection reason is required"));
        }
        if (rejectionReason.Length > 500)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Validation("Rejection reason cannot exceed 500 characters"));
        }

        request.Status = ProjectHandoverRequest.Statuses.Rejected;
        request.RespondedBy = TryParseGuid(userId);
        request.RespondedAt = DateTime.UtcNow;
        request.RejectionReason = rejectionReason;
        request.UpdatedBy = userId;
        _context.Set<AuditEntry>().Add(new AuditEntry
        {
            TenantId = request.TenantId,
            EntityType = "Project",
            EntityId = request.ProjectId,
            Action = "ManagerHandoverRejected",
            UserId = TryParseGuid(userId),
            Timestamp = DateTime.UtcNow,
            OriginalValues = JsonSerializer.Serialize(new { request.Id, request.RequestedBy, request.InitiationType }),
            NewValues = JsonSerializer.Serialize(new { request.RejectionReason }),
            CreatedBy = userId
        });
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await SendRejectedNotificationsAsync(request, cancellationToken);

        var dtoResult = await ToDtoAsync(request, cancellationToken);
        return Result<ProjectHandoverRequestDto>.Success(dtoResult);
    }

    public async Task<Result<ProjectHandoverRequestDto>> CancelAsync(Guid requestId, string userId, CancellationToken cancellationToken = default)
    {
        var tenantResult = GetRequiredCurrentTenantId();
        if (tenantResult.IsFailure) return Result<ProjectHandoverRequestDto>.Failure(tenantResult.Error!);
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var request = await _context.Set<ProjectHandoverRequest>()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TenantId == tenantResult.Value && !r.IsDeleted, cancellationToken);
        if (request is null) return Result<ProjectHandoverRequestDto>.Failure(Error.NotFound("Handover request not found"));

        if (request.Status != ProjectHandoverRequest.Statuses.Pending)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Conflict("The handover request is no longer pending"));
        }

        var actorId = TryParseGuid(userId);
        var isRequester = actorId.HasValue && request.RequestedBy == actorId.Value;
        var requiredPermission = request.InitiationType == ManagerHandoverInitiationType.ManagerInitiated ? "Handover.Manage" : "Handover.AdminInitiate";
        if (!isRequester || !await IsActiveTenantUserAsync(actorId!.Value, request.TenantId, cancellationToken)
            || !await HasPermissionAsync(actorId.Value, request.TenantId, requiredPermission, cancellationToken))
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Forbidden("Only the requester with the matching handover permission can cancel this request"));
        }

        request.Status = ProjectHandoverRequest.Statuses.Cancelled;
        request.RespondedBy = TryParseGuid(userId);
        request.RespondedAt = DateTime.UtcNow;
        request.UpdatedBy = userId;
        _context.Set<AuditEntry>().Add(new AuditEntry
        {
            TenantId = request.TenantId,
            EntityType = "Project",
            EntityId = request.ProjectId,
            Action = "ManagerHandoverCancelled",
            UserId = actorId,
            Timestamp = DateTime.UtcNow,
            OriginalValues = JsonSerializer.Serialize(new { request.Id, request.RequestedBy, request.InitiationType }),
            CreatedBy = userId
        });
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var dtoResult = await ToDtoAsync(request, cancellationToken);
        return Result<ProjectHandoverRequestDto>.Success(dtoResult);
    }

    private async Task<ProjectHandoverRequestDto> ToDtoAsync(ProjectHandoverRequest request, CancellationToken cancellationToken)
    {
        var project = await _context.Set<Project>()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.TenantId == request.TenantId && !p.IsDeleted, cancellationToken);

        var userIds = new List<Guid>
        {
            request.NewProjectManagerId,
            request.RequestedBy
        };
        if (request.CurrentProjectManagerId.HasValue) userIds.Add(request.CurrentProjectManagerId.Value);
        if (request.RespondedBy.HasValue) userIds.Add(request.RespondedBy.Value);
        userIds = userIds.Distinct().ToList();

        var users = new Dictionary<Guid, User>();
        if (userIds.Count > 0)
        {
            users = await _context.Set<User>()
                .Where(u => userIds.Contains(u.Id) && u.TenantId == request.TenantId)
                .ToDictionaryAsync(u => u.Id, cancellationToken);
        }

        var snapshotName = (string?)null;
        if (request.SnapshotId.HasValue)
        {
            var snapshot = await _context.Set<ProjectSnapshot>()
                .FirstOrDefaultAsync(s => s.Id == request.SnapshotId.Value && s.TenantId == request.TenantId && s.ProjectId == request.ProjectId, cancellationToken);
            snapshotName = snapshot?.SnapshotName;
        }

        return new ProjectHandoverRequestDto
        {
            Id = request.Id,
            ProjectId = request.ProjectId,
            ProjectPublicId = project?.PublicId,
            ProjectName = project?.Name ?? string.Empty,
            ProjectCode = project?.Code ?? string.Empty,
            CurrentProjectManagerId = request.CurrentProjectManagerId,
            CurrentProjectManagerName = request.CurrentProjectManagerId.HasValue && users.TryGetValue(request.CurrentProjectManagerId.Value, out var current) ? current.FullName : string.Empty,
            NewProjectManagerId = request.NewProjectManagerId,
            NewProjectManagerName = users.TryGetValue(request.NewProjectManagerId, out var next) ? next.FullName : string.Empty,
            EffectiveHandoverDate = request.EffectiveHandoverDate,
            Reason = request.Reason,
            Notes = request.Notes,
            Status = request.Status,
            InitiationType = request.InitiationType,
            RequestedBy = request.RequestedBy,
            RequestedByName = users.TryGetValue(request.RequestedBy, out var requester) ? requester.FullName : string.Empty,
            RequestedAt = request.RequestedAt,
            RespondedBy = request.RespondedBy,
            RespondedByName = request.RespondedBy.HasValue && users.TryGetValue(request.RespondedBy.Value, out var responder) ? responder.FullName : null,
            RespondedAt = request.RespondedAt,
            RejectionReason = request.RejectionReason,
            SnapshotId = request.SnapshotId,
            SnapshotName = snapshotName,
            CreatedAt = request.CreatedAt,
            CreatedBy = request.CreatedBy
        };
    }

    private async Task SendRequestCreatedNotificationsAsync(ProjectHandoverRequest request, Project project, CancellationToken cancellationToken)
    {
        await _notificationService.SendAsync(new Notification
        {
            TenantId = project.TenantId,
            UserId = request.NewProjectManagerId,
            Title = "Project handover request",
            Body = $"You have received a project handover request requiring your approval. Project: {project.Name}",
            Type = NotificationTypes.ProjectHandover,
            ReferenceType = "ProjectHandoverRequest",
            ReferenceId = request.Id,
            SentAt = DateTime.UtcNow
        }, cancellationToken);

        foreach (var stakeholder in await GetActiveStakeholdersAsync(request.ProjectId, cancellationToken))
        {
            await _notificationService.SendAsync(new Notification
            {
                TenantId = project.TenantId,
                UserId = stakeholder.UserId!.Value,
                Title = "Project manager handover",
                Body = $"A Project Manager handover request has been submitted. Project: {project.Name}",
                Type = NotificationTypes.ProjectHandover,
                ReferenceType = "Project",
                ReferenceId = project.Id,
                SentAt = DateTime.UtcNow
            }, cancellationToken);
        }
    }

    private async Task SendAcceptedNotificationsAsync(ProjectHandoverRequest request, CancellationToken cancellationToken)
    {
        var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.TenantId == request.TenantId, cancellationToken);
        var projectName = project?.Name ?? string.Empty;
        var currentManagerExists = request.CurrentProjectManagerId.HasValue && await _context.Set<User>()
            .AnyAsync(user => user.Id == request.CurrentProjectManagerId.Value && user.TenantId == request.TenantId && !user.IsDeleted, cancellationToken);

        if (currentManagerExists && request.CurrentProjectManagerId != request.RespondedBy)
        {
            await _notificationService.SendAsync(new Notification
            {
                TenantId = request.TenantId,
                UserId = request.CurrentProjectManagerId!.Value,
                Title = "Handover accepted",
                Body = $"Your project manager handover for {projectName} has been accepted by the new project manager.",
                Type = NotificationTypes.ProjectHandover,
                ReferenceType = "Project",
                ReferenceId = request.ProjectId,
                SentAt = DateTime.UtcNow
            }, cancellationToken);
        }

        foreach (var stakeholder in await GetActiveStakeholdersAsync(request.ProjectId, cancellationToken))
        {
            await _notificationService.SendAsync(new Notification
            {
                TenantId = request.TenantId,
                UserId = stakeholder.UserId!.Value,
                Title = "Project manager handover completed",
                Body = $"The project manager handover for {projectName} has been completed.",
                Type = NotificationTypes.ProjectHandover,
                ReferenceType = "Project",
                ReferenceId = request.ProjectId,
                SentAt = DateTime.UtcNow
            }, cancellationToken);
        }
    }

    private async Task SendRejectedNotificationsAsync(ProjectHandoverRequest request, CancellationToken cancellationToken)
    {
        var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.TenantId == request.TenantId, cancellationToken);
        var projectName = project?.Name ?? string.Empty;
        var currentManagerExists = request.CurrentProjectManagerId.HasValue && await _context.Set<User>()
            .AnyAsync(user => user.Id == request.CurrentProjectManagerId.Value && user.TenantId == request.TenantId && !user.IsDeleted, cancellationToken);

        await _notificationService.SendAsync(new Notification
        {
            TenantId = request.TenantId,
            UserId = request.RequestedBy,
            Title = "Handover rejected",
            Body = $"Your project manager handover request for {projectName} was rejected. Reason: {request.RejectionReason}",
            Type = NotificationTypes.ProjectHandover,
            ReferenceType = "Project",
            ReferenceId = request.ProjectId,
            SentAt = DateTime.UtcNow
        }, cancellationToken);

        if (currentManagerExists && request.CurrentProjectManagerId != request.RequestedBy)
        {
            await _notificationService.SendAsync(new Notification
            {
                TenantId = request.TenantId,
                UserId = request.CurrentProjectManagerId!.Value,
                Title = "Handover rejected",
                Body = $"The project manager handover request for {projectName} was rejected by the proposed new project manager.",
                Type = NotificationTypes.ProjectHandover,
                ReferenceType = "Project",
                ReferenceId = request.ProjectId,
                SentAt = DateTime.UtcNow
            }, cancellationToken);
        }
    }

    private async Task<List<ProjectStakeholder>> GetActiveStakeholdersAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var tenantResult = GetRequiredCurrentTenantId();
        if (tenantResult.IsFailure) return new List<ProjectStakeholder>();
        return await _context.Set<ProjectStakeholder>()
            .Where(s => s.TenantId == tenantResult.Value && s.ProjectId == projectId && s.Status == ProjectStakeholderStatus.Active && s.RelationshipType == "Stakeholder" && s.UserId.HasValue && _context.Set<User>().Any(user => user.Id == s.UserId.Value && user.TenantId == tenantResult.Value && user.IsActive && !user.IsDeleted))
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permission, CancellationToken ct) =>
        (await _effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, ct)).Contains(permission);

    private async Task<bool> HasAnyPermissionAsync(Guid userId, Guid tenantId, CancellationToken ct, params string[] permissions) =>
        HasAny(await _effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, ct), permissions);

    private async Task<bool> IsActiveTenantUserAsync(Guid userId, Guid tenantId, CancellationToken ct) =>
        await _context.Set<User>().AnyAsync(x => x.Id == userId && x.TenantId == tenantId && x.IsActive && !x.IsDeleted, ct);

    private static bool HasAny(IReadOnlySet<string> effectivePermissions, params string[] permissions) =>
        permissions.Any(effectivePermissions.Contains);

    private Guid GetCurrentTenantId()
    {
        var tenantId = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var parsed))
        {
            return parsed;
        }

        return TenantConstants.DemoTenantId;
    }

    private Result<Guid> GetRequiredCurrentTenantId()
    {
        var tenantId = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        if (Guid.TryParse(tenantId, out var parsed))
        {
            return Result<Guid>.Success(parsed);
        }

        return Result<Guid>.Failure(Error.Unauthorized("Tenant ID is missing or invalid in the authentication token."));
    }

    private static Guid? TryParseGuid(string value)
    {
        return GuidHelper.TryParse(value);
    }
}
