using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Audit.Application;

namespace NEXUS.Modules.Audit.Api;

[ApiController]
[Route("api/audit")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? entityType = null, [FromQuery] string? action = null, CancellationToken cancellationToken = default)
    {
        var result = await _auditService.GetPagedAsync(page, pageSize, entityType, action, cancellationToken);
        return Ok(result);
    }
}
