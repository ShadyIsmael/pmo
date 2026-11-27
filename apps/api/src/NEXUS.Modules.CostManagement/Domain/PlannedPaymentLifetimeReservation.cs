using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

/// <summary>
/// One-time lifetime-budget reservation created when a Planned Payment is
/// registered. Settlements do not release or duplicate this reservation; only
/// an authorized cancellation or controlled replacement can release it.
/// </summary>
public sealed class PlannedPaymentLifetimeReservation : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid PlannedPaymentId { get; set; }

    /// <summary>Full amount reserved at registration.</summary>
    public decimal ReservedAmount { get; set; }

    /// <summary>Amount released by an authorized cancellation or replacement.</summary>
    public decimal ReleasedAmount { get; set; }

    public PlannedPayment? PlannedPayment { get; set; }

    public decimal OutstandingReservedAmount => Math.Max(0m, ReservedAmount - ReleasedAmount);
}
