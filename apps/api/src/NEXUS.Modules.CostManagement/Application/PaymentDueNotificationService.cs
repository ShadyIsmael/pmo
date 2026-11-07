using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

public sealed class PaymentDueNotificationService(
    DbContext context,
    INotificationService notificationService,
    IUserEffectivePermissionService effectivePermissions,
    IProjectMilestoneReferenceLookup milestoneLookup,
    IProjectOrganizationLookup? projectOrganizationLookup = null)
{
    public async Task<int> SendDueNotificationsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var notificationDate = asOfUtc.Date;
        var obligations = await context.Set<PaymentObligation>()
            .IgnoreQueryFilters()
            .Include(item => item.Settlements)
            .Where(item => !item.IsDeleted)
            .ToListAsync(cancellationToken);

        var createdCount = 0;
        foreach (var obligation in obligations)
        {
            using var trustedTenant = TrustedTenantExecutionContext.Enter(obligation.TenantId);

            var outstanding = obligation.Amount - obligation.Settlements
                .Where(item => item.TenantId == obligation.TenantId && !item.IsDeleted)
                .Sum(item => item.Amount);
            if (outstanding <= 0m || obligation.DueDate.Date > notificationDate) continue;
            if (!Guid.TryParse(obligation.CreatedBy, out var recipientId)) continue;
            var permissions = await effectivePermissions.GetEffectivePermissionsAsync(recipientId, obligation.TenantId, cancellationToken);
            if (!permissions.Contains("ProjectPaymentRequest.View")
                || !permissions.Contains("ProjectSettlement.View")) continue;

            var notificationType = obligation.DueDate.Date < notificationDate
                ? NotificationTypes.PaymentOverdue
                : NotificationTypes.PaymentDue;
            var milestoneName = obligation.MilestoneId is Guid milestoneId
                ? await milestoneLookup.FindActiveMilestoneNameForProjectAsync(obligation.ProjectId, milestoneId, cancellationToken)
                    ?? $"Milestone {milestoneId}"
                : null;
            var dispatchExists = await context.Set<PaymentNotificationDispatch>()
                .IgnoreQueryFilters()
                .AnyAsync(item =>
                    item.TenantId == obligation.TenantId
                    && item.PaymentObligationId == obligation.Id
                    && item.UserId == recipientId
                    && item.NotificationType == notificationType
                    && item.NotificationDate == notificationDate,
                    cancellationToken);
            if (dispatchExists) continue;

            var notificationId = Guid.NewGuid();
            context.Set<PaymentNotificationDispatch>().Add(new PaymentNotificationDispatch
            {
                Id = Guid.NewGuid(),
                TenantId = obligation.TenantId,
                CreatedBy = "PaymentDueNotificationService",
                PaymentObligationId = obligation.Id,
                UserId = recipientId,
                NotificationType = notificationType,
                NotificationDate = notificationDate,
                NotificationId = notificationId,
            });

            try
            {
                var organizationId = projectOrganizationLookup is null
                    ? null
                    : await projectOrganizationLookup.ResolveOrganizationIdAsync(obligation.TenantId, obligation.ProjectId, cancellationToken);
                await notificationService.SendAsync(new Notification
                {
                    Id = notificationId,
                    TenantId = obligation.TenantId,
                    OrganizationId = organizationId,
                    UserId = recipientId,
                    Type = notificationType,
                    ReferenceType = nameof(PaymentObligation),
                    ReferenceId = obligation.Id,
                    Title = notificationType == NotificationTypes.PaymentOverdue ? "Payment overdue" : "Payment due",
                    Body = $"{obligation.ProjectName} — {obligation.PaymentReference}{(milestoneName is null ? string.Empty : $" — Milestone: {milestoneName}")}: {obligation.Currency} {outstanding:0.00##} due {obligation.DueDate:yyyy-MM-dd}.",
                    IsInAppVisible = true,
                    SentAt = asOfUtc,
                    CreatedBy = "PaymentDueNotificationService",
                }, cancellationToken);
                createdCount++;
            }
            catch (DbUpdateException)
            {
                var concurrentDispatchExists = await context.Set<PaymentNotificationDispatch>()
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(item =>
                        item.TenantId == obligation.TenantId
                        && item.PaymentObligationId == obligation.Id
                        && item.UserId == recipientId
                        && item.NotificationType == notificationType
                        && item.NotificationDate == notificationDate,
                        cancellationToken);
                if (!concurrentDispatchExists) throw;

                foreach (var entry in context.ChangeTracker.Entries<PaymentNotificationDispatch>()
                             .Where(entry => entry.State == EntityState.Added
                                 && entry.Entity.PaymentObligationId == obligation.Id
                                 && entry.Entity.UserId == recipientId))
                {
                    entry.State = EntityState.Detached;
                }
                foreach (var entry in context.ChangeTracker.Entries<Notification>()
                             .Where(entry => entry.State == EntityState.Added && entry.Entity.Id == notificationId))
                {
                    entry.State = EntityState.Detached;
                }
            }
        }

        return createdCount;
    }

}
