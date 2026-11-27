using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

[ApiController]
[Route("api/projects/{projectId:guid}/financial-reporting")]
[Authorize]
public sealed class ProjectFinancialReportingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IManagementContextAccessor _management;

    public ProjectFinancialReportingController(AppDbContext db, IManagementContextAccessor management)
    {
        _db = db;
        _management = management;
    }

    [HttpGet]
    [Authorize(Policy = "ProjectFinancialFoundation.View")]
    [Authorize(Policy = "ProjectSettings.View")]
    public async Task<IActionResult> Get(
        Guid projectId,
        [FromQuery] DateTime? periodFrom,
        [FromQuery] DateTime? periodTo,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId)) return Unauthorized();
        if (!IsInProjectScope(projectId)) return NotFound();

        var project = await _db.Projects.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (project is null) return NotFound();

        var projectData = new ProjectFinancialDataAdapter(project);
        var report = await FinancialReportingCalculator.CalculateAsync(
            _db, projectData, periodFrom, periodTo, cancellationToken);

        return Ok(report);
    }

    private bool TryGetTenantId(out Guid tenantId) => Guid.TryParse(User.FindFirst("tenantId")?.Value, out tenantId);

    private bool IsInProjectScope(Guid projectId) => _management.Current?.Scope.ProjectIds.Contains(projectId) == true;

    private sealed record ProjectFinancialDataAdapter(Project Project) : IProjectFinancialData
    {
        public Guid Id => Project.Id;
        public Guid TenantId => Project.TenantId;
        public string Code => Project.Code;
        public string Name => Project.Name;
        public decimal Budget => Project.Budget ?? 0m;
        public string Currency => Project.Currency;
        public bool IsDeleted => Project.IsDeleted;
    }
}