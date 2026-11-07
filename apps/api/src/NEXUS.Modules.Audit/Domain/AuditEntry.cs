using NEXUS.SharedKernel;

namespace NEXUS.Modules.Audit.Domain;

/// <summary>
/// Immutable record of a successful business operation. Entries are written by
/// the owning module inside (or immediately after) its own unit of work and are
/// never updated or deleted through application APIs.
/// </summary>
public class AuditEntry : BaseEntity
{
    /// <summary>
    /// Project the event belongs to. Populated directly by project-scoped
    /// producers and backfilled for project records; project-linked child
    /// entities are additionally resolved at read time so historical rows stay
    /// visible without fabricating data.
    /// </summary>
    public Guid? ProjectId { get; set; }

    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;

    /// <summary>JSON object describing changed fields as { field: { from, to } }.</summary>
    public string? Changes { get; set; }

    /// <summary>JSON object or scalar describing the entity before the change.</summary>
    public string? OriginalValues { get; set; }

    /// <summary>JSON object or scalar describing the entity after the change.</summary>
    public string? NewValues { get; set; }

    /// <summary>Comma-separated list of the fields that actually changed.</summary>
    public string? ChangedFields { get; set; }

    /// <summary>Optional human-readable business description of the event.</summary>
    public string? Description { get; set; }

    /// <summary>User, System, Integration or BackgroundJob.</summary>
    public string? Source { get; set; }

    /// <summary>Request or trace correlation identifier when one is available.</summary>
    public string? CorrelationId { get; set; }

    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ICollection<AuditActivity> Activities { get; set; } = new List<AuditActivity>();
}
