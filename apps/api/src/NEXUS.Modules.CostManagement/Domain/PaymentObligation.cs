using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public class PaymentObligation : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid? MilestoneId { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string? Notes { get; set; }
    public ICollection<PaymentSettlement> Settlements { get; set; } = new List<PaymentSettlement>();

    public decimal SettledAmount => Settlements.Where(item => !item.IsDeleted).Sum(item => item.Amount);
    public decimal OutstandingAmount => Math.Max(0m, Amount - SettledAmount);
    public string SettlementProgressStatus => SettledAmount switch
    {
        <= 0m => "Not Paid",
        _ when OutstandingAmount <= 0m => "Fully Paid",
        _ => "Partially Paid",
    };

    public bool IsDue(DateTime asOfUtc)
    {
        var asOfDate = asOfUtc.Date;
        return OutstandingAmount > 0m && DueDate.Date <= asOfDate;
    }

    public bool IsOverdue(DateTime asOfUtc)
    {
        var asOfDate = asOfUtc.Date;
        return OutstandingAmount > 0m && DueDate.Date < asOfDate;
    }

    public string GetDueDateStatus(DateTime asOfUtc)
    {
        if (OutstandingAmount <= 0m) return "Settled";
        if (IsOverdue(asOfUtc)) return "Overdue";
        if (DueDate.Date == asOfUtc.Date) return "Due";
        return "Upcoming";
    }

    public string GetLifecycleStatus(DateTime asOfUtc)
    {
        if (OutstandingAmount <= 0m) return "Fully Paid";
        if (IsOverdue(asOfUtc))
            return SettledAmount > 0m ? "Partially Paid and Overdue" : "Overdue";
        if (DueDate.Date == asOfUtc.Date)
            return SettledAmount > 0m ? "Partially Paid and Due" : "Due";
        if (SettledAmount > 0m) return "Partially Paid";
        return "Upcoming";
    }
}
