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
    public DateTime? DeclinedAt { get; init; }
    public DateTime? RevokedAt { get; init; }
    public bool ReportsConsent { get; init; }
    public bool NotificationsConsent { get; init; }
    public string EmailDeliveryStatus { get; init; } = string.Empty;
    public DateTime? EmailSentAt { get; init; }
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
    public string Status { get; init; } = string.Empty;
    public bool ReportsConsent { get; init; }
    public bool NotificationsConsent { get; init; }
}

public sealed class AcceptStakeholderInvitationDto
{
    public string Token { get; init; } = string.Empty;
    public bool AcceptParticipation { get; init; }
    public bool ReceiveProjectReports { get; init; }
    public bool ReceiveProjectNotifications { get; init; }
}

public sealed class StakeholderInvitationTokenDto
{
    public string Token { get; init; } = string.Empty;
}

public sealed class WithdrawStakeholderConsentDto
{
    public string Token { get; init; } = string.Empty;
    public bool WithdrawReportsConsent { get; init; }
    public bool WithdrawNotificationsConsent { get; init; }
}

public sealed class StakeholderInvitationAcceptanceDto
{
    public Guid ResourceId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool ReportsConsent { get; init; }
    public bool NotificationsConsent { get; init; }
}
