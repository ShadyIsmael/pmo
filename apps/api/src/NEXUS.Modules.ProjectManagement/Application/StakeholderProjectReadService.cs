using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

/// <summary>
/// Read-only stakeholder self-service. Access is derived from the canonical
/// Resource.UserId link and an active Project People stakeholder relationship.
/// </summary>
public sealed class StakeholderProjectReadService : IStakeholderProjectReadService
{
    private readonly DbContext _context;
    private readonly IResourceReferenceLookup _resources;
    private readonly IUserReferenceLookup _users;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StakeholderProjectReadService(
        DbContext context,
        IResourceReferenceLookup resources,
        IUserReferenceLookup users,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _resources = resources;
        _users = users;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<PagedResult<StakeholderProjectViewDto>>> GetMyProjectsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var resourceId = await GetCurrentActiveResourceIdAsync(cancellationToken);
        if (!resourceId.HasValue)
            return Result<PagedResult<StakeholderProjectViewDto>>.Failure(Error.Forbidden("An active Resource and stakeholder assignment are required"));

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var projectIds = await ActiveStakeholderProjectIdsAsync(resourceId.Value, cancellationToken);
        var query = _context.Set<Project>().AsNoTracking().Where(project => projectIds.Contains(project.Id));
        var total = await query.CountAsync(cancellationToken);
        var projects = await query
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var views = await BuildViewsAsync(projects, includeHistory: false, cancellationToken);
        return Result<PagedResult<StakeholderProjectViewDto>>.Success(new PagedResult<StakeholderProjectViewDto>(views, page, pageSize, total));
    }

    public async Task<Result<StakeholderProjectViewDto>> GetMyProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var resourceId = await GetCurrentActiveResourceIdAsync(cancellationToken);
        if (!resourceId.HasValue)
            return Result<StakeholderProjectViewDto>.Failure(Error.Forbidden("An active Resource and stakeholder assignment are required"));

        var assigned = await _context.Set<ProjectStakeholder>().AsNoTracking().AnyAsync(
            stakeholder => stakeholder.ProjectId == projectId &&
                           stakeholder.ResourceId == resourceId.Value &&
                           stakeholder.RelationshipType == ProjectRelationshipType.Stakeholder &&
                           stakeholder.Status == ProjectStakeholderStatus.Active,
            cancellationToken);
        if (!assigned)
            return Result<StakeholderProjectViewDto>.Failure(Error.NotFound("Stakeholder project not found"));

        var project = await _context.Set<Project>().AsNoTracking().FirstOrDefaultAsync(project => project.Id == projectId, cancellationToken);
        if (project is null)
            return Result<StakeholderProjectViewDto>.Failure(Error.NotFound("Stakeholder project not found"));

