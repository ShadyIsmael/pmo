using System.ComponentModel.DataAnnotations;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Documents.Domain;

/// <summary>
/// Durable approval record for deleting an approved Document Center template.
/// It deliberately has no foreign key to the template or its users so history
/// survives a hard delete and account removal.
/// </summary>
public sealed class TemplateDeletionRequest : BaseEntity
{
    public const string StatusPendingApproval = "PendingApproval";
    public const string StatusApproved = "Approved";
    public const string StatusRejected = "Rejected";
    public const string StatusStale = "Stale";

    public const string OwnerScopeOrganization = "Organization";
    public const string OwnerScopeTenant = "Tenant";

    public const string DeletionTypeSoftDelete = "SoftDelete";
    public const string DeletionTypeHardDelete = "HardDelete";

    public const string CleanupStatusNone = "None";
    public const string CleanupStatusPending = "Pending";
    public const string CleanupStatusCompleted = "Completed";

    /// <summary>Stable identity snapshot; intentionally not an FK to Documents.</summary>
    public Guid TemplateId { get; set; }

    [MaxLength(256)]
    public string TemplateName { get; set; } = string.Empty;

    public Guid? OwnerOrganizationId { get; set; }

    [MaxLength(24)]
    public string OwnerScope { get; set; } = OwnerScopeOrganization;

    public Guid RequesterId { get; set; }

    [MaxLength(256)]
    public string RequesterName { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Status { get; set; } = StatusPendingApproval;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public Guid? ApproverId { get; set; }

    [MaxLength(256)]
    public string? ApproverName { get; set; }

    public DateTime? DecidedAt { get; set; }

    [MaxLength(4000)]
    public string? DecisionComments { get; set; }

    [MaxLength(24)]
    public string? DeletionType { get; set; }

    /// <summary>Hash of the template and all version metadata at request time.</summary>
    [MaxLength(64)]
    public string TemplateMutationSnapshotHash { get; set; } = string.Empty;

    /// <summary>
    /// Opaque provider/key pairs queued for post-commit cleanup. Keeping this on
    /// the durable request makes cleanup recoverable after process interruption.
    /// </summary>
    public string? StorageCleanupKeysJson { get; set; }

    [MaxLength(24)]
    public string StorageCleanupStatus { get; set; } = CleanupStatusNone;

    public DateTime? StorageCleanupLastAttemptAt { get; set; }

    [MaxLength(1000)]
    public string? StorageCleanupError { get; set; }
}
