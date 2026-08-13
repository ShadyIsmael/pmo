using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.WorkItems.Application;

namespace NEXUS.Modules.WorkItems.Api;

[ApiController]
[Route("api/work-items")]
[Authorize]
public class WorkItemController : ControllerBase
{
    private readonly IWorkItemService _workItemService;

    public WorkItemController(IWorkItemService workItemService)
    {
        _workItemService = workItemService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workItemService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? projectId = null, [FromQuery] string? status = null, [FromQuery] string? type = null, CancellationToken cancellationToken = default)
    {
        var result = await _workItemService.GetPagedAsync(page, pageSize, projectId, status, type, cancellationToken);
        return Ok(result);
    }
}
