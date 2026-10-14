using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.ProjectManagement.Application;

public class ProjectHandoverRequestService : IProjectHandoverRequestService
{
    private const string PmoAdminRole = "pmo.admin";

    private readonly DbContext _context;
    private readonly IProjectSnapshotService _snapshotService;
    private readonly INotificationService _notificationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProjectHandoverRequestService(
        DbContext context,
        IProjectSnapshotService snapshotService,
        INotificationService notificationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _snapshotService = snapshotService;
        _notificationService = notificationService;
        _httpContextAccessor = httpContextAccessor;
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
            .Select(user => new { user.Id, user.FirstName, user.LastName, user.Email, user.JobTitle })
            .ToListAsync(cancellationToken);

        IReadOnlyList<ProjectHandoverManagerDto> managers = users.Select(user => new ProjectHandoverManagerDto
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

    public async Task<Result<ProjectHandoverRequestDto>> CreateAsync(Guid projectId, CreateProjectHandoverRequestDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var tenantResult = GetRequiredCurrentTenantId();
        if (tenantResult.IsFailure)
        {
            return Result<ProjectHandoverRequestDto>.Failure(tenantResult.Error!);
        }
        var tenantId = tenantResult.Value;
        var project = await _context.Set<Project>()
            .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == tenantId && !p.IsDeleted, cancellationToken);
        if (project is null) return Result<ProjectHandoverRequestDto>.Failure(Error.NotFound("Project not found"));

        var currentManagerId = project.ProjectManagerId;
        if (!currentManagerId.HasValue)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Validation("Cannot hand over the project manager because the project does not have a current project manager"));
        }
        if (currentManagerId.Value == dto.NewProjectManagerId)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Validation("The new project manager must be different from the current project manager"));
        }

        var newManager = await _context.Set<User>().FirstOrDefaultAsync(u => u.Id == dto.NewProjectManagerId
            && u.TenantId == tenantId && u.IsActive && !u.IsDeleted, cancellationToken);
        if (newManager is null)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Validation("The new project manager could not be found"));
        }

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
            .AnyAsync(r => r.ProjectId == projectId && r.Status == ProjectHandoverRequest.Statuses.Pending && !r.IsDeleted, cancellationToken);
        if (pendingExists)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Conflict("A pending handover request already exists for this project"));
        }

        var requester = await _context.Set<User>().FirstOrDefaultAsync(u => u.Id == TryParseGuid(userId), cancellationToken)
            ?? await _context.Set<User>().FirstOrDefaultAsync(u => u.Id == newManager.Id && !u.IsDeleted, cancellationToken);

        var request = new ProjectHandoverRequest
        {
            TenantId = tenantId,
            ProjectId = projectId,
            CurrentProjectManagerId = currentManagerId.Value,
            NewProjectManagerId = dto.NewProjectManagerId,
            EffectiveHandoverDate = effectiveDate,
            Reason = reason,
            Notes = dto.Notes,
            Status = ProjectHandoverRequest.Statuses.Pending,
            RequestedBy = requester?.Id ?? currentManagerId.Value,
            RequestedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _context.Set<ProjectHandoverRequest>().Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        await SendRequestCreatedNotificationsAsync(request, project, cancellationToken);

        var dtoResult = await ToDtoAsync(request, cancellationToken);
        return Result<ProjectHandoverRequestDto>.Success(dtoResult);
    }

    public async Task<Result<IReadOnlyList<ProjectHandoverRequestDto>>> GetForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var requests = await _context.Set<ProjectHandoverRequest>()
            .Where(r => r.ProjectId == projectId && !r.IsDeleted)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);

        var dtos = new List<ProjectHandoverRequestDto>();
        foreach (var request in requests)
        {
            dtos.Add(await ToDtoAsync(request, cancellationToken));
        }
        return Result<IReadOnlyList<ProjectHandoverRequestDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<ProjectHandoverRequestDto>>> GetPendingForMeAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!TryParseGuid(userId).HasValue)
        {
            return Result<IReadOnlyList<ProjectHandoverRequestDto>>.Success(Array.Empty<ProjectHandoverRequestDto>());
        }

        var id = TryParseGuid(userId)!.Value;
        var requests = await _context.Set<ProjectHandoverRequest>()
            .Where(r => r.NewProjectManagerId == id && r.Status == ProjectHandoverRequest.Statuses.Pending && !r.IsDeleted)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);

        var dtos = new List<ProjectHandoverRequestDto>();
        foreach (var request in requests)
        {
            dtos.Add(await ToDtoAsync(request, cancellationToken));
        }
        return Result<IReadOnlyList<ProjectHandoverRequestDto>>.Success(dtos);
    }

    public async Task<Result<ProjectHandoverRequestDto>> AcceptAsync(Guid requestId, string userId, CancellationToken cancellationToken = default)
    {
        var request = await _context.Set<ProjectHandoverRequest>()
            .FirstOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted, cancellationToken);
        if (request is null) return Result<ProjectHandoverRequestDto>.Failure(Error.NotFound("Handover request not found"));

        if (request.Status != ProjectHandoverRequest.Statuses.Pending)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Conflict("The handover request is no longer pending"));
        }
        if (!TryParseGuid(userId).HasValue || request.NewProjectManagerId != TryParseGuid(userId)!.Value)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Forbidden("Only the selected new project manager can accept this request"));
        }

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
        var request = await _context.Set<ProjectHandoverRequest>()
            .FirstOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted, cancellationToken);
        if (request is null) return Result<ProjectHandoverRequestDto>.Failure(Error.NotFound("Handover request not found"));

        if (request.Status != ProjectHandoverRequest.Statuses.Pending)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Conflict("The handover request is no longer pending"));
        }
        if (!TryParseGuid(userId).HasValue || request.NewProjectManagerId != TryParseGuid(userId)!.Value)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Forbidden("Only the selected new project manager can reject this request"));
        }

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
        await _context.SaveChangesAsync(cancellationToken);

        await SendRejectedNotificationsAsync(request, cancellationToken);

        var dtoResult = await ToDtoAsync(request, cancellationToken);
        return Result<ProjectHandoverRequestDto>.Success(dtoResult);
    }

    public async Task<Result<ProjectHandoverRequestDto>> CancelAsync(Guid requestId, string userId, CancellationToken cancellationToken = default)
    {
        var request = await _context.Set<ProjectHandoverRequest>()
            .FirstOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted, cancellationToken);
        if (request is null) return Result<ProjectHandoverRequestDto>.Failure(Error.NotFound("Handover request not found"));

        if (request.Status != ProjectHandoverRequest.Statuses.Pending)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Conflict("The handover request is no longer pending"));
        }

        var isRequester = TryParseGuid(userId).HasValue && request.RequestedBy == TryParseGuid(userId)!.Value;
        var isPmoAdmin = _httpContextAccessor.HttpContext?.User.IsInRole(PmoAdminRole) == true;
        if (!isRequester && !isPmoAdmin)
        {
            return Result<ProjectHandoverRequestDto>.Failure(Error.Forbidden("Only the requester or a PMO administrator can cancel this request"));
        }

        request.Status = ProjectHandoverRequest.Statuses.Cancelled;
        request.RespondedBy = TryParseGuid(userId);
        request.RespondedAt = DateTime.UtcNow;
        request.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        var dtoResult = await ToDtoAsync(request, cancellationToken);
        return Result<ProjectHandoverRequestDto>.Success(dtoResult);
    }

    private async Task<ProjectHandoverRequestDto> ToDtoAsync(ProjectHandoverRequest request, CancellationToken cancellationToken)
    {
        var project = await _context.Set<Project>()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId && !p.IsDeleted, cancellationToken);

        var userIds = new List<Guid>
        {
            request.CurrentProjectManagerId,
            request.NewProjectManagerId,
            request.RequestedBy
        };
        if (request.RespondedBy.HasValue) userIds.Add(request.RespondedBy.Value);
        userIds = userIds.Distinct().ToList();

        var users = new Dictionary<Guid, User>();
        if (userIds.Count > 0)
        {
            users = await _context.Set<User>()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken);
        }

        var snapshotName = (string?)null;
        if (request.SnapshotId.HasValue)
        {
            var snapshot = await _context.Set<ProjectSnapshot>()
                .FirstOrDefaultAsync(s => s.Id == request.SnapshotId.Value, cancellationToken);
            snapshotName = snapshot?.SnapshotName;
        }

        return new ProjectHandoverRequestDto
        {
            Id = request.Id,
            ProjectId = request.ProjectId,
            ProjectName = project?.Name ?? string.Empty,
            ProjectCode = project?.Code ?? string.Empty,
            CurrentProjectManagerId = request.CurrentProjectManagerId,
            CurrentProjectManagerName = users.TryGetValue(request.CurrentProjectManagerId, out var current) ? current.FullName : string.Empty,
            NewProjectManagerId = request.NewProjectManagerId,
            NewProjectManagerName = users.TryGetValue(request.NewProjectManagerId, out var next) ? next.FullName : string.Empty,
            EffectiveHandoverDate = request.EffectiveHandoverDate,
            Reason = request.Reason,
            Notes = request.Notes,
            Status = request.Status,
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
            TenantId = GetCurrentTenantId(),
            UserId = request.NewProjectManagerId,
            Title = "Project handover request",
            Body = $"You have received a project handover request requiring your approval. Project: {project.Name}",
            Type = NotificationTypes.ProjectHandover,
            ReferenceType = "Project",
            ReferenceId = project.Id,
            SentAt = DateTime.UtcNow
        }, cancellationToken);

        foreach (var stakeholder in await GetActiveStakeholdersAsync(request.ProjectId, cancellationToken))
        {
            await _notificationService.SendAsync(new Notification
            {
                TenantId = GetCurrentTenantId(),
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
        var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);
        var projectName = project?.Name ?? string.Empty;
        var currentManagerExists = await _context.Set<User>()
            .AnyAsync(user => user.Id == request.CurrentProjectManagerId && !user.IsDeleted, cancellationToken);

        if (currentManagerExists && request.CurrentProjectManagerId != request.RespondedBy)
        {
            await _notificationService.SendAsync(new Notification
            {
                TenantId = GetCurrentTenantId(),
                UserId = request.CurrentProjectManagerId,
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
                TenantId = GetCurrentTenantId(),
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
        var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);
        var projectName = project?.Name ?? string.Empty;
        var currentManagerExists = await _context.Set<User>()
            .AnyAsync(user => user.Id == request.CurrentProjectManagerId && !user.IsDeleted, cancellationToken);

        await _notificationService.SendAsync(new Notification
        {
            TenantId = GetCurrentTenantId(),
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
                TenantId = GetCurrentTenantId(),
                UserId = request.CurrentProjectManagerId,
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
        return await _context.Set<ProjectStakeholder>()
            .Where(s => s.ProjectId == projectId && s.Status == ProjectStakeholderStatus.Active && s.RelationshipType == "Stakeholder" && s.UserId.HasValue && _context.Set<User>().Any(user => user.Id == s.UserId.Value && user.IsActive))
            .ToListAsync(cancellationToken);
    }

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
