namespace NEXUS.SharedKernel;

/// <summary>
/// Database-owned counter used for tenant-scoped, server-generated entity codes.
/// The row is updated inside the same transaction as the entity that consumes the
/// value, so a rolled-back create does not advance the counter.
/// </summary>
public sealed class EntityCodeSequence : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int NextValue { get; set; }
}
