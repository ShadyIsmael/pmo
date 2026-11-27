namespace NEXUS.Modules.Documents.Application;

/// <summary>
/// Checks all current and historical records that prevent permanent category deletion.
/// Implementations must include soft-deleted rows and enforce the supplied tenant boundary.
/// </summary>
public interface IDocumentCategoryReferenceValidator
{
    Task<bool> HasReferencesAsync(
        Guid tenantId,
        Guid categoryId,
        string nameEn,
        string? nameAr,
        Guid? organizationId,
        CancellationToken cancellationToken = default);
}
