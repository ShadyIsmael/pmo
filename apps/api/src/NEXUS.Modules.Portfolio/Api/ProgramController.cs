using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Portfolio.Application;

namespace NEXUS.Modules.Portfolio.Api;

[ApiController]
[Route("api/programs")]
[Authorize]
public class ProgramController : ControllerBase
{
    private readonly IProgramService _programService;

    public ProgramController(IProgramService programService)
    {
        _programService = programService;
    }

    [Authorize(Policy = "Program.View")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _programService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [Authorize(Policy = "Program.View")]
    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] Guid? portfolioId = null, CancellationToken cancellationToken = default)
    {
        var result = await _programService.GetPagedAsync(page, pageSize, search, status, portfolioId, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "Program.Manage")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveProgramDto dto, CancellationToken cancellationToken)
    {
        var result = await _programService.CreateAsync(dto, UserId(), cancellationToken);
        if (result.IsFailure) return Problem(result.Error?.Message, statusCode: result.Error?.StatusCode ?? 400);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Policy = "Program.Manage")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveProgramDto dto, CancellationToken cancellationToken)
    {
        var result = await _programService.UpdateAsync(id, dto, UserId(), cancellationToken);
        if (result.IsFailure) return Problem(result.Error?.Message, statusCode: result.Error?.StatusCode ?? 400);
        return Ok(result.Value);
    }

    [Authorize(Policy = "Program.Manage")]
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _programService.ArchiveAsync(id, UserId(), cancellationToken);
        if (result.IsFailure) return Problem(result.Error?.Message, statusCode: result.Error?.StatusCode ?? 400);
        return Ok();
    }

    [Authorize(Policy = "Program.Manage")]
    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _programService.ReactivateAsync(id, UserId(), cancellationToken);
        if (result.IsFailure) return Problem(result.Error?.Message, statusCode: result.Error?.StatusCode ?? 400);
        return Ok();
    }

    private string UserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
}
