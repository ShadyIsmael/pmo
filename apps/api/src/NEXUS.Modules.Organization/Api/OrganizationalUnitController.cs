using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Organization.Application;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.Organization.Api;

[ApiController]
[Route("api/organization")]
[Authorize]
public sealed class OrganizationalUnitController : ControllerBase
{
    private readonly IOrganizationalUnitService _service;
    private readonly IManagementContextAccessor _managementContext;

    public OrganizationalUnitController(IOrganizationalUnitService service, IManagementContextAccessor managementContext)
    {
        _service = service;
        _managementContext = managementContext;
    }

    [HttpGet("units")]
    [Authorize(Policy = "Settings.View")]
    public async Task<IActionResult> GetUnits([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetUnitsAsync(includeInactive, cancellationToken));
    }

    [HttpGet("units/project-lookup")]
    [Authorize(Policy = "WorkflowLookup.Project.OrganizationalUnits")]
    public async Task<IActionResult> GetProjectLookup(CancellationToken cancellationToken) =>
        Ok(await _service.GetProjectLookupAsync(cancellationToken));

    [HttpGet("units/resource-lookup")]
    [Authorize(Policy = "WorkflowLookup.Resource.OrganizationalUnits")]
    public async Task<IActionResult> GetResourceLookup(CancellationToken cancellationToken) =>
        Ok(await _service.GetProjectLookupAsync(cancellationToken));

    [HttpGet("units/{id:guid}")]
    [Authorize(Policy = "Settings.View")]
    public async Task<IActionResult> GetUnit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetUnitAsync(id, cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("units")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> CreateUnit(SaveOrganizationalUnitDto request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        var result = await _service.CreateUnitAsync(request, actor, cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPut("units/{id:guid}")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> UpdateUnit(Guid id, SaveOrganizationalUnitDto request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        var result = await _service.UpdateUnitAsync(id, request, actor, cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("units/{id:guid}/deactivate")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> DeactivateUnit(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        return FromResult(await _service.DeactivateUnitAsync(id, actor, cancellationToken));
    }

    [HttpPost("units/{id:guid}/reactivate")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> ReactivateUnit(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        return FromResult(await _service.ReactivateUnitAsync(id, actor, cancellationToken));
    }

    [HttpDelete("units/{id:guid}")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> DeleteUnit(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        return FromResult(await _service.DeleteUnitAsync(id, actor, cancellationToken));
    }

    [HttpGet("unit-types")]
    [Authorize(Policy = "Settings.View")]
    public async Task<IActionResult> GetTypes([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default) => Ok(await _service.GetTypesAsync(includeInactive, cancellationToken));

    [HttpPost("unit-types")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> CreateType(SaveOrganizationalUnitTypeDto request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        var result = await _service.CreateTypeAsync(request, actor, cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPut("unit-types/{id:guid}")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> UpdateType(Guid id, SaveOrganizationalUnitTypeDto request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        var result = await _service.UpdateTypeAsync(id, request, actor, cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpGet("memberships")]
    [Authorize(Policy = "Settings.View")]
    public async Task<IActionResult> GetMemberships([FromQuery] Guid? organizationalUnitId = null, [FromQuery] Guid? resourceId = null, [FromQuery] bool includeHistory = false, CancellationToken cancellationToken = default) => Ok(await _service.GetMembershipsAsync(organizationalUnitId, resourceId, includeHistory, cancellationToken));

    [HttpPost("memberships")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> AssignMembership(SaveResourceOrganizationalUnitDto request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        var result = await _service.AssignMembershipAsync(request, actor, cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("memberships/{id:guid}/end")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> EndMembership(Guid id, [FromBody] EndEffectiveDateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        return FromResult(await _service.EndMembershipAsync(id, request.EffectiveTo, actor, cancellationToken));
    }

    [HttpGet("reporting-lines")]
    [Authorize(Policy = "Settings.View")]
    public async Task<IActionResult> GetReportingLines([FromQuery] Guid? resourceId = null, [FromQuery] bool includeHistory = false, CancellationToken cancellationToken = default) => Ok(await _service.GetReportingLinesAsync(resourceId, includeHistory, cancellationToken));

    [HttpPost("reporting-lines")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> AssignReportingLine(SaveResourceReportingLineDto request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        var result = await _service.AssignReportingLineAsync(request, actor, cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("reporting-lines/{id:guid}/end")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> EndReportingLine(Guid id, [FromBody] EndEffectiveDateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        return FromResult(await _service.EndReportingLineAsync(id, request.EffectiveTo, actor, cancellationToken));
    }

    private bool TryGetActor(out string actorId)
    {
        actorId = string.Empty;
        var tenantClaim = User.FindFirstValue("tenantId");
        var context = _managementContext.Current;
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId == Guid.Empty
            || !Guid.TryParse(tenantClaim, out var tenantId) || tenantId == Guid.Empty
            || context is null || context.EffectiveReadOnly || context.UserId != userId || context.TenantId != tenantId)
        {
            return false;
        }

        actorId = userId.ToString("D");
        return true;
    }
    private IActionResult FromResult(NEXUS.SharedKernel.Result result) => result.IsSuccess ? NoContent() : ToError(result.Error!);
    private IActionResult ToError(NEXUS.SharedKernel.Error error) => error.StatusCode switch { 404 => NotFound(new { error = error.Message }), 409 => Conflict(new { error = error.Message }), 403 => Forbid(), _ => BadRequest(new { error = error.Message }) };
}

public sealed class EndEffectiveDateRequest
{
    public DateTime EffectiveTo { get; set; }
}
