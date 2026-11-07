using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Integrations.Domain;
using NEXUS.Modules.Integrations.Infrastructure;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Integrations.Application;

public class IntegrationService : IIntegrationService
{
    private const int MaxPageSize = 100;

    private readonly DbContext _context;
    private readonly IIntegrationSecretProtector _secrets;

    public IntegrationService(DbContext context, IIntegrationSecretProtector secrets)
    {
        _context = context;
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
    }

    public async Task<Result<IntegrationConnectionDto>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var conn = await _context.Set<IntegrationConnection>()
            .Include(c => c.Syncs)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
        return conn is null
            ? Result<IntegrationConnectionDto>.Failure(Error.NotFound())
            : Result<IntegrationConnectionDto>.Success(Map(conn));
    }

    public async Task<PagedResult<IntegrationConnectionDto>> GetPagedAsync(Guid tenantId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _context.Set<IntegrationConnection>().Where(c => c.TenantId == tenantId && !c.IsDeleted);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Provider,
                c.BaseUrl,
                c.AuthType,
                c.IsActive,
                c.LastSyncAt,
                c.Status,
                c.ApiKey,
                c.ApiSecret
            })
            .ToListAsync(cancellationToken);

        // Secret presence is computed after materialization so ciphertext/legacy detection
        // never leaks secret values and is not translated to SQL.
        var items = rows.Select(c => new IntegrationConnectionDto
        {
            Id = c.Id,
            Name = c.Name,
            Provider = c.Provider,
            BaseUrl = c.BaseUrl,
            AuthType = c.AuthType,
            IsActive = c.IsActive,
            LastSyncAt = c.LastSyncAt,
            Status = c.Status,
            HasApiKey = HasConfiguredSecret(c.ApiKey),
            HasApiSecret = HasConfiguredSecret(c.ApiSecret)
        }).ToList();
        return new PagedResult<IntegrationConnectionDto>(items, page, pageSize, total);
    }

    private IntegrationConnectionDto Map(IntegrationConnection conn) => new()
    {
        Id = conn.Id,
        Name = conn.Name,
        Provider = conn.Provider,
        BaseUrl = conn.BaseUrl,
        AuthType = conn.AuthType,
        IsActive = conn.IsActive,
        LastSyncAt = conn.LastSyncAt,
        Status = conn.Status,
        HasApiKey = HasConfiguredSecret(conn.ApiKey),
        HasApiSecret = HasConfiguredSecret(conn.ApiSecret),
        Syncs = conn.Syncs
            .OrderByDescending(sync => sync.StartedAt)
            .Select(sync => new IntegrationSyncDto
            {
                Id = sync.Id,
                Direction = sync.Direction,
                EntityType = sync.EntityType,
                StartedAt = sync.StartedAt,
                CompletedAt = sync.CompletedAt,
                Status = sync.Status,
                RecordsProcessed = sync.RecordsProcessed,
                RecordsFailed = sync.RecordsFailed
            })
            .ToList()
    };

    private bool HasConfiguredSecret(string? storedValue)
    {
        if (string.IsNullOrEmpty(storedValue)) return false;
        return _secrets.TryUnprotect(storedValue, out var plaintext) && !string.IsNullOrEmpty(plaintext);
    }
}
