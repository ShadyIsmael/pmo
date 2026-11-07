using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.IssueManagement.Application;

namespace NEXUS.Modules.IssueManagement.Api;

[ApiController]
[Route("api/issues")]
[Authorize(Policy = "Issue.View")]
public class IssueController : ControllerBase
{
    private readonly IIssueService _issueService;

    public IssueController(IIssueService issueService)
    {
        _issueService = issueService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _issueService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? projectId = null, [FromQuery] string? status = null, CancellationToken cancellationToken = default)
    {
        var result = await _issueService.GetPagedAsync(page, pageSize, projectId, status, cancellationToken);
        return Ok(result);
    }
}
