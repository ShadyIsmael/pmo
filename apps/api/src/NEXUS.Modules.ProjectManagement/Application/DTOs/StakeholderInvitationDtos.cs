namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public sealed class StakeholderInvitationDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectStakeholderId { get; init; }
    public Guid ResourceId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public DateTime? AcceptedAt { get; init; }
    public DateTime? RevokedAt { get; init; }
    /// <summary>Only populated in Development when email delivery is unavailable.</summary>
    public string? InvitationUrl { get; init; }
    public bool EmailDeliveryDeferred { get; init; }
}

public sealed class StakeholderInvitationValidationDto
{
    public Guid ProjectId { get; init; }
    public Guid ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string RelationshipType { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

public sealed class AcceptStakeholderInvitationDto
{
    public string Password { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed class StakeholderInvitationAcceptanceDto
{
    public Guid UserId { get; init; }
    public Guid ResourceId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
