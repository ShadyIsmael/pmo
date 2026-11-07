namespace NEXUS.Modules.Organization.Domain;

/// <summary>A customer environment in the FOPX PM platform.</summary>
public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TenantLifecycleStatus Status { get; set; } = TenantLifecycleStatus.Provisioning;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<Organization> Organizations { get; set; } = new List<Organization>();
}

public enum TenantLifecycleStatus
{
    Provisioning = 0,
    Active = 1,
    Suspended = 2,
    Retired = 3
}
