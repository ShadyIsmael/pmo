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
    /// Lists all roles available in the system for assignment to users.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var roles = await _userAdministrationService.GetRolesAsync(cancellationToken);
        return Ok(roles);
    }

    [Authorize(Policy = "Users.Write")]
    [HttpGet("manage")]
    public async Task<IActionResult> GetManageable(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var tenantId)) return Forbid();
        var result = await _roleAdministrationService.GetManageableRolesAsync(actorId, tenantId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToError(result.Error);
    }

    [Authorize(Policy = "Users.Write")]
    [HttpGet("permissions")]
    public async Task<IActionResult> GetAssignablePermissions(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var tenantId)) return Forbid();
        var result = await _roleAdministrationService.GetAssignablePermissionsAsync(actorId, tenantId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToError(result.Error);
    }

    [Authorize(Policy = "Users.Write")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var tenantId)) return Forbid();
        var result = await _roleAdministrationService.GetByIdAsync(id, actorId, tenantId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToError(result.Error);
    }

    [Authorize(Policy = "Users.Write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveRoleDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var tenantId)) return Forbid();
        var result = await _roleAdministrationService.CreateAsync(dto, actorId, tenantId, cancellationToken);
        if (result.IsFailure) return ToError(result.Error);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Policy = "Users.Write")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveRoleDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var tenantId)) return Forbid();
        var result = await _roleAdministrationService.UpdateAsync(id, dto, actorId, tenantId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToError(result.Error);
    }

    [Authorize(Policy = "Users.Write")]
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
        403 => StatusCode(StatusCodes.Status403Forbidden, new { error = error.Message }),
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message ?? "The role request is invalid" })
    };
}
