using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public sealed class StakeholderInvitation : BaseEntity
{
    public Guid ResourceId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProjectStakeholderId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public StakeholderInvitationStatus Status { get; set; } = StakeholderInvitationStatus.Pending;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public Project Project { get; set; } = null!;
    public ProjectStakeholder ProjectStakeholder { get; set; } = null!;
}

public enum StakeholderInvitationStatus
{
    Pending,
    Accepted,
    Revoked,
    Expired
}