        var views = await BuildViewsAsync([project], includeHistory: true, cancellationToken);
        return Result<StakeholderProjectViewDto>.Success(views[0]);
    }

    private async Task<Guid?> GetCurrentActiveResourceIdAsync(CancellationToken cancellationToken)
    {
        var claim = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(claim, out var userId)) return null;

        // FindAsync only returns active users in the current tenant. This is the
        // explicit User.IsActive + same-Tenant gate required by self-service.
        if (await _users.FindAsync(userId, cancellationToken) is null) return null;

        var resource = await _resources.FindByUserIdAsync(userId, cancellationToken);
        if (resource is null || resource.UserId != userId || !resource.IsActive ||
            string.Equals(resource.Status, "Inactive", StringComparison.OrdinalIgnoreCase)) return null;

        return resource.Id;
    }

    private async Task<List<Guid>> ActiveStakeholderProjectIdsAsync(Guid resourceId, CancellationToken cancellationToken) =>
        await _context.Set<ProjectStakeholder>().AsNoTracking()
            .Where(stakeholder => stakeholder.ResourceId == resourceId &&
                                  stakeholder.RelationshipType == ProjectRelationshipType.Stakeholder &&
                                  stakeholder.Status == ProjectStakeholderStatus.Active)
            .Select(stakeholder => stakeholder.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<StakeholderProjectViewDto>> BuildViewsAsync(
        IReadOnlyList<Project> projects,
        bool includeHistory,
        CancellationToken cancellationToken)
    {
        if (projects.Count == 0) return [];

        var projectIds = projects.Select(project => project.Id).ToList();
        var userIds = projects
            .SelectMany(project => new[] { project.ProjectManagerId, project.SponsorId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var users = await _users.GetManyAsync(userIds, cancellationToken);

        var reports = await _context.Set<ProjectStatusReport>().AsNoTracking()
            .Where(report => projectIds.Contains(report.ProjectId) && report.ReportState == ProjectStatusReportState.Published)
            .OrderByDescending(report => report.ReportDate)
            .ThenByDescending(report => report.CreatedAt)
            .ToListAsync(cancellationToken);

        var milestones = await _context.Set<Milestone>().AsNoTracking()
            .Where(milestone => projectIds.Contains(milestone.ProjectId))
            .OrderBy(milestone => milestone.DueDate.HasValue ? 0 : 1)
            .ThenBy(milestone => milestone.DueDate)
            .ThenBy(milestone => milestone.Name)
            .ToListAsync(cancellationToken);

        return projects.Select(project =>
        {
            var projectReports = reports.Where(report => report.ProjectId == project.Id).ToList();
            var latest = projectReports.FirstOrDefault();
            return new StakeholderProjectViewDto
            {
                Id = project.Id,
            PublicId = project.PublicId,
                Code = project.Code,
                Name = project.Name,
                Description = project.Description,
                Status = project.Status,
                Health = project.Health,
                Progress = project.Progress,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                ProjectManagerName = project.ProjectManagerId.HasValue && users.TryGetValue(project.ProjectManagerId.Value, out var manager) ? manager.FullName : null,
                SponsorName = project.SponsorId.HasValue && users.TryGetValue(project.SponsorId.Value, out var sponsor) ? sponsor.FullName : null,
                LatestPublishedStatusReport = latest is null ? null : MapLatest(latest),
                PublishedStatusHistory = includeHistory ? projectReports.Select(MapHistory).ToList() : [],
                Milestones = milestones.Where(milestone => milestone.ProjectId == project.Id).Select(MapMilestone).ToList()
            };
        }).ToList();
    }

    private static StakeholderPublishedStatusReportDto MapLatest(ProjectStatusReport report) => new()
    {
        Id = report.Id,
        ReportDate = report.ReportDate,
        PeriodFrom = report.PeriodFrom,
        PeriodTo = report.PeriodTo,
        ProjectLifecycleStatus = report.ProjectLifecycleStatusAtReport,
        OverallHealth = report.OverallHealth,
        OverallProgressPercent = report.OverallProgressPercent,
        ScheduleHealth = report.ScheduleHealth,
        CostHealth = report.CostHealth,
        ScopeHealth = report.ScopeHealth,
        QualityHealth = report.QualityHealth,
        ResourceHealth = report.ResourceHealth,
        ExecutiveSummary = report.ExecutiveSummary,
        KeyAchievements = report.KeyAchievements,
        ChallengesConcerns = report.ChallengesConcerns,
        NextPeriodPriorities = report.NextPeriodPriorities,
        PreparedByName = report.PreparedByNameSnapshot,
        PublishedByName = report.PublishedByNameSnapshot,
        PublishedAt = report.PublishedAt
    };

    private static StakeholderPublishedStatusHistoryDto MapHistory(ProjectStatusReport report) => new()
    {
        Id = report.Id,
        ReportDate = report.ReportDate,
        PeriodFrom = report.PeriodFrom,
        PeriodTo = report.PeriodTo,
        ProjectLifecycleStatus = report.ProjectLifecycleStatusAtReport,
        OverallHealth = report.OverallHealth,
        OverallProgressPercent = report.OverallProgressPercent,
        PublishedAt = report.PublishedAt
    };

    private static StakeholderProjectMilestoneDto MapMilestone(Milestone milestone) => new()
    {
        Id = milestone.Id,
        Name = milestone.Name,
        DueDate = milestone.DueDate,
        CompletedDate = milestone.CompletedDate,
        Status = milestone.Status,
        Progress = milestone.Progress
    };
}
