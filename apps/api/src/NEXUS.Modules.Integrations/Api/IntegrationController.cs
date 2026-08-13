using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Integrations.Application;

namespace NEXUS.Modules.Integrations.Api;

[ApiController]
[Route("api/integrations")]
[Authorize]
public class IntegrationController : ControllerBase
{
    private readonly IIntegrationService _integrationService;

    public IntegrationController(IIntegrationService integrationService)
    {
        _integrationService = integrationService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _integrationService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _integrationService.GetPagedAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }
}
