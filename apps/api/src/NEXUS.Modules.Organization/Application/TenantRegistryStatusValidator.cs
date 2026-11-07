using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Application;

public sealed class TenantRegistryStatusValidator : ITenantRegistryStatusValidator
{
    private readonly DbContext _context;

    public TenantRegistryStatusValidator(DbContext context) => _context = context;

    public Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return Task.FromResult(false);
        return _context.Set<Tenant>().AsNoTracking()
            .AnyAsync(tenant => tenant.Id == tenantId && tenant.Status == TenantLifecycleStatus.Active, cancellationToken);
    }
}
