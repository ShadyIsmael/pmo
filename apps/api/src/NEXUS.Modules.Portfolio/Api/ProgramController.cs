using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.SharedKernel;

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
    [OrganizationScopedCollectionPermission("Program.View")]
    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] Guid? portfolioId = null, CancellationToken cancellationToken = default)
    {
        var result = await _programService.GetPagedAsync(page, pageSize, search, status, portfolioId, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "WorkflowLookup.Project.Programs")]
    [HttpGet("project-lookup")]
    public async Task<IActionResult> GetProjectLookup([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] Guid? portfolioId = null, CancellationToken cancellationToken = default)
        => Ok(await _programService.GetProjectLookupAsync(page, pageSize, search, status, portfolioId, cancellationToken));

    [Authorize(Policy = "WorkflowLookup.ProgramCreate.Portfolios")]
    [HttpGet("eligible-portfolios")]
    public async Task<IActionResult> GetEligiblePortfolios(CancellationToken cancellationToken)
    {
        var result = await _programService.GetEligiblePortfoliosForCreationAsync(cancellationToken);
        if (result.IsFailure) return Problem(result.Error?.Message, statusCode: result.Error?.StatusCode ?? 400);
        return Ok(new { items = result.Value });
    }

    /// <summary>Returns scoped Portfolio options for editing Programs.</summary>
    [Authorize(Policy = "WorkflowLookup.ProgramEdit.Portfolios")]
    [HttpGet("editable-portfolios")]
    public async Task<IActionResult> GetEditablePortfolios(CancellationToken cancellationToken)
    {
        var result = await _programService.GetEligiblePortfoliosForEditingAsync(cancellationToken);
        if (result.IsFailure) return Problem(result.Error?.Message, statusCode: result.Error?.StatusCode ?? 400);
        return Ok(new { items = result.Value });
    }

    [Authorize(Policy = "Program.Create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveProgramDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _programService.CreateAsync(dto, actorId, cancellationToken);
        if (result.IsFailure) return Problem(result.Error?.Message, statusCode: result.Error?.StatusCode ?? 400);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Policy = "Program.Edit")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveProgramDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _programService.UpdateAsync(id, dto, actorId, cancellationToken);
        if (result.IsFailure) return Problem(result.Error?.Message, statusCode: result.Error?.StatusCode ?? 400);
        return Ok(result.Value);
    }

    [Authorize(Policy = "Program.Archive")]
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _programService.ArchiveAsync(id, actorId, cancellationToken);
        if (result.IsFailure) return Problem(result.Error?.Message, statusCode: result.Error?.StatusCode ?? 400);
        return Ok();
    }

    [Authorize(Policy = "Program.Reactivate")]
    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _programService.ReactivateAsync(id, actorId, cancellationToken);
        if (result.IsFailure) return Problem(result.Error?.Message, statusCode: result.Error?.StatusCode ?? 400);
        return Ok();
    }

    private bool TryGetActorId(out string actorId)
    {
        if (User.Identity?.IsAuthenticated == true
            && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
            && parsed != Guid.Empty)
        {
            actorId = parsed.ToString();
            return true;
        }

        actorId = string.Empty;
        return false;
    }
}
