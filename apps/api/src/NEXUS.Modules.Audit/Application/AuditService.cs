using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Audit.Application;

public class AuditService : IAuditService
{
    private readonly DbContext _context;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public AuditService(DbContext context, IHttpContextAccessor? httpContextAccessor = null)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PagedResult<AuditEntry>> GetPagedAsync(int page, int pageSize, string? entityType = null, string? action = null, Guid? entityId = null, IReadOnlyCollection<Guid>? entityIds = null, string? search = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _context.Set<AuditEntry>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);
        if (entityId.HasValue) query = query.Where(a => a.EntityId == entityId.Value);
        if (entityIds is { Count: > 0 }) query = query.Where(a => entityIds.Contains(a.EntityId));
        var searchTerm = search?.Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(a =>
                (a.UserName ?? string.Empty).Contains(searchTerm) ||
                a.Action.Contains(searchTerm) ||
                a.EntityType.Contains(searchTerm) ||
                (a.Changes ?? string.Empty).Contains(searchTerm));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<AuditEntry>(items, page, pageSize, total);
    }

    /// <summary>
    /// Reads the audit trail for one project. Scope is derived from the project's
    /// current entity graph (bounded lookups) plus any row that already carries the
    /// project id, so historical events for linked records remain visible without
    /// inventing project attribution.
    /// </summary>
    public async Task<AuditTrailPage> GetProjectTrailAsync(
        ProjectAuditScope scope,
        Guid tenantId,
        AuditTrailQuery request,
        AuditTrailAccess access,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = ApplyScope(_context.Set<AuditEntry>().IgnoreQueryFilters().AsNoTracking(), scope, tenantId);

        if (request.FromUtc.HasValue) query = query.Where(a => a.Timestamp >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) query = query.Where(a => a.Timestamp <= request.ToUtc.Value);
        if (!string.IsNullOrWhiteSpace(request.Action)) query = query.Where(a => a.Action == request.Action);
        if (request.ActorId.HasValue) query = query.Where(a => a.UserId == request.ActorId.Value);
        if (!string.IsNullOrWhiteSpace(request.EntityType)) query = query.Where(a => a.EntityType == request.EntityType);

        if (!string.IsNullOrWhiteSpace(request.Module))
        {
            var entityTypes = AuditModuleMap.EntityTypesFor(request.Module.Trim());
            if (entityTypes.Count == 0)
            {
                return new AuditTrailPage([], page, pageSize, 0, false);
            }
            query = query.Where(a => entityTypes.Contains(a.EntityType));
        }

        var searchTerm = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(a =>
                (a.UserName ?? string.Empty).Contains(searchTerm) ||
                a.Action.Contains(searchTerm) ||
                a.EntityType.Contains(searchTerm) ||
                (a.Description ?? string.Empty).Contains(searchTerm) ||
                (a.Changes ?? string.Empty).Contains(searchTerm));
        }

        var total = await query.CountAsync(cancellationToken);
        var ordered = request.NewestFirst
            ? query.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
            : query.OrderBy(a => a.Timestamp).ThenBy(a => a.Id);

        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = rows.Select(row => Map(row, access)).ToList();
        return new AuditTrailPage(items, page, pageSize, total, page * pageSize < total);
    }

    public Task<AuditTrailFacets> GetProjectFacetsAsync(
        ProjectAuditScope scope,
        Guid tenantId,
        CancellationToken cancellationToken = default) => GetProjectFacetsCoreAsync(scope, tenantId, null, cancellationToken);

    public async Task<AuditTrailFacets> GetProjectFacetsAsync(
        ProjectAuditScope scope,
        Guid tenantId,
        string module,
        CancellationToken cancellationToken = default) => await GetProjectFacetsCoreAsync(scope, tenantId, module, cancellationToken);

    private async Task<AuditTrailFacets> GetProjectFacetsCoreAsync(
        ProjectAuditScope scope,
        Guid tenantId,
        string? module,
        CancellationToken cancellationToken)
    {
        var query = ApplyScope(_context.Set<AuditEntry>().IgnoreQueryFilters().AsNoTracking(), scope, tenantId);
        if (!string.IsNullOrWhiteSpace(module))
        {
            var eventTypesForModule = AuditModuleMap.EntityTypesFor(module.Trim());
            if (eventTypesForModule.Count == 0) return new AuditTrailFacets([], [], [], [], null);
            query = query.Where(entry => eventTypesForModule.Contains(entry.EntityType));
        }

        var entityTypes = await query.Select(a => a.EntityType).Distinct().ToListAsync(cancellationToken);
        var actions = await query.Select(a => a.Action).Distinct().OrderBy(value => value).ToListAsync(cancellationToken);
        var actorRows = await query
            .Where(a => a.UserId.HasValue && a.UserName != null && a.UserName != string.Empty)
            .GroupBy(a => new { a.UserId, a.UserName })
            .Select(group => new { Id = group.Key.UserId, Name = group.Key.UserName })
            .ToListAsync(cancellationToken);
        var actors = actorRows
            .Where(row => row.Id.HasValue && !string.IsNullOrWhiteSpace(row.Name))
            .Select(row => new AuditActorOption(row.Id!.Value, row.Name!))
            .OrderBy(actor => actor.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var coverageStart = await query
            .OrderBy(a => a.Timestamp)
            .Select(a => (DateTime?)a.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        var modules = entityTypes.Select(AuditModuleMap.Resolve).Distinct().OrderBy(value => value).ToList();
        return new AuditTrailFacets(modules, actions, actors, entityTypes, coverageStart);
    }

    private static IQueryable<AuditEntry> ApplyScope(IQueryable<AuditEntry> query, ProjectAuditScope scope, Guid tenantId)
    {
        // Tenant isolation is explicit here because the query intentionally bypasses
        // the global filters to include legacy rows that were written without a
        // tenant id but are provably owned by an entity inside this project scope.
        query = query.Where(a => !a.IsDeleted && (a.TenantId == tenantId || a.TenantId == Guid.Empty));

        var parameter = Expression.Parameter(typeof(AuditEntry), "entry");
        // The explicit type matters: Expression.Constant(object) infers the type from the
        // boxed value, so a non-null Guid? collapses to Guid and Equal() then fails between
        // Guid? and Guid.
        Expression body = Expression.Equal(
            Expression.Property(parameter, nameof(AuditEntry.ProjectId)),
            Expression.Constant(scope.ProjectId, typeof(Guid?)));

        foreach (var pair in scope.EntityIdsByType)
        {
            if (pair.Value.Count == 0) continue;

            // The id list is reached through a property access on a constant holder so
            // EF Core extracts it as a query parameter. A bare constant would be inlined
            // as a multi-thousand element IN list, which bloats the batch and defeats
            // plan reuse.
            var ids = Expression.Property(
                Expression.Constant(new AuditIdSet(pair.Value)),
                nameof(AuditIdSet.Ids));
            var contains = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Contains),
                [typeof(Guid)],
                ids,
                Expression.Property(parameter, nameof(AuditEntry.EntityId)));
            var typeMatches = Expression.Equal(
                Expression.Property(parameter, nameof(AuditEntry.EntityType)),
                Expression.Constant(pair.Key));
            body = Expression.OrElse(body, Expression.AndAlso(typeMatches, contains));
        }

        return query.Where(Expression.Lambda<Func<AuditEntry, bool>>(body, parameter));
    }

    /// <summary>Holder that keeps scope id lists parameterisable inside a built expression.</summary>
    private sealed class AuditIdSet
    {
        public AuditIdSet(IReadOnlyCollection<Guid> ids) => Ids = ids.ToArray();

        public Guid[] Ids { get; }
    }

    private static AuditTrailEvent Map(AuditEntry row, AuditTrailAccess access)
    {
        var module = AuditModuleMap.Resolve(row.EntityType);

        // Sensitive module detail is withheld unless the caller has the exact
        // permission for this record type; the event metadata stays visible so the
        // trail remains complete without exposing the underlying changed values.
        if (!access.CanViewSensitiveDetailsFor(row.EntityType))
        {
            return new AuditTrailEvent(
                row.Id,
                row.ProjectId,
                row.EntityType,
                module,
                row.EntityId,
                row.Action,
                row.UserId,
                row.UserName,
                AuditSources.Normalize(row.Source),
                DateTime.SpecifyKind(row.Timestamp, DateTimeKind.Utc),
                row.Description,
                row.CorrelationId,
                ParseChangedFields(row.ChangedFields),
                new Dictionary<string, AuditFieldChange>(),
                null,
                null,
                true);
        }

        var includeFinancial = access.IncludeFinancialDetail;
        // Financial detail is masked per field, so non-monetary fields on the same event
        // stay readable. The response-level IncludeFinancialDetail flag tells the client
        // that masking happened; DetailsRestricted is reserved for withheld payloads.
        var changes = AuditFieldRedactor.Redact(row.Changes, includeFinancial);
        var before = AuditFieldRedactor.Redact(row.OriginalValues, includeFinancial);
        var after = AuditFieldRedactor.Redact(row.NewValues, includeFinancial);

        return new AuditTrailEvent(
            row.Id,
            row.ProjectId,
            row.EntityType,
            module,
            row.EntityId,
            row.Action,
            row.UserId,
            row.UserName,
            AuditSources.Normalize(row.Source),
            DateTime.SpecifyKind(row.Timestamp, DateTimeKind.Utc),
            row.Description,
            row.CorrelationId,
            ParseChangedFields(row.ChangedFields),
            ParseChanges(changes.Json),
            before.Json,
            after.Json,
            false);
    }

    private static IReadOnlyList<string> ParseChangedFields(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyDictionary<string, AuditFieldChange> ParseChanges(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, AuditFieldChange>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new Dictionary<string, AuditFieldChange>();

            var result = new Dictionary<string, AuditFieldChange>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object
                    && property.Value.TryGetProperty("from", out var from)
                    && property.Value.TryGetProperty("to", out var to))
                {
                    result[property.Name] = new AuditFieldChange(Format(from), Format(to));
                }
                else
                {
                    result[property.Name] = new AuditFieldChange(null, Format(property.Value));
                }
            }
            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, AuditFieldChange>();
        }
    }

    private static string? Format(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => element.GetString(),
        _ => element.GetRawText()
    };

    public async Task<Result> LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await AppendAsync(entry, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public Task<Result> AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        Enrich(entry);
        _context.Set<AuditEntry>().Add(entry);
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Fills the audit metadata that producers are not required to supply and
    /// removes credential material before the row is persisted.
    /// </summary>
    private void Enrich(AuditEntry entry)
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        if (entry.Timestamp == default) entry.Timestamp = DateTime.UtcNow;
        if (entry.TenantId == Guid.Empty)
        {
            var tenantClaim = httpContext?.User.FindFirst("tenantId")?.Value;
            if (Guid.TryParse(tenantClaim, out var tenantId)) entry.TenantId = tenantId;
        }

        entry.Source = AuditSources.IsKnown(entry.Source)
            ? entry.Source
            : entry.UserId.HasValue ? AuditSources.User : AuditSources.System;

        if (string.IsNullOrWhiteSpace(entry.CorrelationId))
        {
            var header = httpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault();
            entry.CorrelationId = !string.IsNullOrWhiteSpace(header)
                ? header.Trim()
                : httpContext?.TraceIdentifier ?? System.Diagnostics.Activity.Current?.Id;
        }

        if (string.IsNullOrWhiteSpace(entry.ChangedFields) && !string.IsNullOrWhiteSpace(entry.Changes))
        {
            entry.ChangedFields = ExtractChangedFields(entry.Changes);
        }

        // Audit key columns are length-bounded so they can be indexed. A free-text
        // value (display name, client-supplied correlation header) must never fail the
        // business transaction it is describing, so clamp here, once, centrally.
        entry.EntityType = Clamp(entry.EntityType, 128) ?? string.Empty;
        entry.Action = Clamp(entry.Action, 96) ?? string.Empty;
        entry.Source = Clamp(entry.Source, 32);
        entry.CorrelationId = Clamp(entry.CorrelationId, 128);
        entry.Description = Clamp(entry.Description, 500);
        entry.UserName = Clamp(entry.UserName, 256);
        entry.IpAddress = Clamp(entry.IpAddress, 64);
        entry.UserAgent = Clamp(entry.UserAgent, 512);
        entry.ChangedFields = ClampFieldList(entry.ChangedFields, 1000);

        entry.Changes = AuditFieldRedactor.Sanitize(entry.Changes);
        entry.OriginalValues = AuditFieldRedactor.Sanitize(entry.OriginalValues);
        entry.NewValues = AuditFieldRedactor.Sanitize(entry.NewValues);
    }

    private static string? Clamp(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>Trims a comma-separated field list on a field boundary, never mid-name.</summary>
    private static string? ClampFieldList(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
        var cut = value.LastIndexOf(',', maxLength - 1);
        return cut <= 0 ? value[..maxLength] : value[..cut];
    }

    private static string? ExtractChangedFields(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            var keys = document.RootElement.EnumerateObject().Select(property => property.Name).ToList();
            return keys.Count == 0 ? null : string.Join(',', keys);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
