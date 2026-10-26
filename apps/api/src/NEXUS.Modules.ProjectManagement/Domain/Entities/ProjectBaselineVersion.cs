using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain.Entities;

/// <summary>
/// A captured, versioned baseline of a project. Inherits <see cref="BaseEntity"/> so
/// tenant isolation (Guid TenantId), audit fields, soft delete, and RowVersion
/// concurrency are provided centrally and it participates automatically in the
/// global query filters.
/// </summary>
public class ProjectBaselineVersion : BaseEntity
{
    public ProjectId ProjectId { get; set; } = null!;
    public VersionNumber VersionNumber { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public Status Status { get; set; }
    public string? Description { get; set; }

    /// <summary>Approval provenance; distinct from the base creation audit.</summary>
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? SourceChangeRequestId { get; set; }
    public bool IsCurrent { get; set; }

    // Navigation property for immutable approved baseline metadata
    public virtual ProjectBaselineVersion? ApprovedBaselineMetadata { get; set; }
    public Guid? ApprovedBaselineMetadataId { get; set; }
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
    // Private parameterless constructor for EF
    private ProjectId() => Value = string.Empty;

    public string Value { get; }

    public ProjectId(string value)
    {
        Value = !string.IsNullOrEmpty(value) ? value : throw new ArgumentException("ProjectId cannot be null or empty.", nameof(value));
    }

    public override string ToString() => Value;
}

public class VersionNumber
{
    // Private parameterless constructor for EF
    private VersionNumber() { }

    public int Value { get; }

    public VersionNumber(int value)
    {
        if (value <= 0) throw new ArgumentException("Version number must be positive.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value.ToString();
}