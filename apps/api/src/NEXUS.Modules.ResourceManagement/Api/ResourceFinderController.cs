using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;

namespace NEXUS.Modules.ResourceManagement.Api;

[Authorize]
[ApiController]
[Route("api/resource-finder")]
public class ResourceFinderController : ControllerBase
{
    private readonly IResourceFinderService _resourceFinderService;

    public ResourceFinderController(IResourceFinderService resourceFinderService)
    {
        _resourceFinderService = resourceFinderService;
    }

    [HttpPost("find")]
    [Authorize(Policy = "Resource.View")]
    public async Task<IActionResult> Find([FromBody] ResourceFinderQueryDto query, CancellationToken cancellationToken)
    {
        var result = await _resourceFinderService.FindAsync(query, cancellationToken);
        return Ok(result);
    }
}
