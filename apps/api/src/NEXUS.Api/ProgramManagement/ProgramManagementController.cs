using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NEXUS.Api.ProgramManagement;

[ApiController]
[Authorize(Policy = "Program.View")]
[Route("api/program-management")]
public sealed class ProgramManagementController : ControllerBase
{
    private readonly IProgramManagementService _service;

    public ProgramManagementController(IProgramManagementService service) => _service = service;

    [HttpGet("programs")]
    public async Task<ActionResult> GetPrograms([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null, CancellationToken cancellationToken = default)
        => Ok(await _service.GetProgramsAsync(page, pageSize, search, cancellationToken));

    [HttpGet("programs/{programId:guid}/overview")]
    public async Task<ActionResult> GetOverview(Guid programId, [FromQuery] int projectsPage = 1, [FromQuery] int projectsPageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetOverviewAsync(programId, projectsPage, projectsPageSize, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }
}
