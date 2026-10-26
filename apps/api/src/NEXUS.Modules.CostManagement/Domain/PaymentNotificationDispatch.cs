using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public sealed class PaymentNotificationDispatch : BaseEntity
{
    public Guid PaymentObligationId { get; set; }
    public Guid UserId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public DateTime NotificationDate { get; set; }
    public Guid NotificationId { get; set; }
}
