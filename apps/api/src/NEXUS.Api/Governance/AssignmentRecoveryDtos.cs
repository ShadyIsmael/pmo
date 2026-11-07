namespace NEXUS.Api.Governance;

public sealed class AssignmentRecoveryPageDto
{
    public IReadOnlyList<AssignmentRecoveryItemDto> Items { get; init; } = Array.Empty<AssignmentRecoveryItemDto>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public AssignmentRecoverySummaryDto Summary { get; init; } = new();
}

public sealed class InitialProjectManagerSummaryDto
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? SponsorName { get; init; }
}

public sealed class AssignmentRecoveryItemDto
{
    public string EntityType { get; init; } = string.Empty;
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public Guid? ManagerId { get; init; }
    public string? ManagerName { get; init; }
    public bool? ManagerIsActive { get; init; }
    public string ManagerAssignmentState { get; init; } = "Unassigned";
    public Guid? SponsorId { get; init; }
    public string? SponsorName { get; init; }
    public bool? SponsorIsActive { get; init; }
    public string SponsorState { get; init; } = "NotApplicable";
    public string? ParentProgramName { get; init; }
    public string? ParentPortfolioName { get; init; }
    public bool MissingParentHierarchy { get; init; }
    public string? AssignmentRequestStatus { get; init; }
    public string? AssignmentInitiationType { get; init; }
    public Guid? RequestId { get; init; }
    public bool CanViewExistingRequest { get; init; }
    public bool CanReviewExistingRequest { get; init; }
    public bool IsInitiatedByCurrentUser { get; init; }
    public bool IsAssignedToCurrentUser { get; init; }
    public Guid? ProposedManagerId { get; init; }
    public string? ProposedManagerName { get; init; }
    public string? InitiatedByName { get; init; }
    public DateTime? AssignmentRequestedAt { get; init; }
    public DateTime? AssignmentRespondedAt { get; init; }
    public string? RejectionReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AssignmentRecoverySummaryDto
{
    public int TotalProjects { get; init; }
    public int TotalPrograms { get; init; }
    public int TotalPortfolios { get; init; }
    public int UnassignedManagers { get; init; }
    public int ProjectsWithoutSponsor { get; init; }
    public int PendingAssignments { get; init; }
    public int RejectedAssignments { get; init; }
    public int InconsistentAssignments { get; init; }
}

public sealed class AssignmentRecoveryCandidateDto
{
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? JobTitle { get; init; }
    public IReadOnlyList<string> ManagerTypes { get; init; } = Array.Empty<string>();
}

public sealed class CreateRecoveryAssignmentDto
{
    public Guid ManagerId { get; init; }
    public DateTime EffectiveDate { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

public sealed class RecoveryAssignmentCreatedDto
{
    public Guid RequestId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityPublicId { get; init; }
    public string EntityName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string InitiationType { get; init; } = string.Empty;
    public string ProposedManagerName { get; init; } = string.Empty;
    public string InitiatedByName { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
}

public sealed class AssignProjectSponsorDto
{
    public Guid SponsorUserId { get; init; }
}

public sealed class AssignmentRecoveryHistoryDto
{
    public IReadOnlyList<AssignmentRecoveryHistoryItemDto> Items { get; init; } = Array.Empty<AssignmentRecoveryHistoryItemDto>();
}

public sealed class AssignmentRecoveryHistoryItemDto
{
    public string RecordType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? PreviousManagerName { get; init; }
    public string ManagerName { get; init; } = string.Empty;
    public string? InitiatedByName { get; init; }
    public DateTime? RequestedAt { get; init; }
    public DateTime EffectiveDate { get; init; }
    public DateTime? RespondedAt { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public string? RejectionReason { get; init; }
}

public sealed class AssignmentRecoveryReconciledDto
{
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityPublicId { get; init; }
    public string ManagerAssignmentState { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
