using System.ComponentModel.DataAnnotations;

namespace Pmo.Planing.Domain.Entities;

public class ProjectBaselineVersion
{
    public Guid Id { get; set; }
    public ProjectId ProjectId { get; set; } = null!;
    public VersionNumber VersionNumber { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public Status Status { get; set; }
    public string? Description { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? SourceChangeRequestId { get; set; }
    public bool IsCurrent { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation property for immutable approved baseline metadata
    public virtual ProjectBaselineVersion? ApprovedBaselineMetadata { get; set; }
    public Guid? ApprovedBaselineMetadataId { get; set; }

    // Tenant isolation property
    public string TenantId { get; set; } = string.Empty;
}

public enum Status
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    Superseded
}

public class ProjectId
{
    public string Value { get; }

    public ProjectId(string value)
    {
        Value = !string.IsNullOrEmpty(value) ? value : throw new ArgumentException("ProjectId cannot be null or empty.", nameof(value));
    }

    public override string ToString() => Value;
}

public class VersionNumber
{
    public int Value { get; }

    public VersionNumber(int value)
    {
        if (value <= 0) throw new ArgumentException("Version number must be positive.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value.ToString();
}