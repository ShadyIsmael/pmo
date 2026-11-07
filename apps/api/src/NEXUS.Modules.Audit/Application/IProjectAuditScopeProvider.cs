namespace NEXUS.Modules.Audit.Application;

/// <summary>
/// Resolves the identifiers of every entity that currently belongs to a project,
/// so project-scoped audit queries can include events for project-linked records
/// even when the audit row itself predates the ProjectId column.
/// </summary>
public interface IProjectAuditScopeProvider
{
    Task<ProjectAuditScope?> ResolveAsync(Guid projectId, Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record ProjectAuditScope(
    Guid ProjectId,
    Guid ProjectPublicId,
    string ProjectName,
    IReadOnlyDictionary<string, IReadOnlyCollection<Guid>> EntityIdsByType)
{
    public bool IsEmpty => EntityIdsByType.Count == 0;
}
