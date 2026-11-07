using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Integrations.Application;

namespace NEXUS.Modules.Integrations.Api;

[ApiController]
[Route("api/integrations")]
[Authorize(Policy = "Settings.View")]
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
        var tenantId = TenantId();
        var result = await _integrationService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId();
        var result = await _integrationService.GetPagedAsync(tenantId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    private Guid TenantId() => Guid.TryParse(User.FindFirstValue("tenantId"), out var tenantId)
        ? tenantId
        : throw new UnauthorizedAccessException("The authenticated tenant is unavailable.");
}
