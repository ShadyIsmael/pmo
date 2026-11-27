using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

/// <summary>Lifecycle states for the target Planned Payment aggregate.</summary>
public static class PlannedPaymentStates
{
    public const string Registered = "Registered";
    public const string Locked = "Locked";
    public const string Replaced = "Replaced";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Registered, Locked, Replaced, Cancelled];
}

/// <summary>Payment status values derived from confirmed settlement records.</summary>
public static class PlannedPaymentPaymentStatuses
{
    public const string NotPaid = "Not Paid";
    public const string PartiallyPaid = "Partially Paid";
    public const string FullyPaid = "Fully Paid";

    public static readonly string[] All = [NotPaid, PartiallyPaid, FullyPaid];
}

/// <summary>
/// Project-owned financial commitment. Registration reserves the full amount
/// against the project's current approved lifetime ceiling immediately.
/// </summary>
public sealed class PlannedPayment : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string State { get; set; } = PlannedPaymentStates.Registered;

    /// <summary>Stable client key used to make registration replay-safe.</summary>
    public Guid? IdempotencyKey { get; set; }

    /// <summary>Canonical hash of the registered command payload.</summary>
    public string? RegistrationFingerprint { get; set; }

    /// <summary>Stable key and fingerprint of the latest pre-lock amendment.</summary>
    public Guid? UpdateIdempotencyKey { get; set; }
    public string? UpdateFingerprint { get; set; }

    public Guid? RegisteredBy { get; set; }
    public DateTime? RegisteredAtUtc { get; set; }
    public DateTime? FirstRequestAtUtc { get; set; }
    public Guid? ReplacesPlannedPaymentId { get; set; }
    public Guid? ReplacedByPlannedPaymentId { get; set; }

    public ICollection<PlannedPaymentInstallment> Installments { get; set; } = new List<PlannedPaymentInstallment>();
    public PlannedPaymentLifetimeReservation? Reservation { get; set; }

    public bool IsLocked => string.Equals(State, PlannedPaymentStates.Locked, StringComparison.Ordinal);

    /// <summary>Payment status derived from confirmed settlement records across all installments.</summary>
    public string PaymentStatus
    {
        get
        {
            var totalSettled = Installments.Sum(item => item.ActualPaid);
            if (totalSettled <= 0m) return PlannedPaymentPaymentStatuses.NotPaid;
            var totalAmount = Installments.Sum(item => item.Amount);
            if (totalSettled >= totalAmount) return PlannedPaymentPaymentStatuses.FullyPaid;
            return PlannedPaymentPaymentStatuses.PartiallyPaid;
        }
    }
}
