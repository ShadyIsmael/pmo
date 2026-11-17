using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Api;

[ApiController]
[Route("api/portfolios")]
[Authorize]
public class PortfolioController : ControllerBase
{
    private readonly IPortfolioService _portfolioService;

    public PortfolioController(IPortfolioService portfolioService)
    {
        _portfolioService = portfolioService;
    }

    [Authorize(Policy = "Portfolio.View")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _portfolioService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [Authorize(Policy = "Portfolio.View")]
    [OrganizationScopedCollectionPermission("Portfolio.View")]
    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? status = null, CancellationToken cancellationToken = default)
    {
        var result = await _portfolioService.GetPagedAsync(page, pageSize, search, status, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "WorkflowLookup.Project.Portfolios")]
    [HttpGet("project-lookup")]
    public async Task<IActionResult> GetProjectLookup([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? status = null, CancellationToken cancellationToken = default)
        => Ok(await _portfolioService.GetProjectLookupAsync(page, pageSize, search, status, cancellationToken));

    [Authorize(Policy = "Portfolio.Create")]
    [HttpPost]
    public async Task<IActionResult> Create(SavePortfolioDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _portfolioService.CreateAsync(dto, actorId, cancellationToken);
        return result.IsFailure
            ? Problem(result.Error?.Message, statusCode: result.Error?.StatusCode ?? 400)
            : CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Policy = "Portfolio.Edit")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SavePortfolioDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _portfolioService.UpdateAsync(id, dto, actorId, cancellationToken);
        return result.IsFailure
            ? Problem(result.Error?.Message, statusCode: result.Error?.StatusCode ?? 400)
            : Ok(result.Value);
    }

    [Authorize(Policy = "Portfolio.Archive")]
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _portfolioService.ArchiveAsync(id, actorId, cancellationToken);
        return result.IsFailure
            ? Problem(result.Error?.Message, statusCode: result.Error?.StatusCode ?? 400)
            : Ok();
    }

    [Authorize(Policy = "Portfolio.Reactivate")]
    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _portfolioService.ReactivateAsync(id, actorId, cancellationToken);
        return result.IsFailure
            ? Problem(result.Error?.Message, statusCode: result.Error?.StatusCode ?? 400)
            : Ok();
    }

    private bool TryGetActorId(out string actorId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (User.Identity?.IsAuthenticated == true
            && Guid.TryParse(claim, out var parsed)
            && parsed != Guid.Empty)
        {
            actorId = parsed.ToString();
            return true;
        }

        actorId = string.Empty;
        return false;
    }
}
