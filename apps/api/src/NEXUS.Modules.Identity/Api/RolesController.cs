using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Identity.Application;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Api;

[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IUserAdministrationService _userAdministrationService;
    private readonly IRoleAdministrationService _roleAdministrationService;

    public RolesController(IUserAdministrationService userAdministrationService, IRoleAdministrationService roleAdministrationService)
    {
        _userAdministrationService = userAdministrationService;
        _roleAdministrationService = roleAdministrationService;
    }

    /// <summary>
    /// Lists all roles available in the system for assignment to users. Role names and
    /// membership are administrative metadata, so the directory requires role-view
    /// permission rather than any authenticated session.
    /// </summary>
    [Authorize(Policy = "Roles.View")]
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var roles = await _userAdministrationService.GetRolesAsync(cancellationToken);
        return roles.IsSuccess ? Ok(roles.Value) : ToError(roles.Error);
    }

    [Authorize(Policy = "Roles.View")]
    [HttpGet("manage")]
    public async Task<IActionResult> GetManageable(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var tenantId)) return Forbid();
        var result = await _roleAdministrationService.GetManageableRolesAsync(actorId, tenantId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToError(result.Error);
    }

    [Authorize(Policy = "Roles.ConfigurePermissions")]
    [HttpGet("permissions")]
    public async Task<IActionResult> GetAssignablePermissions(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var tenantId)) return Forbid();
        var result = await _roleAdministrationService.GetAssignablePermissionsAsync(actorId, tenantId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToError(result.Error);
    }

    [Authorize(Policy = "Roles.View")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var tenantId)) return Forbid();
        var result = await _roleAdministrationService.GetByIdAsync(id, actorId, tenantId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToError(result.Error);
    }

    [Authorize(Policy = "Roles.Create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveRoleDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var tenantId)) return Forbid();
        var result = await _roleAdministrationService.CreateAsync(dto, actorId, tenantId, cancellationToken);
        if (result.IsFailure) return ToError(result.Error);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Policy = "Roles.View")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveRoleDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var tenantId)) return Forbid();
        var result = await _roleAdministrationService.UpdateAsync(id, dto, actorId, tenantId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToError(result.Error);
    }

    [Authorize(Policy = "Roles.Delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var tenantId)) return Forbid();
        var result = await _roleAdministrationService.DeleteAsync(id, actorId, tenantId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToError(result.Error);
    }

    private bool TryGetActor(out Guid actorId, out Guid tenantId)
    {
        actorId = Guid.Empty;
        tenantId = Guid.Empty;
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId)
            && Guid.TryParse(User.FindFirstValue("tenantId"), out tenantId);
    }

    private IActionResult ToError(Error? error) => error?.StatusCode switch
    {
        403 => StatusCode(StatusCodes.Status403Forbidden, new { error = error.Message, error.Code, error.FieldName, error.Details }),
        404 => NotFound(new { error = error.Message, error.Code, error.FieldName, error.Details }),
        409 => Conflict(new { error = error.Message, error.Code, error.FieldName, error.Details }),
        _ => BadRequest(new { error = error?.Message ?? "The role request is invalid", error?.Code, error?.FieldName, error?.Details })
    };
}
