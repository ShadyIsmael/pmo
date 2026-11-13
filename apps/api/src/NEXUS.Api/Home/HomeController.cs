using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Home;

[ApiController]
[Route("api/home")]
[Authorize]
[Authorize(Policy = "Dashboard.View")]
public sealed class HomeController(
    AppDbContext db,
    IAuthorizationService authorization,
    IManagementContextAccessor managementContext) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<HomeSummaryDto>> GetSummary(
        [FromQuery] Guid? portfolioId = null,
        [FromQuery] Guid? programId = null,
        [FromQuery] Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        var canViewProjects = (await authorization.AuthorizeAsync(User, null, "Project.View")).Succeeded;
        var canViewRisks = (await authorization.AuthorizeAsync(User, null, "Risk.View")).Succeeded;

        var projectQuery = canViewProjects ? GetProjectQuery(portfolioId, programId, projectId) : null;
        var activeProjectQuery = projectQuery?.Where(project =>
            project.ProjectStatusId == null
            || db.ProjectStatusCatalogEntries.Any(status => status.TenantId == project.TenantId
                && status.Id == project.ProjectStatusId
                && status.ReportingCategory == ProjectStatusReportingCategories.InProgress));
        var activeProjectsCount = activeProjectQuery is null
            ? 0
            : await activeProjectQuery.CountAsync(cancellationToken);

        var healthRows = activeProjectQuery is null
            ? []
            : await activeProjectQuery
                .GroupBy(project => new { project.Health, project.RagStatus })
                .Select(group => new { group.Key.Health, group.Key.RagStatus, Count = group.Count() })
                .ToListAsync(cancellationToken);
        var healthCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["green"] = healthRows.Where(row => ProjectHealth(row.Health, row.RagStatus) == "green").Sum(row => row.Count),
            ["amber"] = healthRows.Where(row => ProjectHealth(row.Health, row.RagStatus) == "amber").Sum(row => row.Count),
            ["red"] = healthRows.Where(row => ProjectHealth(row.Health, row.RagStatus) == "red").Sum(row => row.Count),
            ["unknown"] = healthRows.Where(row => ProjectHealth(row.Health, row.RagStatus) == "unknown").Sum(row => row.Count)
        };

        var attentionProjects = activeProjectQuery is null
            ? []
            : await activeProjectQuery
                .Where(project =>
                    EF.Functions.Like(project.Health, "%red%") ||
                    EF.Functions.Like(project.RagStatus, "%red%") ||
                    EF.Functions.Like(project.Health, "%critical%") ||
                    EF.Functions.Like(project.RagStatus, "%critical%") ||
                    EF.Functions.Like(project.Health, "%amber%") ||
                    EF.Functions.Like(project.RagStatus, "%amber%") ||
                    EF.Functions.Like(project.Health, "%yellow%") ||
                    EF.Functions.Like(project.RagStatus, "%yellow%") ||
                    EF.Functions.Like(project.Health, "%warning%") ||
                    EF.Functions.Like(project.RagStatus, "%warning%") ||
                    EF.Functions.Like(project.Health, "%at risk%") ||
                    EF.Functions.Like(project.RagStatus, "%at risk%"))
                .OrderBy(project => project.Health)
                .ThenBy(project => project.Name)
                .Take(10)
                .Select(project => new HomeProjectSummaryDto(project.Id, project.Name, project.Progress, project.Health, project.RagStatus))
                .ToListAsync(cancellationToken);

        var budgetByCurrency = activeProjectQuery is null
            ? []
            : await activeProjectQuery
                .GroupBy(project => string.IsNullOrWhiteSpace(project.Currency) ? "—" : project.Currency)
                .OrderBy(group => group.Key)
                .Select(group => new HomeBudgetSummaryDto(
                    group.Key,
                    group.Sum(project => project.Budget ?? 0),
                    group.Sum(project => project.ActualCost ?? 0)))
                .ToListAsync(cancellationToken);

        var riskQuery = canViewRisks && projectQuery is not null
            ? db.Set<Risk>()
                .AsNoTracking()
                .Where(risk => risk.Status != RiskStatus.Closed && risk.Status != RiskStatus.Mitigated && projectQuery.Select(project => project.Id).Contains(risk.ProjectId))
            : null;
        var openRisksCount = riskQuery is null ? 0 : await riskQuery.CountAsync(cancellationToken);
        var criticalOpenRisksCount = riskQuery is null ? 0 : await riskQuery.CountAsync(risk => risk.RiskLevel == RiskLevel.Critical, cancellationToken);
        var topRisks = riskQuery is null
            ? []
            : await riskQuery
                .Join(projectQuery!, risk => risk.ProjectId, project => project.Id, (risk, project) => new { risk, project.Name })
                .OrderByDescending(row => row.risk.RiskLevel)
                .ThenByDescending(row => row.risk.UpdatedAt ?? row.risk.CreatedAt)
                .Take(10)
                .Select(row => new HomeRiskSummaryDto(
                    row.risk.Id,
                    row.risk.Title,
                    row.Name,
                    row.risk.RiskLevel.ToString(),
                    row.risk.Status.ToString()))
                .ToListAsync(cancellationToken);

        return Ok(new HomeSummaryDto(
            activeProjectsCount,
            healthCounts["amber"] + healthCounts["red"],
            openRisksCount,
            criticalOpenRisksCount,
            healthCounts,
            budgetByCurrency,
            attentionProjects,
            topRisks));
    }

    private IQueryable<Project> GetProjectQuery(Guid? portfolioId, Guid? programId, Guid? projectId)
    {
        var query = db.Set<Project>().AsNoTracking();
        var scope = managementContext.Current?.Scope;
        if (scope is not null) query = query.Where(project => scope.ProjectIds.Contains(project.Id));
        if (portfolioId.HasValue) query = query.Where(project => project.PortfolioId == portfolioId.Value);
        if (programId.HasValue) query = query.Where(project => project.ProgramId == programId.Value);
        if (projectId.HasValue) query = query.Where(project => project.Id == projectId.Value);

        return query;
    }

    private static string ProjectHealth(string? health, string? ragStatus)
    {
        var value = $"{health} {ragStatus}".ToLowerInvariant();
        if (value.Contains("red") || value.Contains("critical")) return "red";
        if (value.Contains("amber") || value.Contains("yellow") || value.Contains("warning") || value.Contains("at risk")) return "amber";
        if (value.Contains("green") || value.Contains("healthy") || value.Contains("good")) return "green";
        return "unknown";
    }

}
