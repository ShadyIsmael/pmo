using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;

namespace NEXUS.Modules.ResourceManagement.Api;

[Authorize]
[ApiController]
[Route("api/resource-analytics/cost-utilization")]
public sealed class CostUtilizationAnalyticsController : ControllerBase
{
    private readonly ICostUtilizationAnalyticsService _service;

    public CostUtilizationAnalyticsController(ICostUtilizationAnalyticsService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = "Resource.View")]
    public async Task<IActionResult> Get([FromQuery] CostUtilizationFilterDto filter, CancellationToken cancellationToken)
        => await GetReportAsync(filter, forExport: false, cancellationToken);

    [HttpGet("export-data")]
    [Authorize(Policy = "CostUtilization.Export")]
    public async Task<IActionResult> GetExportData([FromQuery] CostUtilizationFilterDto filter, CancellationToken cancellationToken)
        => await GetReportAsync(filter, forExport: true, cancellationToken);

    private async Task<IActionResult> GetReportAsync(
        CostUtilizationFilterDto filter,
        bool forExport,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)
            || !Guid.TryParse(User.FindFirstValue("tenantId"), out var tenantId))
            return Unauthorized();

        var result = forExport
            ? await _service.GetForExportAsync(filter, actorId, tenantId, cancellationToken)
            : await _service.GetAsync(filter, actorId, tenantId, cancellationToken);
        if (result.IsSuccess) return Ok(result.Value);
        return result.Error?.StatusCode switch
        {
            403 => Forbid(),
            409 => Conflict(new { error = result.Error.Message, code = result.Error.Code }),
            _ => BadRequest(new { error = result.Error?.Message, code = result.Error?.Code })
        };
    }
}
