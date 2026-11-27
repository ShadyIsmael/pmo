using NEXUS.Modules.Documents.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Documents;

public interface ITemplateDeletionNotificationPublisher
{
    /// <summary>Queues in-app notifications for active, same-tenant eligible approvers.</summary>
    Task<Result> PublishPendingAsync(
        TemplateDeletionRequest request,
        IReadOnlyCollection<Guid> recipientIds,
        CancellationToken cancellationToken);

    /// <summary>Queues an in-app decision notification for the active requester.</summary>
    Task<Result> PublishDecisionAsync(
        TemplateDeletionRequest request,
        CancellationToken cancellationToken);
}
