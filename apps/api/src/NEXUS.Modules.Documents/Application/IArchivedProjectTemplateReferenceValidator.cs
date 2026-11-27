namespace NEXUS.Modules.Documents.Application;

/// <summary>
/// Verifies whether an archived source template version is pinned by a persisted
/// Phase Gate requirement for the exact project and organization.
/// </summary>
public interface IArchivedProjectTemplateReferenceValidator
{
    Task<bool> IsExactPinnedReferenceAsync(
        Guid tenantId,
        Guid projectId,
        Guid organizationId,
        Guid templateId,
        Guid templateVersionId,
        CancellationToken cancellationToken = default);
}
