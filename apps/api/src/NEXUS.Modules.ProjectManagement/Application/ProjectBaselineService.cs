using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectManagement.Domain.Entities;
using NEXUS.SharedKernel;
using Status = NEXUS.Modules.ProjectManagement.Domain.Entities.Status;

namespace NEXUS.Modules.ProjectManagement.Application;

public class ProjectBaselineService : IProjectBaselineService
{
    private const string InitialBaselineSnapshotType = "InitialBaseline";

    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IProjectSnapshotService _snapshotService;

    public ProjectBaselineService(
        DbContext context,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        IProjectSnapshotService snapshotService)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _snapshotService = snapshotService;
    }

    public async Task<Result<ProjectBaselineVersionDto>> CreateInitialBaselineAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
    {
        var tenantIdResult = GetCurrentTenantId();
        if (tenantIdResult.IsFailure)
        {
            return Result<ProjectBaselineVersionDto>.Failure(tenantIdResult.Error!);
        }
        var tenantId = tenantIdResult.Value;

        var actorIdResult = GetCurrentUserGuid();
        if (actorIdResult.IsFailure)
        {
            return Result<ProjectBaselineVersionDto>.Failure(actorIdResult.Error!);
        }
        var actorId = actorIdResult.Value;

        var project = await _context.Set<Project>()
            .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == tenantId && !p.IsDeleted, cancellationToken);
        if (project is null)
        {
            return Result<ProjectBaselineVersionDto>.Failure(Error.NotFound());
        }

        var hasCurrentApprovedBaseline = await _context.Set<ProjectBaselineVersion>()
            .AnyAsync(b => b.ProjectId == new ProjectId(projectId.ToString())
                        && b.TenantId == tenantId
                        && b.Status == Status.Approved
                        && b.IsCurrent, cancellationToken);
        if (hasCurrentApprovedBaseline)
        {
            return Result<ProjectBaselineVersionDto>.Failure(Error.Conflict("The project already has an approved current baseline. An initial baseline can only be created once."));
        }

        var actor = actorId.ToString();
        var now = DateTime.UtcNow;
        var versionNumber = new VersionNumber(1);

        var baseline = new ProjectBaselineVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = new ProjectId(projectId.ToString()),
            VersionNumber = versionNumber,
            Name = $"Initial Baseline v{versionNumber.Value}",
            Status = Status.Approved,
            Description = "Approved initial baseline captured when the project was started.",
            IsCurrent = true,
            CreatedBy = actor,
            ApprovedBy = actor,
            ApprovedAt = now
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.Set<ProjectBaselineVersion>().Add(baseline);

            var snapshotResult = await _snapshotService.CreateBaselineSnapshotAsync(
                projectId,
                InitialBaselineSnapshotType,
                baseline.Name,
                userId,
                cancellationToken);
            if (snapshotResult.IsFailure)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Result<ProjectBaselineVersionDto>.Failure(snapshotResult.Error!);
            }

            await _context.SaveChangesAsync(cancellationToken);

            var snapshotId = snapshotResult.Value!.Id;

            await _auditService.LogAsync(new AuditEntry
            {
                EntityType = "ProjectBaselineVersion",
                EntityId = baseline.Id,
                Action = "InitialBaselineCreated",
                TenantId = tenantId,
                UserId = actorId,
                NewValues = System.Text.Json.JsonSerializer.Serialize(new
                {
                    ProjectId = projectId,
                    VersionNumber = baseline.VersionNumber.Value,
                    Status = baseline.Status.ToString(),
                    IsCurrent = baseline.IsCurrent,
                    SnapshotId = snapshotId
                }),
                Timestamp = now,
                CreatedBy = actor
            }, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<ProjectBaselineVersionDto>.Success(new ProjectBaselineVersionDto
            {
                Id = baseline.Id,
                ProjectId = projectId,
                VersionNumber = baseline.VersionNumber.Value,
                Name = baseline.Name,
                Status = baseline.Status.ToString(),
                Description = baseline.Description,
                IsCurrent = baseline.IsCurrent,
                CreatedBy = baseline.CreatedBy,
                CreatedAt = baseline.CreatedAt,
                ApprovedBy = baseline.ApprovedBy,
                ApprovedAt = baseline.ApprovedAt,
                SourceChangeRequestId = baseline.SourceChangeRequestId,
                BaselineSnapshotId = snapshotId
            });
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private Result<Guid> GetCurrentTenantId()
    {
        var tenantIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return Result<Guid>.Failure(Error.Unauthorized("Tenant ID is missing or invalid in the authentication token."));
        }
        return Result<Guid>.Success(tenantId);
    }

    private Result<Guid> GetCurrentUserGuid()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Result<Guid>.Failure(Error.Unauthorized("User ID is missing or invalid in the authentication token."));
        }
        return Result<Guid>.Success(userId);
    }
}
