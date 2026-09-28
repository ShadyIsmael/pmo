using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/time-entries")]
[Authorize]
public sealed class TimeEntryController : ControllerBase
{
    private readonly ITimeEntryService _service;

    public TimeEntryController(ITimeEntryService service) => _service = service;

    [HttpPost]
    [Authorize(Policy = "TimeTracking.Manage")]
    public async Task<IActionResult> Create([FromBody] CreateTimeEntryDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var actorId, out var tenantId)) return Unauthorized();
        var result = await _service.CreateManualAsync(dto, actorId, tenantId, cancellationToken);
        return ToActionResult(result, () => CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "TimeTracking.Manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTimeEntryDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var actorId, out var tenantId)) return Unauthorized();
        var result = await _service.UpdateManualAsync(id, dto, actorId, tenantId, cancellationToken);
        return ToActionResult(result, () => Ok(result.Value));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "TimeTracking.Manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var actorId, out var tenantId)) return Unauthorized();
        var result = await _service.DeleteAsync(id, actorId, tenantId, cancellationToken);
        if (result.IsSuccess) return NoContent();
        return ErrorResult(result.Error!);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "TimeTracking.View")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var actorId, out var tenantId)) return Unauthorized();
        var result = await _service.GetByIdAsync(id, actorId, tenantId, cancellationToken);
        return ToActionResult(result, () => Ok(result.Value));
    }

    [HttpGet]
    [Authorize(Policy = "TimeTracking.View")]
    public async Task<IActionResult> List([FromQuery] TimeEntryFilterDto filter, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var actorId, out var tenantId)) return Unauthorized();
        return Ok(await _service.ListAsync(filter, actorId, tenantId, cancellationToken));
    }

    [HttpGet("summary")]
    [Authorize(Policy = "TimeTracking.View")]
    public async Task<IActionResult> Summary([FromQuery] TimeEntryFilterDto filter, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var actorId, out var tenantId)) return Unauthorized();
        var result = await _service.GetSummaryAsync(filter, actorId, tenantId, cancellationToken);
        return ToActionResult(result, () => Ok(result.Value));
    }

    private bool TryGetContext(out Guid actorId, out Guid tenantId)
    {
        actorId = Guid.Empty;
        tenantId = Guid.Empty;
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId)
               && Guid.TryParse(User.FindFirstValue("tenantId"), out tenantId);
    }

    private IActionResult ToActionResult<T>(NEXUS.SharedKernel.Result<T> result, Func<IActionResult> success)
        => result.IsSuccess ? success() : ErrorResult(result.Error!);

    private IActionResult ErrorResult(NEXUS.SharedKernel.Error error)
        => error.StatusCode switch
        {
            401 => Unauthorized(new { error = error.Message, code = error.Code }),
            403 => Forbid(),
            404 => NotFound(new { error = error.Message, code = error.Code }),
            409 => Conflict(new { error = error.Message, code = error.Code }),
            _ => BadRequest(new { error = error.Message, code = error.Code })
        };
}
