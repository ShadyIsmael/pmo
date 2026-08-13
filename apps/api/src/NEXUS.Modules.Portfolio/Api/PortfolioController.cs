using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Portfolio.Application;

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

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _portfolioService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search=null, [FromQuery] string? status=null, CancellationToken cancellationToken = default)
    {
        var result = await _portfolioService.GetPagedAsync(page,pageSize,search,status,cancellationToken);
        return Ok(result);
    }
    [HttpPost] public async Task<IActionResult> Create(SavePortfolioDto dto,CancellationToken ct){var r=await _portfolioService.CreateAsync(dto,User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value??"System",ct);return r.IsFailure?Problem(r.Error?.Message,statusCode:r.Error?.StatusCode??400):CreatedAtAction(nameof(GetById),new{id=r.Value!.Id},r.Value);}
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id,SavePortfolioDto dto,CancellationToken ct){var r=await _portfolioService.UpdateAsync(id,dto,User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value??"System",ct);return r.IsFailure?Problem(r.Error?.Message,statusCode:r.Error?.StatusCode??400):Ok(r.Value);}
    [HttpPost("{id:guid}/archive")] public async Task<IActionResult> Archive(Guid id,CancellationToken ct){var r=await _portfolioService.ArchiveAsync(id,User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value??"System",ct);return r.IsFailure?Problem(r.Error?.Message,statusCode:r.Error?.StatusCode??400):Ok();}
    [HttpPost("{id:guid}/reactivate")] public async Task<IActionResult> Reactivate(Guid id,CancellationToken ct){var r=await _portfolioService.ReactivateAsync(id,User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value??"System",ct);return r.IsFailure?Problem(r.Error?.Message,statusCode:r.Error?.StatusCode??400):Ok();}
}
