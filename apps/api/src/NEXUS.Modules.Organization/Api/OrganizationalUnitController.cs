using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Organization.Application;

namespace NEXUS.Modules.Organization.Api;

[ApiController]
[Route("api/organization")]
[Authorize]
public sealed class OrganizationalUnitController : ControllerBase
{
    private readonly IOrganizationalUnitService _service;

    public OrganizationalUnitController(IOrganizationalUnitService service) => _service = service;

    [HttpGet("units")]
    [Authorize(Policy = "Settings.Read")]
    public async Task<IActionResult> GetUnits([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default) => Ok(await _service.GetUnitsAsync(includeInactive, cancellationToken));

    [HttpGet("units/{id:guid}")]
    [Authorize(Policy = "Settings.Read")]
    public async Task<IActionResult> GetUnit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetUnitAsync(id, cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("units")]
    [Authorize(Policy = "Settings.Write")]
    public async Task<IActionResult> CreateUnit(SaveOrganizationalUnitDto request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateUnitAsync(request, Actor(), cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPut("units/{id:guid}")]
    [Authorize(Policy = "Settings.Write")]
    public async Task<IActionResult> UpdateUnit(Guid id, SaveOrganizationalUnitDto request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateUnitAsync(id, request, Actor(), cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("units/{id:guid}/deactivate")]
    [Authorize(Policy = "Settings.Write")]
    public async Task<IActionResult> DeactivateUnit(Guid id, CancellationToken cancellationToken) => FromResult(await _service.DeactivateUnitAsync(id, Actor(), cancellationToken));

    [HttpPost("units/{id:guid}/reactivate")]
    [Authorize(Policy = "Settings.Write")]
    public async Task<IActionResult> ReactivateUnit(Guid id, CancellationToken cancellationToken) => FromResult(await _service.ReactivateUnitAsync(id, Actor(), cancellationToken));

    [HttpDelete("units/{id:guid}")]
    [Authorize(Policy = "Settings.Write")]
    public async Task<IActionResult> DeleteUnit(Guid id, CancellationToken cancellationToken) => FromResult(await _service.DeleteUnitAsync(id, Actor(), cancellationToken));

    [HttpGet("unit-types")]
    [Authorize(Policy = "Settings.Read")]
    public async Task<IActionResult> GetTypes([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default) => Ok(await _service.GetTypesAsync(includeInactive, cancellationToken));

    [HttpPost("unit-types")]
    [Authorize(Policy = "Settings.Write")]
    public async Task<IActionResult> CreateType(SaveOrganizationalUnitTypeDto request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateTypeAsync(request, Actor(), cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPut("unit-types/{id:guid}")]
    [Authorize(Policy = "Settings.Write")]
    public async Task<IActionResult> UpdateType(Guid id, SaveOrganizationalUnitTypeDto request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateTypeAsync(id, request, Actor(), cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpGet("memberships")]
    [Authorize(Policy = "Settings.Read")]
    public async Task<IActionResult> GetMemberships([FromQuery] Guid? organizationalUnitId = null, [FromQuery] Guid? resourceId = null, [FromQuery] bool includeHistory = false, CancellationToken cancellationToken = default) => Ok(await _service.GetMembershipsAsync(organizationalUnitId, resourceId, includeHistory, cancellationToken));

    [HttpPost("memberships")]
    [Authorize(Policy = "Settings.Write")]
    public async Task<IActionResult> AssignMembership(SaveResourceOrganizationalUnitDto request, CancellationToken cancellationToken)
    {
        var result = await _service.AssignMembershipAsync(request, Actor(), cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("memberships/{id:guid}/end")]
    [Authorize(Policy = "Settings.Write")]
    public async Task<IActionResult> EndMembership(Guid id, [FromBody] EndEffectiveDateRequest request, CancellationToken cancellationToken) => FromResult(await _service.EndMembershipAsync(id, request.EffectiveTo, Actor(), cancellationToken));

    [HttpGet("reporting-lines")]
    [Authorize(Policy = "Settings.Read")]
    public async Task<IActionResult> GetReportingLines([FromQuery] Guid? resourceId = null, [FromQuery] bool includeHistory = false, CancellationToken cancellationToken = default) => Ok(await _service.GetReportingLinesAsync(resourceId, includeHistory, cancellationToken));

    [HttpPost("reporting-lines")]
    [Authorize(Policy = "Settings.Write")]
    public async Task<IActionResult> AssignReportingLine(SaveResourceReportingLineDto request, CancellationToken cancellationToken)
    {
        var result = await _service.AssignReportingLineAsync(request, Actor(), cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("reporting-lines/{id:guid}/end")]
    [Authorize(Policy = "Settings.Write")]
    public async Task<IActionResult> EndReportingLine(Guid id, [FromBody] EndEffectiveDateRequest request, CancellationToken cancellationToken) => FromResult(await _service.EndReportingLineAsync(id, request.EffectiveTo, Actor(), cancellationToken));

    private string Actor() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "System";
    private IActionResult FromResult(NEXUS.SharedKernel.Result result) => result.IsSuccess ? NoContent() : ToError(result.Error!);
    private IActionResult ToError(NEXUS.SharedKernel.Error error) => error.StatusCode switch { 404 => NotFound(new { error = error.Message }), 409 => Conflict(new { error = error.Message }), 403 => Forbid(), _ => BadRequest(new { error = error.Message }) };
}

public sealed class EndEffectiveDateRequest
{
    public DateTime EffectiveTo { get; set; }
}
