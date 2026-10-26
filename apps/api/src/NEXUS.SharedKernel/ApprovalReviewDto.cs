namespace NEXUS.SharedKernel;

/// <summary>
/// Read-only, assignment-scoped projection used to review one actionable approval.
/// It deliberately omits internal owning-object identifiers and unrelated project data.
/// </summary>
public sealed class ApprovalReviewDto
{
    public Guid ApprovalId { get; init; }
    public string ApprovalType { get; init; } = string.Empty;
    public string BusinessObjectType { get; init; } = string.Empty;
    public Guid? BusinessObjectPublicId { get; init; }
    public string BusinessObjectName { get; init; } = string.Empty;
    public string? BusinessObjectCode { get; init; }
    public string? BusinessObjectStatus { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public string RequestedByName { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? EffectiveDate { get; init; }
    public string? CurrentAssigneeName { get; init; }
    public string? ProposedAssigneeName { get; init; }
    public int? ChangeNumber { get; init; }
    public string? ChangeType { get; init; }
    public string? Justification { get; init; }
    public string? Impact { get; init; }
    public decimal? CurrentAmount { get; init; }
    public decimal? AmountChange { get; init; }
    public decimal? ProposedAmount { get; init; }
    public string? Currency { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public bool CanDecide { get; init; }
}
