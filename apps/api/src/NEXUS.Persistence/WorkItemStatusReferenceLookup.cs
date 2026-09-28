using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Persistence;

public sealed class WorkItemStatusReferenceLookup : IWorkItemStatusReferenceLookup
{
    private readonly DbContext _context;

    public WorkItemStatusReferenceLookup(DbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WorkItemStatusReference>> GetAsync(bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Lookup>()
            .Where(lookup => lookup.Category == ReferenceDataCategories.WorkItemStatus && !lookup.IsDeleted);
        if (!includeInactive) query = query.Where(lookup => lookup.IsActive);

        var statuses = await query
            .OrderBy(lookup => lookup.DisplayOrder)
            .ThenBy(lookup => lookup.Code)
            .ToListAsync(cancellationToken);
        return statuses.Select(ToReference).ToList();
    }

    public async Task<WorkItemStatusReference?> FindAsync(string code, CancellationToken cancellationToken = default)
    {
        var status = await _context.Set<Lookup>()
            .FirstOrDefaultAsync(lookup => lookup.Category == ReferenceDataCategories.WorkItemStatus && !lookup.IsDeleted && lookup.Code == code, cancellationToken);
        return status is null ? null : ToReference(status);
    }

    private static WorkItemStatusReference ToReference(Lookup lookup) => new(
        lookup.Id,
        lookup.Code,
        lookup.NameEn,
        lookup.NameAr,
        lookup.DisplayOrder,
        lookup.IsActive,
        lookup.Color,
        lookup.IsDefault);
}
