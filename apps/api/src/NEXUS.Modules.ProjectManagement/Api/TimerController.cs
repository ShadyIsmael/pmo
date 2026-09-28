using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/timer")]
[Authorize(Policy = "TimeTracking.Manage")]
public sealed class TimerController : ControllerBase
{
    private readonly ITimerService _service;
    public TimerController(ITimerService service) => _service = service;

    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken cancellationToken)
        => await ExecuteAsync((actor, tenant) => _service.GetCurrentAsync(actor, tenant, cancellationToken), value => Ok(value));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => await ExecuteAsync((actor, tenant) => _service.GetByIdAsync(id, actor, tenant, cancellationToken), value => Ok(value));

    [HttpPost("start")]
    public async Task<IActionResult> Start(CancellationToken cancellationToken)
        => await ExecuteAsync((actor, tenant) => _service.StartAsync(actor, tenant, cancellationToken), value => Ok(value));

    [HttpPost("{id:guid}/break")]
    public async Task<IActionResult> Break(Guid id, CancellationToken cancellationToken)
        => await ExecuteAsync((actor, tenant) => _service.BreakAsync(id, actor, tenant, cancellationToken), value => Ok(value));

    [HttpPost("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id, CancellationToken cancellationToken)
        => await ExecuteAsync((actor, tenant) => _service.PauseAsync(id, actor, tenant, cancellationToken), value => Ok(value));

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken)
        => await ExecuteAsync((actor, tenant) => _service.ResumeAsync(id, actor, tenant, cancellationToken), value => Ok(value));

    [HttpPost("{id:guid}/heartbeat")]
    public async Task<IActionResult> Heartbeat(Guid id, [FromBody] TimerHeartbeatDto dto, CancellationToken cancellationToken)
        => await ExecuteAsync((actor, tenant) => _service.HeartbeatAsync(id, dto, actor, tenant, cancellationToken), value => Ok(value));

    [HttpPost("{id:guid}/finish")]
    public async Task<IActionResult> Finish(Guid id, CancellationToken cancellationToken)
        => await ExecuteAsync((actor, tenant) => _service.FinishAsync(id, actor, tenant, cancellationToken), value => Ok(value));

    [HttpPost("{id:guid}/finalize")]
    public async Task<IActionResult> Finalize(Guid id, [FromBody] FinalizeTimerSessionDto dto, CancellationToken cancellationToken)
        => await ExecuteAsync((actor, tenant) => _service.FinalizeAsync(id, dto, actor, tenant, cancellationToken), value => Ok(value));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
        => await ExecuteAsync((actor, tenant) => _service.CancelAsync(id, actor, tenant, cancellationToken), value => Ok(value));

    [HttpGet("{id:guid}/eligible-projects")]
    public async Task<IActionResult> GetEligibleProjects(Guid id, CancellationToken cancellationToken)
        => await ExecuteAsync((actor, tenant) => _service.GetEligibleProjectsForFinalizeAsync(id, actor, tenant, cancellationToken), value => Ok(value));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Guid, Guid, Task<Result<T>>> action, Func<T, IActionResult> success)
    {
        if (!TryGetContext(out var actorId, out var tenantId)) return Unauthorized();
        var result = await action(actorId, tenantId);
        return result.IsSuccess ? success(result.Value!) : ErrorResult(result.Error!);
    }

    private bool TryGetContext(out Guid actorId, out Guid tenantId)
    {
        actorId = Guid.Empty;
        tenantId = Guid.Empty;
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId)
               && Guid.TryParse(User.FindFirstValue("tenantId"), out tenantId);
    }

    private IActionResult ErrorResult(Error error) => error.StatusCode switch
    {
        401 => Unauthorized(new { error = error.Message, code = error.Code }),
        403 => Forbid(),
        404 => NotFound(new { error = error.Message, code = error.Code }),
        409 => Conflict(new { error = error.Message, code = error.Code }),
        _ => BadRequest(new { error = error.Message, code = error.Code })
    };
}
