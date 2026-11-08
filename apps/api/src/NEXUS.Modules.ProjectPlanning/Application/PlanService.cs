using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Application.Mapping;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectPlanning.Application;

public class PlanService : IPlanService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPlanWbsService? _wbsService;
    private readonly IPlanActivityService? _activityService;
    private readonly IPlanDependencyService? _dependencyService;
    private readonly IManagementContextAccessor? _managementContextAccessor;
    private readonly IProjectPublicIdLookup? _projectPublicIdLookup;

    public PlanService(
        DbContext context,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        IPlanWbsService? wbsService = null,
        IPlanActivityService? activityService = null,
        IPlanDependencyService? dependencyService = null,
        IManagementContextAccessor? managementContextAccessor = null,
        IProjectPublicIdLookup? projectPublicIdLookup = null)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _wbsService = wbsService;
        _activityService = activityService;
        _dependencyService = dependencyService;
        _managementContextAccessor = managementContextAccessor;
        _projectPublicIdLookup = projectPublicIdLookup;
    }

    public async Task<Result<PlanDto>> CreateAsync(CreatePlanDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (_httpContextAccessor.HttpContext?.User.HasClaim("permission", "ProjectPlan.Create") != true)
            return Result<PlanDto>.Failure(Error.Forbidden("Project Plan creation permission is required."));

        if (dto.PlanType is null)
        {
            return Result<PlanDto>.Failure(Error.Validation("A plan type is required"));
        }

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<PlanDto>.Failure(Error.Validation("Plan name is required"));
        }

        if (dto.ProjectId == Guid.Empty)
        {
            return Result<PlanDto>.Failure(Error.Validation("A project is required"));
        }

        var tenantId = GetCurrentTenantId();
        if (!tenantId.HasValue)
            return Result<PlanDto>.Failure(Error.Forbidden("A trusted tenant context is required."));

        var managementContext = _managementContextAccessor?.Current;
        if (managementContext is null
            || managementContext.TenantId != tenantId.Value
            || managementContext.EffectiveReadOnly)
        {
            return Result<PlanDto>.Failure(Error.Forbidden("A matching writable management context is required."));
        }

        if (!managementContext.Scope.ProjectIds.Contains(dto.ProjectId))
            return Result<PlanDto>.Failure(Error.NotFound("Project not found"));

        if (_projectPublicIdLookup is null)
            return Result<PlanDto>.Failure(Error.Forbidden("Project ownership could not be verified."));

        var projectPublicId = await _projectPublicIdLookup.FindPublicIdAsync(
            dto.ProjectId,
            tenantId.Value,
            cancellationToken);
        if (!projectPublicId.HasValue)
            return Result<PlanDto>.Failure(Error.NotFound("Project not found"));

        var duplicateName = await _context.Set<Plan>()
            .AnyAsync(p => p.ProjectId == dto.ProjectId && p.PlanType == dto.PlanType.Value && p.Name == name, cancellationToken);
        if (duplicateName)
            return Result<PlanDto>.Failure(Error.Conflict("A plan with this name and type already exists for the project"));

        var plan = new Plan
        {
            TenantId = tenantId.Value,
            ProjectId = dto.ProjectId,
            PlanType = dto.PlanType.Value,
            Name = name,
            Description = dto.Description?.Trim(),
            Status = PlanStatus.Draft,
            CreatedBy = userId
        };
        _context.Set<Plan>().Add(plan);

        var workingVersion = new PlanVersion
        {
            TenantId = tenantId.Value,
            PlanId = plan.Id,
            VersionNumber = 1,
            Type = PlanVersionType.Working,
            Status = PlanVersionStatus.Active,
            Name = "Working v1",
            CreatedBy = userId
        };
        _context.Set<PlanVersion>().Add(workingVersion);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique project/type/name index guards a concurrent duplicate.
            return Result<PlanDto>.Failure(Error.Conflict("A plan with this name and type already exists for the project"));
        }

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Plan",
            EntityId = plan.Id,
            Action = "Create",
            UserId = TryParseGuid(userId),
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        plan.Versions = new List<PlanVersion> { workingVersion };
        return Result<PlanDto>.Success(plan.ToDto());
    }

    public async Task<Result<IReadOnlyList<PlanSummaryDto>>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var plans = await _context.Set<Plan>()
            .Where(p => p.ProjectId == projectId)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .AsNoTracking()
            .Select(plan => new PlanSummaryDto
            {
                Id = plan.Id,
                ProjectId = plan.ProjectId,
                PlanType = plan.PlanType,
                Name = plan.Name,
                Description = plan.Description,
                Status = plan.Status,
                VersionCount = plan.Versions.Count(version => !version.IsDeleted),
                CreatedAt = plan.CreatedAt,
                UpdatedAt = plan.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<PlanSummaryDto>>.Success(plans);
    }

    public async Task<Result<PlanningBootstrapDto>> GetBootstrapAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var plansResult = await GetByProjectAsync(projectId, cancellationToken);
        if (plansResult.IsFailure) return Result<PlanningBootstrapDto>.Failure(plansResult.Error!);

        var plans = plansResult.Value!;
        if (plans.Count == 0)
        {
            return Result<PlanningBootstrapDto>.Success(new PlanningBootstrapDto { Plans = plans });
        }

        if (_wbsService is null || _activityService is null || _dependencyService is null)
        {
            return Result<PlanningBootstrapDto>.Failure(Error.Validation("Planning services are not configured."));
        }

        // Preserve the workspace's existing default selection: newest plan first.
        var selectedPlanId = plans[0].Id;
        var planResult = await GetByIdAsync(selectedPlanId, cancellationToken);
        if (planResult.IsFailure) return Result<PlanningBootstrapDto>.Failure(planResult.Error!);

        var workingVersion = planResult.Value!.Versions
            .FirstOrDefault(version => version.Type == PlanVersionType.Working && version.Status == PlanVersionStatus.Active);
        if (workingVersion is null) return Result<PlanningBootstrapDto>.Failure(Error.NotFound("No active working version found"));

        var wbsResult = await _wbsService.GetTreeAsync(selectedPlanId, cancellationToken);
        if (wbsResult.IsFailure) return Result<PlanningBootstrapDto>.Failure(wbsResult.Error!);
        var activitiesResult = await _activityService.GetByPlanAsync(selectedPlanId, cancellationToken);
        if (activitiesResult.IsFailure) return Result<PlanningBootstrapDto>.Failure(activitiesResult.Error!);
        var dependenciesResult = await _dependencyService.GetByPlanAsync(selectedPlanId, cancellationToken);
        if (dependenciesResult.IsFailure) return Result<PlanningBootstrapDto>.Failure(dependenciesResult.Error!);

        return Result<PlanningBootstrapDto>.Success(new PlanningBootstrapDto
        {
            Plans = plans,
            SelectedPlan = planResult.Value,
            WorkingVersion = workingVersion,
            Wbs = wbsResult.Value!,
            Activities = activitiesResult.Value!,
            Dependencies = dependenciesResult.Value!,
        });
    }

    public async Task<Result<PlanDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _context.Set<Plan>()
            .Include(p => p.Versions)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (plan is null) return Result<PlanDto>.Failure(Error.NotFound("Plan not found"));

        return Result<PlanDto>.Success(plan.ToDto());
    }

    public async Task<Result<PlanVersionDto>> GetActiveWorkingVersionAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var plan = await _context.Set<Plan>().AnyAsync(p => p.Id == planId, cancellationToken);
        if (!plan) return Result<PlanVersionDto>.Failure(Error.NotFound("Plan not found"));

        var version = await _context.Set<PlanVersion>()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.PlanId == planId && v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active, cancellationToken);
        if (version is null) return Result<PlanVersionDto>.Failure(Error.NotFound("No active working version found"));

        return Result<PlanVersionDto>.Success(version.ToDto());
    }

    private Guid? GetCurrentTenantId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true
            && Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId)
            && tenantId != Guid.Empty
                ? tenantId
                : null;
    }

    private static Guid? TryParseGuid(string value)
    {
        return GuidHelper.TryParse(value);
    }
}
