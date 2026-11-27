using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

[ApiController]
[Route("api/projects/{projectId:guid}/cash-flow")]
[Authorize]
[Authorize(Policy = "ProjectFinancialFoundation.View")]
[Authorize(Policy = "ProjectSettings.View")]
public sealed class ProjectCashFlowController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IManagementContextAccessor _management;

    public ProjectCashFlowController(AppDbContext db, IManagementContextAccessor management)
    {
        _db = db;
        _management = management;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        Guid projectId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] CashFlowGranularity granularity = CashFlowGranularity.Month,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantId(out var tenantId)) return Unauthorized();
        if (!IsInProjectScope(projectId)) return NotFound();
        if (from is null || to is null || from > to)
            return BadRequest(new { error = "A valid from/to date range is required." });
        if (!Enum.IsDefined(granularity))
            return BadRequest(new { error = "Granularity must be Day or Month." });

        var project = await _db.Projects.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId
                && item.TenantId == tenantId
                && !item.IsDeleted,
                cancellationToken);
        if (project is null) return NotFound();

        var report = await ProjectCashFlowReader.ReadProjectAsync(
            _db,
            tenantId,
            project.Id,
            project.Currency,
            from.Value,
            to.Value,
            granularity,
            cancellationToken);

        return Ok(report);
    }

    private bool TryGetTenantId(out Guid tenantId) =>
        Guid.TryParse(User.FindFirst("tenantId")?.Value, out tenantId);

    private bool IsInProjectScope(Guid projectId) =>
        _management.Current?.Scope.ProjectIds.Contains(projectId) == true;
}
