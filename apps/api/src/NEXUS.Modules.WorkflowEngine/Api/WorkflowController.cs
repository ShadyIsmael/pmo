using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.WorkflowEngine.Application;

namespace NEXUS.Modules.WorkflowEngine.Api;

[ApiController]
[Route("api/workflows")]
[Authorize]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowService _workflowService;

    public WorkflowController(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workflowService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _workflowService.GetPagedAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }
}
