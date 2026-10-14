using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Identity.Application;

namespace NEXUS.Modules.Identity.Api;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IUserAdministrationService _userAdministrationService;

    public UsersController(IUserRepository userRepository, IUserAdministrationService userAdministrationService)
    {
        _userRepository = userRepository;
        _userAdministrationService = userAdministrationService;
    }

    /// <summary>
    /// Lightweight user lookup used by pickers/selectors across the UI.
    /// Optionally filter by role name (e.g. "project.manager").
    /// </summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookup([FromQuery] string? role = null, CancellationToken cancellationToken = default)
    {
        var users = string.IsNullOrWhiteSpace(role)
            ? await _userRepository.GetActiveAsync(cancellationToken)
            : await _userRepository.GetByRoleNameAsync(role, cancellationToken);

        var dtos = users.Select(u => new UserLookupDto
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            JobTitle = u.JobTitle
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Paginated user administration list with assigned roles.
    /// Requires the "users.read" permission.
    /// </summary>
    [Authorize(Policy = "Users.Read")]
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _userAdministrationService.GetPagedAsync(page, pageSize, search, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a user and assigns roles (including the Project Manager role).
    /// Requires the "users.write" permission.
    /// </summary>
    [Authorize(Policy = "Users.Write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto, CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";
        var result = await _userAdministrationService.CreateAsync(dto, actorId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                409 => Conflict(new { error = result.Error.Message, code = result.Error.Code }),
                403 => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error.Message, code = result.Error.Code }),
                _ => BadRequest(new { error = result.Error?.Message, code = result.Error?.Code })
            };
        return CreatedAtAction(nameof(GetPaged), new { page = 1, pageSize = 20 }, result.Value);
    }

    [Authorize(Policy = "Users.ResetPassword")]
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";
        var result = await _userAdministrationService.ResetPasswordAsync(id, actorId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                403 => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error.Message, code = result.Error.Code }),
                404 => NotFound(new { error = result.Error.Message, code = result.Error.Code }),
                409 => Conflict(new { error = result.Error.Message, code = result.Error.Code }),
                _ => BadRequest(new { error = result.Error?.Message, code = result.Error?.Code })
            };
        return Ok(result.Value);
    }

    /// <summary>
    /// Updates a user profile, active status and role assignments.
    /// Requires the "users.write" permission.
    /// </summary>
    [Authorize(Policy = "Users.Write")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto, CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";
        var result = await _userAdministrationService.UpdateAsync(id, dto, actorId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                403 => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [Authorize(Policy = "Users.Write")]
    [HttpGet("{id:guid}/permissions")]
    public async Task<IActionResult> GetPermissions(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out _, out var tenantId))
            return Forbid();

        var result = await _userAdministrationService.GetUserPermissionsAsync(id, tenantId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode == 404 ? NotFound(new { error = result.Error.Message }) : BadRequest(new { error = result.Error?.Message });

        return Ok(result.Value);
    }

    [Authorize(Policy = "Users.Write")]
    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> UpdatePermissions(Guid id, [FromBody] UpdateUserPermissionsDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var tenantId))
            return Forbid();

        var result = await _userAdministrationService.UpdateUserPermissionsAsync(id, tenantId, dto, actorId.ToString(), cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                403 => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error.Message }),
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };

        return Ok(result.Value);
    }

    private bool TryGetActor(out Guid actorId, out Guid tenantId)
    {
        actorId = Guid.Empty;
        tenantId = Guid.Empty;
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId)
            && Guid.TryParse(User.FindFirstValue("tenantId"), out tenantId);
    }

}
