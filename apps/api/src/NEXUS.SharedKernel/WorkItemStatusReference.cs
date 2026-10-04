namespace NEXUS.SharedKernel;

public static class ReferenceDataCategories
{
    public const string WorkItemStatus = "WorkItemStatus";
    public const string VendorCategory = "VendorCategory";
    public const string OrganizationalUnitType = "OrganizationalUnitType";
}

public sealed record WorkItemStatusReference(
    Guid Id,
    string Code,
    string NameEn,
    string NameAr,
    int DisplayOrder,
    bool IsActive,
    string? Color,
    bool IsDefault);

public interface IWorkItemStatusReferenceLookup
{
    Task<IReadOnlyList<WorkItemStatusReference>> GetAsync(bool includeInactive = true, CancellationToken cancellationToken = default);
    Task<WorkItemStatusReference?> FindAsync(string code, CancellationToken cancellationToken = default);
}

public interface IWorkItemStatusUsageLookup
{
    Task<bool> IsCodeUsedAsync(string code, CancellationToken cancellationToken = default);
}
