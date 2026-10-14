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
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? entityType = null, [FromQuery] string? action = null, [FromQuery] Guid? entityId = null, [FromQuery] string? entityIds = null, [FromQuery] string? search = null, CancellationToken cancellationToken = default)
    {
        var result = await _auditService.GetPagedAsync(page, pageSize, entityType, action, entityId, ParseEntityIds(entityIds), search, cancellationToken);
        return Ok(result);
    }

    private static Guid[]? ParseEntityIds(string? entityIds)
    {
        if (string.IsNullOrWhiteSpace(entityIds)) return null;
        var parsed = entityIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();
        return parsed.Length == 0 ? null : parsed;
    }
}
