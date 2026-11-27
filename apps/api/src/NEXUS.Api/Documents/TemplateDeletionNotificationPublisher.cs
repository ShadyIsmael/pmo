using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Documents;

/// <summary>
/// Queues idempotent in-app notifications through the shared notification preference
/// pipeline. The workflow calls this before committing the request or decision
/// transaction so the in-app notification row commits atomically with it.
/// </summary>
public sealed class TemplateDeletionNotificationPublisher(
    AppDbContext context,
    INotificationService notifications) : ITemplateDeletionNotificationPublisher
{
    private const string CreatedBy = "TemplateDeletionNotificationPublisher";
    private const string ReferenceType = "TemplateDeletionRequest";

    public async Task<Result> PublishPendingAsync(
        TemplateDeletionRequest request,
        IReadOnlyCollection<Guid> recipientIds,
        CancellationToken cancellationToken)
    {
        if (!IsValidRequest(request) || recipientIds is null)
            return Result.Failure(Error.Validation("The template deletion notification scope is invalid."));
        if (!string.Equals(request.Status, TemplateDeletionRequest.StatusPendingApproval, StringComparison.Ordinal))
            return Result.Success();
        if (!await IsTenantActiveAsync(request.TenantId, cancellationToken))
            return Result.Success();

        var requestedRecipientIds = recipientIds
            .Where(userId => userId != Guid.Empty && userId != request.RequesterId)
            .Distinct()
            .ToArray();
        if (requestedRecipientIds.Length == 0)
            return Result.Success();

        var activeRecipientIds = await context.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.TenantId == request.TenantId
                && user.IsActive
                && !user.IsDeleted
                && requestedRecipientIds.Contains(user.Id))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        foreach (var recipientId in activeRecipientIds)
        {
            var notification = new Notification
            {
                Id = CreateNotificationId(request.TenantId, request.Id, NotificationTypes.TemplateDeletionRequested, recipientId),
                TenantId = request.TenantId,
                OrganizationId = request.OwnerOrganizationId,
                UserId = recipientId,
                Type = NotificationTypes.TemplateDeletionRequested,
                ReferenceType = ReferenceType,
                ReferenceId = request.Id,
                Title = request.TemplateName,
                Body = request.Reason,
                IsInAppVisible = true,
                SentAt = DateTime.UtcNow,
                CreatedBy = CreatedBy,
            };

            var queued = await notifications.SendIdempotentAsync(
                notification,
                allowPush: false,
                allowEmail: false,
                cancellationToken);
            if (queued.IsFailure)
                return Result.Failure(queued.Error ?? Error.Validation("The template deletion notification could not be queued."));
        }

        return Result.Success();
    }

    public async Task<Result> PublishDecisionAsync(
        TemplateDeletionRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidRequest(request))
            return Result.Failure(Error.Validation("The template deletion decision notification scope is invalid."));

        var notificationType = request.Status switch
        {
            TemplateDeletionRequest.StatusApproved => NotificationTypes.TemplateDeletionApproved,
            TemplateDeletionRequest.StatusRejected => NotificationTypes.TemplateDeletionRejected,
            _ => null,
        };
        if (notificationType is null || request.RequesterId == Guid.Empty)
            return Result.Success();
        if (!await IsTenantActiveAsync(request.TenantId, cancellationToken))
            return Result.Success();

        var requesterIsActive = await context.Users.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(user => user.Id == request.RequesterId
                && user.TenantId == request.TenantId
                && user.IsActive
                && !user.IsDeleted,
                cancellationToken);
        if (!requesterIsActive)
            return Result.Success();

        var notification = new Notification
        {
            Id = CreateNotificationId(request.TenantId, request.Id, notificationType, request.RequesterId),
            TenantId = request.TenantId,
            OrganizationId = request.OwnerOrganizationId,
            UserId = request.RequesterId,
            Type = notificationType,
            ReferenceType = ReferenceType,
            ReferenceId = request.Id,
            Title = request.TemplateName,
            Body = request.DecisionComments,
            IsInAppVisible = true,
            SentAt = request.DecidedAt ?? DateTime.UtcNow,
            CreatedBy = CreatedBy,
        };

        var queued = await notifications.SendIdempotentAsync(
            notification,
            allowPush: false,
            allowEmail: false,
            cancellationToken);
        return queued.IsFailure
            ? Result.Failure(queued.Error ?? Error.Validation("The template deletion decision notification could not be queued."))
            : Result.Success();
    }

    private static bool IsValidRequest(TemplateDeletionRequest? request) =>
        request is not null
        && request.Id != Guid.Empty
        && request.TenantId != Guid.Empty
        && request.TemplateId != Guid.Empty
        && request.RequesterId != Guid.Empty
        && !string.IsNullOrWhiteSpace(request.TemplateName)
        && request.OwnerOrganizationId != Guid.Empty;

    private Task<bool> IsTenantActiveAsync(Guid tenantId, CancellationToken cancellationToken) =>
        context.Tenants.AsNoTracking().AnyAsync(
            tenant => tenant.Id == tenantId && tenant.Status == TenantLifecycleStatus.Active,
            cancellationToken);

    private static Guid CreateNotificationId(Guid tenantId, Guid requestId, string type, Guid recipientId)
    {
        var identity = $"template-deletion:{tenantId:D}:{requestId:D}:{type}:{recipientId:D}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return new Guid(hash.AsSpan(0, 16));
    }
}
