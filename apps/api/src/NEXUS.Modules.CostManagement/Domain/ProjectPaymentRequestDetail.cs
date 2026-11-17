using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

/// <summary>
/// Project finance extension for the shared ApprovalRequest root. Amount,
/// currency, requester, reference, and lifecycle status remain authoritative on
/// ApprovalRequest; this row stores only financial workflow-specific data.
/// </summary>
public sealed class ProjectPaymentRequestDetail : ITenantEntity
{
    /// <summary>Shared primary key and foreign key to ApprovalRequest.Id.</summary>
    public Guid ApprovalRequestId { get; set; }

    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? PlanActivityId { get; set; }

    /// <summary>
    /// Immutable annual budget association for requests created after annual budget
    /// integration. Null is retained only for pre-integration historical requests.
    /// </summary>
    public Guid? ProjectAnnualBudgetId { get; set; }

    /// <summary>Fiscal-year identity captured with the immutable annual budget association.</summary>
    public int? AnnualBudgetFiscalYear { get; set; }

    /// <summary>Financial reference snapshot for audit and historical request display.</summary>
    public string? AnnualBudgetReference { get; set; }

    /// <summary>Null while draft; frozen to Sequential or Parallel on submission.</summary>
    public string? ApprovalMode { get; set; }

    public string RequestKind { get; set; } = ProjectPaymentRequestKinds.PaymentRequest;
    public Guid? ReversesApprovalRequestId { get; set; }
    public ProjectPaymentRequestDetail? ReversesRequest { get; set; }

    /// <summary>Current funding source for an approved reversal; null means original Annual Budget.</summary>
    public Guid? FundingTransferId { get; set; }

    /// <summary>Project-scoped idempotency key for create/submit retries.</summary>
    public Guid? IdempotencyKey { get; set; }

    /// <summary>Canonical request-content fingerprint paired with the idempotency key.</summary>
    public string? RequestFingerprint { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<PaymentSettlement> Settlements { get; set; } = new List<PaymentSettlement>();
}

public static class ProjectPaymentRequestKinds
{
    public const string PaymentRequest = "PaymentRequest";
    public const string ReversalRequest = "ReversalRequest";
}

public static class ProjectPaymentRequestApprovalModes
{
    public const string Sequential = "Sequential";
    public const string Parallel = "Parallel";
}

public static class PaymentRequestAssignmentRequirements
{
    public const string Mandatory = "Mandatory";
    public const string Optional = "Optional";
}

public static class PaymentRequestAssignmentStates
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}
