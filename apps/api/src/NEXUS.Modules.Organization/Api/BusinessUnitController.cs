using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Organization.Application;

namespace NEXUS.Modules.Organization.Api;

[ApiController]
[Route("api/business-units")]
[Authorize]
public class BusinessUnitController : ControllerBase
{
    private readonly IBusinessUnitService _businessUnitService;

    public BusinessUnitController(IBusinessUnitService businessUnitService)
    {
        _businessUnitService = businessUnitService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _businessUnitService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetByOrganization([FromQuery] Guid organizationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _businessUnitService.GetByOrganizationAsync(organizationId, page, pageSize, cancellationToken);
        return Ok(result);
    }
}
