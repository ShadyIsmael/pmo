namespace NEXUS.Modules.Organization.Domain;

/// <summary>
/// A durable platform, tenant or organization setting override. This entity intentionally does not
/// implement ITenantEntity: platform rows have no tenant, and every query must scope explicitly
/// by scope, tenant and owner in the settings repository.
/// </summary>
public sealed class ConfigurationSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Scope { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public Guid? OwnerId { get; set; }
    /// <summary>Organization owner FK, populated only for Organization scope.</summary>
    public Guid? OrganizationId { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public string ValueJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
