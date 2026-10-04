using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ResourceManagement.Application;

namespace NEXUS.Modules.ResourceManagement.Api;

[Authorize]
[ApiController]
[Route("api/project-roles")]
public class ProjectRoleController : ControllerBase
{
    private readonly IProjectRoleService _service;

    public ProjectRoleController(IProjectRoleService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "Resource.View")]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var result = activeOnly
            ? await _service.GetActiveAsync(cancellationToken)
            : await _service.GetAllAsync(cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Returns the next generic project-role code so the create form can show a code
    /// before anything is saved. The {id:guid} route cannot shadow this because "next-code"
    /// is not a Guid.
    /// </summary>
    [HttpGet("next-code")]
    [Authorize(Policy = "Resource.Manage")]
    public async Task<IActionResult> GetNextCode(CancellationToken cancellationToken)
    {
        var code = await _service.GetNextCodeAsync(cancellationToken);
        return Ok(new { code });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Resource.View")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = "Resource.Manage")]
    public async Task<IActionResult> Create([FromBody] CreateLookupDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _service.CreateAsync(dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Resource.Manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLookupDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _service.UpdateAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/deactivate")]
    [Authorize(Policy = "Resource.Manage")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _service.DeactivateAsync(id, userId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return NoContent();
    }
}
