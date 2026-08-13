using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Application.Mapping;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public class ProjectChangeRequestService : IProjectChangeRequestService
{
    private const string PmoAdminRole = "pmo.admin";

    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProjectChangeRequestService(DbContext context, IAuditService auditService, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<ProjectChangeRequestDto>> CreateAsync(CreateProjectChangeRequestDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var tenantIdResult = GetCurrentTenantId();
        if (tenantIdResult.IsFailure)
        {
            return Result<ProjectChangeRequestDto>.Failure(tenantIdResult.Error);
        }
        var tenantId = tenantIdResult.Value;

        var userIdResult = GetCurrentUserGuid();
        if (userIdResult.IsFailure)
        {
            return Result<ProjectChangeRequestDto>.Failure(userIdResult.Error);
        }
        var actualUserId = userIdResult.Value;

        var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == dto.ProjectId && p.TenantId == tenantId, cancellationToken);
        if (project == null)
        {
            // Return NotFound if project doesn't exist for the tenant, preventing tenant enumeration
            return Result<ProjectChangeRequestDto>.Failure(Error.NotFound());
        }

        // Generate a sequential change request number per project.
        var nextNumber = (await _context.Set<ProjectChangeRequest>()
            .Where(pcr => pcr.ProjectId == dto.ProjectId && pcr.TenantId == tenantId)
            .Select(pcr => (int?)pcr.Number)
            .MaxAsync(cancellationToken) ?? 0) + 1;

        var request = dto.ToEntity(actualUserId.ToString()); // Use the resolved user ID
        request.TenantId = tenantId; // Use the resolved tenant ID
        request.Number = nextNumber;

        _context.Set<ProjectChangeRequest>().Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ProjectChangeRequest",
            EntityId = request.Id,
            Action = "Create",
            UserId = actualUserId,
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = actualUserId.ToString()
        }, cancellationToken);

        // Fetch related data for the DTO
        var user = await _context.Set<NEXUS.Modules.Identity.Domain.User>().FirstOrDefaultAsync(u => u.Id == request.RequestedBy, cancellationToken);
        var requesterName = user?.FullName ?? "Unknown";

        var projectDto = request.ToDto(project.Name, requesterName);

        return Result<ProjectChangeRequestDto>.Success(projectDto);
    }

    public async Task<Result<ProjectChangeRequestDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantIdResult = GetCurrentTenantId();
        if (tenantIdResult.IsFailure)
        {
            return Result<ProjectChangeRequestDto>.Failure(tenantIdResult.Error);
        }
        var tenantId = tenantIdResult.Value;

        var request = await _context.Set<ProjectChangeRequest>()
            .FirstOrDefaultAsync(pcr => pcr.Id == id && pcr.TenantId == tenantId, cancellationToken); // Tenant-scoped query

        if (request == null)
        {
            return Result<ProjectChangeRequestDto>.Failure(Error.NotFound());
        }

        var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);
        if (project == null)
        {
            // This should ideally not happen due to referential integrity, but good to check.
            return Result<ProjectChangeRequestDto>.Failure(Error.NotFound("Associated project not found."));
        }

        var user = await _context.Set<NEXUS.Modules.Identity.Domain.User>().FirstOrDefaultAsync(u => u.Id == request.RequestedBy, cancellationToken);
        var requesterName = user?.FullName ?? "Unknown";

        var reviewer = await _context.Set<NEXUS.Modules.Identity.Domain.User>().FirstOrDefaultAsync(u => u.Id == request.ReviewedBy, cancellationToken);
        var reviewerName = reviewer?.FullName;

        var dto = request.ToDto(project.Name, requesterName, reviewerName);
        return Result<ProjectChangeRequestDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyList<ProjectChangeRequestDto>>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var tenantIdResult = GetCurrentTenantId();
        if (tenantIdResult.IsFailure)
        {
            return Result<IReadOnlyList<ProjectChangeRequestDto>>.Failure(tenantIdResult.Error);
        }
        var tenantId = tenantIdResult.Value;

        // First, verify the project belongs to the current tenant
        var projectExists = await _context.Set<Project>().AnyAsync(p => p.Id == projectId && p.TenantId == tenantId, cancellationToken);
        if (!projectExists)
        {
            // Return NotFound to prevent tenant enumeration
            return Result<IReadOnlyList<ProjectChangeRequestDto>>.Failure(Error.NotFound());
        }

        var requests = await _context.Set<ProjectChangeRequest>()
            .Where(pcr => pcr.ProjectId == projectId && pcr.TenantId == tenantId) // Tenant-scoped query
            .OrderByDescending(pcr => pcr.CreatedAt) // Order by newest first
            .ToListAsync(cancellationToken);


        var userIds = requests.Select(r => r.RequestedBy).ToList();
        if (requests.Any(r => r.ReviewedBy.HasValue))
        {
            userIds.AddRange(requests.Where(r => r.ReviewedBy.HasValue).Select(r => r.ReviewedBy!.Value));
        }
        userIds = userIds.Distinct().ToList();

        var users = await _context.Set<NEXUS.Modules.Identity.Domain.User>()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var dtos = requests.Select(r =>
        {
            var requesterName = users.TryGetValue(r.RequestedBy, out var reqUser) ? reqUser.FullName : "Unknown";
            var reviewerName = r.ReviewedBy.HasValue && users.TryGetValue(r.ReviewedBy.Value, out var revUser) ? revUser.FullName : null;
            return r.ToDto("", requesterName, reviewerName); // Project name is fetched in the mapping helper, but let's pass the ID for clarity if needed. We already verified project exists.
        }).ToList();

        // Fetch project name once, since we know it exists and is tenant-scoped.
        var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        var projectName = project?.Name ?? "Unknown Project";

        // Assign the project name to each DTO
        foreach (var dto in dtos)
        {
            dto.ProjectName = projectName;
        }

        return Result<IReadOnlyList<ProjectChangeRequestDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<ProjectChangeRequestDto>>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var tenantIdResult = GetCurrentTenantId();
        if (tenantIdResult.IsFailure)
        {
            return Result<IReadOnlyList<ProjectChangeRequestDto>>.Failure(tenantIdResult.Error);
        }
        var tenantId = tenantIdResult.Value;

        // Only an authorized reviewer can view pending change requests for approval.
        if (!IsReviewer())
        {
            return Result<IReadOnlyList<ProjectChangeRequestDto>>.Failure(Error.Forbidden("Only an authorized reviewer can review change requests."));
        }

        var requests = await _context.Set<ProjectChangeRequest>()
            .Where(pcr => pcr.Status == ProjectChangeRequest.Statuses.Pending && pcr.TenantId == tenantId)
            .OrderByDescending(pcr => pcr.RequestedAt)
            .ToListAsync(cancellationToken);

        var userIds = requests.Select(r => r.RequestedBy).Distinct().ToList();
        var usedReviewerIds = requests.Where(r => r.ReviewedBy.HasValue).Select(r => r.ReviewedBy!.Value).ToList();
        if (usedReviewerIds.Count > 0)
        {
            userIds = userIds.Concat(usedReviewerIds).Distinct().ToList();
        }

        var users = await _context.Set<NEXUS.Modules.Identity.Domain.User>()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var projectIds = requests.Select(r => r.ProjectId).Distinct().ToList();
        var projects = await _context.Set<Project>()
            .Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var dtos = requests.Select(r =>
        {
            var requesterName = users.TryGetValue(r.RequestedBy, out var reqUser) ? reqUser.FullName : "Unknown";
            var reviewerName = r.ReviewedBy.HasValue && users.TryGetValue(r.ReviewedBy.Value, out var revUser) ? revUser.FullName : null;
            var projectName = projects.TryGetValue(r.ProjectId, out var project) ? project.Name : "Unknown Project";
            return r.ToDto(projectName, requesterName, reviewerName);
        }).ToList();

        return Result<IReadOnlyList<ProjectChangeRequestDto>>.Success(dtos);
    }

    public async Task<Result<ProjectChangeRequestDto>> ApproveAsync(Guid id, ApproveProjectChangeRequestDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var tenantIdResult = GetCurrentTenantId();
        if (tenantIdResult.IsFailure)
        {
            return Result<ProjectChangeRequestDto>.Failure(tenantIdResult.Error);
        }
        var tenantId = tenantIdResult.Value;

        var reviewerIdResult = GetCurrentUserGuid();
        if (reviewerIdResult.IsFailure)
        {
            return Result<ProjectChangeRequestDto>.Failure(reviewerIdResult.Error);
        }
        var reviewerId = reviewerIdResult.Value;

        if (!IsReviewer())
        {
            return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("Only an authorized reviewer can approve a change request."));
        }

        var request = await _context.Set<ProjectChangeRequest>()
            .FirstOrDefaultAsync(pcr => pcr.Id == id && pcr.TenantId == tenantId, cancellationToken);
        if (request == null)
        {
            return Result<ProjectChangeRequestDto>.Failure(Error.NotFound());
        }

        if (!request.IsPending)
        {
            return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("Only pending change requests can be approved."));
        }

        request.Status = ProjectChangeRequest.Statuses.Approved;
        request.ReviewedBy = reviewerId;
        request.ReviewedAt = DateTime.UtcNow;
        request.DecisionNotes = dto.DecisionNotes?.Trim();
        request.UpdatedBy = userId;

        var saveResult = await SaveWithConcurrencyCheckAsync(cancellationToken);
        if (saveResult.IsFailure)
        {
            return Result<ProjectChangeRequestDto>.Failure(saveResult.Error);
        }

        await LogAuditAsync(request, "Approve", reviewerId, dto.DecisionNotes, cancellationToken);

        return await BuildDtoAsync(request, cancellationToken);
    }

    public async Task<Result<ProjectChangeRequestDto>> RejectAsync(Guid id, RejectProjectChangeRequestDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var tenantIdResult = GetCurrentTenantId();
        if (tenantIdResult.IsFailure)
        {
            return Result<ProjectChangeRequestDto>.Failure(tenantIdResult.Error);
        }
        var tenantId = tenantIdResult.Value;

        var reviewerIdResult = GetCurrentUserGuid();
        if (reviewerIdResult.IsFailure)
        {
            return Result<ProjectChangeRequestDto>.Failure(reviewerIdResult.Error);
        }
        var reviewerId = reviewerIdResult.Value;

        if (!IsReviewer())
        {
            return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("Only an authorized reviewer can reject a change request."));
        }

        var decisionNotes = dto.DecisionNotes?.Trim();
        if (string.IsNullOrWhiteSpace(decisionNotes))
        {
            return Result<ProjectChangeRequestDto>.Failure(Error.Validation("Decision notes are required when rejecting a change request."));
        }
        if (decisionNotes!.Length > 2000)
        {
            return Result<ProjectChangeRequestDto>.Failure(Error.Validation("Decision notes cannot exceed 2000 characters."));
        }

        var request = await _context.Set<ProjectChangeRequest>()
            .FirstOrDefaultAsync(pcr => pcr.Id == id && pcr.TenantId == tenantId, cancellationToken);
        if (request == null)
        {
            return Result<ProjectChangeRequestDto>.Failure(Error.NotFound());
        }

        if (!request.IsPending)
        {
            return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("Only pending change requests can be rejected."));
        }

        request.Status = ProjectChangeRequest.Statuses.Rejected;
        request.ReviewedBy = reviewerId;
        request.ReviewedAt = DateTime.UtcNow;
        request.DecisionNotes = decisionNotes;
        request.UpdatedBy = userId;

        var saveResult = await SaveWithConcurrencyCheckAsync(cancellationToken);
        if (saveResult.IsFailure)
        {
            return Result<ProjectChangeRequestDto>.Failure(saveResult.Error);
        }

        await LogAuditAsync(request, "Reject", reviewerId, decisionNotes, cancellationToken);

        return await BuildDtoAsync(request, cancellationToken);
    }

    public async Task<Result<ProjectChangeRequestDto>> CancelAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var tenantIdResult = GetCurrentTenantId();
        if (tenantIdResult.IsFailure)
        {
            return Result<ProjectChangeRequestDto>.Failure(tenantIdResult.Error);
        }
        var tenantId = tenantIdResult.Value;

        var actorIdResult = GetCurrentUserGuid();
        if (actorIdResult.IsFailure)
        {
            return Result<ProjectChangeRequestDto>.Failure(actorIdResult.Error);
        }
        var actorId = actorIdResult.Value;

        var request = await _context.Set<ProjectChangeRequest>()
            .FirstOrDefaultAsync(pcr => pcr.Id == id && pcr.TenantId == tenantId, cancellationToken);
        if (request == null)
        {
            return Result<ProjectChangeRequestDto>.Failure(Error.NotFound());
        }

        if (!request.IsPending)
        {
            return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("Only pending change requests can be cancelled."));
        }

        var isRequester = request.RequestedBy == actorId;
        var isPmoAdmin = IsPmoAdmin();
        if (!isRequester && !isPmoAdmin)
        {
            return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("Only the requester or a PMO administrator can cancel this change request."));
        }

        request.Status = ProjectChangeRequest.Statuses.Cancelled;
        request.DecisionNotes = request.DecisionNotes ?? "Cancelled by requester or PMO.";
        request.UpdatedBy = userId;

        var saveResult = await SaveWithConcurrencyCheckAsync(cancellationToken);
        if (saveResult.IsFailure)
        {
            return Result<ProjectChangeRequestDto>.Failure(saveResult.Error);
        }

        await LogAuditAsync(request, "Cancel", actorId, request.DecisionNotes, cancellationToken);

        return await BuildDtoAsync(request, cancellationToken);
    }

    private async Task<Result> SaveWithConcurrencyCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Conflict("The change request was modified concurrently. Refresh and try again."));
        }
    }

    private async Task LogAuditAsync(ProjectChangeRequest request, string action, Guid actorId, string? decisionNotes, CancellationToken cancellationToken)
    {
        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ProjectChangeRequest",
            EntityId = request.Id,
            Action = action,
            UserId = actorId,
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { request.Status, request.ReviewedBy, request.ReviewedAt, DecisionNotes = decisionNotes }),
            Timestamp = DateTime.UtcNow,
            CreatedBy = actorId.ToString()
        }, cancellationToken);
    }

    private async Task<Result<ProjectChangeRequestDto>> BuildDtoAsync(ProjectChangeRequest request, CancellationToken cancellationToken)
    {
        var project = await _context.Set<Project>()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        var user = await _context.Set<NEXUS.Modules.Identity.Domain.User>()
            .FirstOrDefaultAsync(u => u.Id == request.RequestedBy, cancellationToken);
        var requesterName = user?.FullName ?? "Unknown";

        var reviewer = request.ReviewedBy.HasValue
            ? await _context.Set<NEXUS.Modules.Identity.Domain.User>().FirstOrDefaultAsync(u => u.Id == request.ReviewedBy.Value, cancellationToken)
            : null;
        var reviewerName = reviewer?.FullName;

        var dto = request.ToDto(project?.Name ?? "Unknown Project", requesterName, reviewerName);
        return Result<ProjectChangeRequestDto>.Success(dto);
    }

    private bool IsReviewer()
    {
        return _httpContextAccessor.HttpContext?.User.IsInRole(PmoAdminRole) == true;
    }

    private bool IsPmoAdmin()
    {
        return _httpContextAccessor.HttpContext?.User.IsInRole(PmoAdminRole) == true;
    }

    private Result<Guid> GetCurrentTenantId()
    {
        var tenantIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            // Return a failure result compatible with the existing pattern
            return Result<Guid>.Failure(Error.Unauthorized("Tenant ID is missing or invalid in the authentication token.")); // Or Forbidden depending on authz policy
        }
        return Result<Guid>.Success(tenantId);
    }

    private Result<Guid> GetCurrentUserGuid()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            // Return a failure result compatible with the existing pattern
            return Result<Guid>.Failure(Error.Unauthorized("User ID is missing or invalid in the authentication token.")); // Or Forbidden depending on authz policy
            }
        return Result<Guid>.Success(userId);
    }

}