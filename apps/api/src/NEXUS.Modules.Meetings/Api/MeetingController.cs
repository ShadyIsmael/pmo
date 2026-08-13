using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Meetings.Application;

namespace NEXUS.Modules.Meetings.Api;

[ApiController]
[Route("api/meetings")]
[Authorize]
public class MeetingController : ControllerBase
{
    private readonly IMeetingService _meetingService;

    public MeetingController(IMeetingService meetingService)
    {
        _meetingService = meetingService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _meetingService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        var result = await _meetingService.GetPagedAsync(page, pageSize, projectId, cancellationToken);
        return Ok(result);
    }
}
