using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Approvals.Application;

namespace NEXUS.Modules.Approvals.Api;

[ApiController]
[Route("api/approvals")]
[Authorize]
public class ApprovalController : ControllerBase
{
    private readonly IApprovalService _approvalService;

    public ApprovalController(IApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _approvalService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, [FromQuery] Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var result = await _approvalService.GetPagedAsync(page, pageSize, status, userId, cancellationToken);
        return Ok(result);
    }
}
