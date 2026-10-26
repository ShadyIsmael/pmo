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
    public string Currency { get; set; } = "GBP";
    public DateTime DueDate { get; set; }
    public string? Notes { get; set; }
    public ICollection<PaymentSettlement> Settlements { get; set; } = new List<PaymentSettlement>();

    public decimal SettledAmount => Settlements.Where(item => !item.IsDeleted).Sum(item => item.Amount);
    public decimal OutstandingAmount => Math.Max(0m, Amount - SettledAmount);

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

    public string GetLifecycleStatus(DateTime asOfUtc)
    {
        if (OutstandingAmount <= 0m) return "Fully Paid";
        if (IsOverdue(asOfUtc)) return "Partially Paid and Overdue";
        if (DueDate.Date == asOfUtc.Date) return "Due";
        if (SettledAmount > 0m) return "Partially Paid";
        return "Upcoming";
    }
}
