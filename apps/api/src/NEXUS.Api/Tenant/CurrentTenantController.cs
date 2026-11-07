using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Persistence;
using TenantEntity = NEXUS.Modules.Organization.Domain.Tenant;

namespace NEXUS.Api.Tenant;

[ApiController]
[Route("api/tenant")]
[Authorize]
public sealed class CurrentTenantController : ControllerBase
{
    private readonly AppDbContext _db;

    public CurrentTenantController(AppDbContext db) => _db = db;

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("tenantId")?.Value, out var tenantId) || tenantId == Guid.Empty)
            return Unauthorized();

        var tenant = await _db.Set<TenantEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == tenantId, cancellationToken);

        if (tenant is null)
            return NotFound();

        return Ok(new { id = tenant.Id, code = tenant.Code, displayName = tenant.DisplayName });
    }
}
