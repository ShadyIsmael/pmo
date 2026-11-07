using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Persistence;

/// <summary>
/// Publishes Meeting lifecycle and Minutes collaboration notifications through the
/// shared preference and delivery pipeline. It deliberately does not open or commit
/// transactions: NotificationService.SaveChanges participates in any transaction
/// owned by the Meeting caller on the shared scoped AppDbContext.
/// </summary>
public sealed class MeetingNotificationPublisher : IMeetingNotificationPublisher
{
    private const string ReferenceType = "Meeting";
    private const string CreatedBy = "MeetingNotificationPublisher";

    private readonly AppDbContext _context;
    private readonly INotificationService _notifications;
    private readonly IUserEffectivePermissionService _effectivePermissions;
    private readonly IManagementScopeResolver _managementScope;
    private readonly IProjectOrganizationLookup? _projectOrganizationLookup;

    public MeetingNotificationPublisher(
        AppDbContext context,
        INotificationService notifications,
        IUserEffectivePermissionService effectivePermissions,
        IManagementScopeResolver managementScope,
        IProjectOrganizationLookup? projectOrganizationLookup = null)
    {
        _context = context;
        _notifications = notifications;
        _effectivePermissions = effectivePermissions;
        _managementScope = managementScope;
        _projectOrganizationLookup = projectOrganizationLookup;
    }

    public async Task<Result> QueueInvitationAsync(Meeting meeting, MeetingParticipant invitee, Guid actorId, CancellationToken cancellationToken = default)
    {
        var validation = ValidateMeetingIdentity(meeting);
        if (validation is not null) return Result.Failure(validation);
        if (actorId == Guid.Empty) return Result.Failure(Error.Unauthorized("The notification actor could not be verified."));
        if (!await IsActiveInviteeAsync(meeting, invitee, cancellationToken)) return Result.Success();
        if (invitee.UserId == actorId) return Result.Success();

        var organizerName = meeting.OrganizerId.HasValue
            ? await _context.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(user => user.Id == meeting.OrganizerId.Value
                    && user.TenantId == meeting.TenantId
                    && user.IsActive
                    && !user.IsDeleted)
                .Select(user => (user.FirstName + " " + user.LastName).Trim())
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var enOrganizer = string.IsNullOrWhiteSpace(organizerName) ? "the meeting organizer" : organizerName;
        var arOrganizer = string.IsNullOrWhiteSpace(organizerName) ? "\u0645\u0646\u0638\u0645 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639" : organizerName;

        var safeTitle = NormalizeMeetingTitle(meeting.Title);
        var title = "Meeting Invitation\n\u062F\u0639\u0648\u0629 \u0644\u062D\u0636\u0648\u0631 \u0627\u062C\u062A\u0645\u0627\u0639";
        var body = $"You are invited by {enOrganizer} to \"{safeTitle}\", scheduled for {FormatUtc(meeting.StartTime)} UTC.\n\n\u0642\u0627\u0645 {arOrganizer} \u0628\u062F\u0639\u0648\u062A\u0643 \u0625\u0644\u0649 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639 \"{safeTitle}\" \u0627\u0644\u0645\u0642\u0631\u0631 \u0641\u064A {FormatUtc(meeting.StartTime)} UTC.";
        return await QueueAsync(meeting, NotificationTypes.MeetingInvitation, $"invitation:{invitee.Id:D}", [invitee.UserId!.Value], title, body, null, null, cancellationToken);
    }

    public async Task<Result> QueueReminderAsync(Meeting meeting, MeetingParticipant recipient, DateTime scheduledAtUtc, CancellationToken cancellationToken = default)
    {
        var validation = ValidateMeetingIdentity(meeting);
        if (validation is not null) return Result.Failure(validation);
        if (meeting.Status != MeetingStatuses.Scheduled || !await IsActiveInviteeAsync(meeting, recipient, cancellationToken)) return Result.Success();
        var recipientId = recipient.UserId!.Value;
        var scheduledAt = AsUtc(scheduledAtUtc);
        var eventKey = $"reminder:{meeting.Id:D}:{scheduledAt.Ticks}";
        var safeTitle = NormalizeMeetingTitle(meeting.Title);
        var title = "Upcoming Meeting\n\u062A\u0630\u0643\u064A\u0631 \u0628\u0627\u062C\u062A\u0645\u0627\u0639";
        var body = $"Reminder: \"{safeTitle}\" starts at {FormatUtc(meeting.StartTime)} UTC.\n\n\u062A\u0630\u0643\u064A\u0631: \u064A\u0628\u062F\u0623 \u0627\u062C\u062A\u0645\u0627\u0639 \"{safeTitle}\" \u0641\u064A {FormatUtc(meeting.StartTime)} UTC.";
        return await QueueAsync(meeting, NotificationTypes.MeetingReminder, eventKey, [recipientId], title, body, null, scheduledAt, cancellationToken);
    }

    public async Task<Result> QueueStartedAsync(Meeting meeting, Guid actorId, CancellationToken cancellationToken = default)
    {
        var validation = ValidateMeetingIdentity(meeting);
        if (validation is not null) return Result.Failure(validation);
        if (actorId == Guid.Empty) return Result.Failure(Error.Unauthorized("The notification actor could not be verified."));
        if (meeting.Status != MeetingStatuses.InProgress) return Result.Success();

        // Start notifications are tied to the persisted lifecycle event, not merely to
        // an in-memory status value. This query runs inside StartAsync's caller-owned
        // transaction, so it sees the ActualStartTime written by that operation.
        var persistedStart = await _context.Meetings.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.Id == meeting.Id
                && item.TenantId == meeting.TenantId
                && item.PublicId == meeting.PublicId
                && item.Status == MeetingStatuses.InProgress
                && item.ActualStartTime.HasValue
                && !item.IsDeleted,
                cancellationToken);
        if (!persistedStart) return Result.Success();

        var recipients = await GetActiveInviteeIdsAsync(meeting, cancellationToken);
        recipients = recipients.Where(id => id != actorId).ToArray();
        if (recipients.Length == 0) return Result.Success();

        var safeTitle = NormalizeMeetingTitle(meeting.Title);
        var title = "Meeting Started\n\u0628\u062F\u0623 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639";
        var body = $"The meeting \"{safeTitle}\" has started. You can open the meeting to follow its agenda and updates.\n\n\u0628\u062F\u0623 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639 \u00AB{safeTitle}\u00BB. \u064A\u0645\u0643\u0646\u0643 \u0641\u062A\u062D \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639 \u0644\u0645\u062A\u0627\u0628\u0639\u0629 \u062C\u062F\u0648\u0644 \u0627\u0644\u0623\u0639\u0645\u0627\u0644 \u0648\u0627\u0644\u0645\u0633\u062A\u062C\u062F\u0627\u062A.";
        return await QueueAsync(meeting, NotificationTypes.MeetingStarted, $"started:{meeting.Id:D}", recipients, title, body, null, null, cancellationToken);
    }

    public async Task<Result> QueueMinutesPublishedAsync(Meeting meeting, MeetingMinutes minutes, Guid actorId, CancellationToken cancellationToken = default)
    {
        var validation = ValidateMeetingIdentity(meeting);
        if (validation is not null) return Result.Failure(validation);
        if (actorId == Guid.Empty) return Result.Failure(Error.Unauthorized("The notification actor could not be verified."));
        if (!await IsPublishedMinutesForMeetingAsync(meeting, minutes, cancellationToken)) return Result.Success();

        var recipients = await GetAuthorizedAttendeeIdsAsync(meeting, actorId, cancellationToken);
        if (recipients.Length == 0) return Result.Success();

        var safeTitle = NormalizeMeetingTitle(meeting.Title);
        var title = "Meeting Minutes Available\n\u0645\u062D\u0636\u0631 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639 \u0645\u062A\u0627\u062D";
        var body = $"The minutes for meeting \"{safeTitle}\" have been published and are available for review.\n\n\u062A\u0645 \u0646\u0634\u0631 \u0645\u062D\u0636\u0631 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639 \u00AB{safeTitle}\u00BB \u0648\u0623\u0635\u0628\u062D \u0645\u062A\u0627\u062D\u064B\u0627 \u0644\u0644\u0645\u0631\u0627\u062C\u0639\u0629.";
        return await QueueAsync(meeting, NotificationTypes.MeetingMinutesPublished, $"minutes-published:{minutes.Id:D}", recipients, title, body, null, null, cancellationToken);
    }

    public async Task<Result> QueueMinutesCommentAsync(Meeting meeting, MeetingMinutes minutes, MeetingMinutesComment comment, Guid actorId, CancellationToken cancellationToken = default)
    {
        var validation = ValidateMeetingIdentity(meeting);
        if (validation is not null) return Result.Failure(validation);
        if (actorId == Guid.Empty) return Result.Failure(Error.Unauthorized("The notification actor could not be verified."));
        if (!await IsPublishedMinutesForMeetingAsync(meeting, minutes, cancellationToken)
            || comment.Id == Guid.Empty
            || comment.PublicId == Guid.Empty
            || comment.TenantId != meeting.TenantId
            || comment.MeetingMinutesId != minutes.Id
            || comment.IsDeleted)
            return Result.Success();

        var persistedComment = await _context.MeetingMinutesComments.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.Id == comment.Id
                && item.PublicId == comment.PublicId
                && item.TenantId == meeting.TenantId
                && item.MeetingMinutesId == minutes.Id
                && !item.IsDeleted,
                cancellationToken);
        if (!persistedComment) return Result.Success();

        var recipients = await GetAuthorizedAttendeeIdsAsync(meeting, actorId, cancellationToken);
        if (recipients.Length == 0) return Result.Success();

        var isReply = comment.ParentCommentId.HasValue;
        var type = isReply ? NotificationTypes.MeetingMinutesCommentReply : NotificationTypes.MeetingMinutesComment;
        var title = isReply ? "New Reply on Meeting Minutes\n\u0631\u062F \u062C\u062F\u064A\u062F \u0639\u0644\u0649 \u062A\u0639\u0644\u064A\u0642 \u0628\u0645\u062D\u0636\u0631 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639" : "New Comment on Meeting Minutes\n\u062A\u0639\u0644\u064A\u0642 \u062C\u062F\u064A\u062F \u0639\u0644\u0649 \u0645\u062D\u0636\u0631 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639";
        var safeTitle = NormalizeMeetingTitle(meeting.Title);
        var body = isReply
            ? $"A reply was added to a comment on the minutes of meeting \"{safeTitle}\".\n\n\u062A\u0645\u062A \u0625\u0636\u0627\u0641\u0629 \u0631\u062F \u0639\u0644\u0649 \u062A\u0639\u0644\u064A\u0642 \u0628\u0645\u062D\u0636\u0631 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639 \u00AB{safeTitle}\u00BB."
            : $"A new comment was added to the minutes of meeting \"{safeTitle}\".\n\n\u062A\u0645\u062A \u0625\u0636\u0627\u0641\u0629 \u062A\u0639\u0644\u064A\u0642 \u062C\u062F\u064A\u062F \u0639\u0644\u0649 \u0645\u062D\u0636\u0631 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639 \u00AB{safeTitle}\u00BB.";
        return await QueueAsync(meeting, type, $"comment:{comment.Id:D}", recipients, title, body, comment.PublicId, null, cancellationToken);
    }

    private async Task<Guid[]> GetAuthorizedAttendeeIdsAsync(Meeting meeting, Guid actorId, CancellationToken cancellationToken)
    {
        var projectId = meeting.ProjectId.GetValueOrDefault();
        var hasProject = projectId != Guid.Empty;
        if (hasProject)
        {
            var projectExists = await _context.Projects.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(project => project.Id == projectId
                    && project.TenantId == meeting.TenantId
                    && !project.IsDeleted,
                    cancellationToken);
            if (!projectExists) return Array.Empty<Guid>();
        }

        var inviteeIds = await GetActiveInviteeIdsAsync(meeting, cancellationToken);
        var eligible = new List<Guid>(inviteeIds.Length);
        foreach (var userId in inviteeIds)
        {
            if (userId == actorId) continue;
            var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(userId, meeting.TenantId, cancellationToken);
            if (!permissions.Contains(MeetingPermissions.View)) continue;
            if (hasProject && !await _managementScope.CanAccessProjectAsync(userId, meeting.TenantId, projectId, cancellationToken)) continue;
            eligible.Add(userId);
        }
        return eligible.ToArray();
    }

    private async Task<Guid[]> GetActiveInviteeIdsAsync(Meeting meeting, CancellationToken cancellationToken)
    {
        var invitees = await _context.MeetingParticipants.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.MeetingId == meeting.Id
                && item.TenantId == meeting.TenantId
                && !item.IsDeleted
                && item.UserId.HasValue)
            .Select(item => new InviteeSnapshot(item.UserId!.Value, item.InvitationResponseStatus))
            .ToListAsync(cancellationToken);

        var invitedUserIds = invitees
            .Where(item => IsActiveInvitationResponse(item.InvitationResponseStatus))
            .Select(item => item.UserId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (invitedUserIds.Length == 0) return Array.Empty<Guid>();

        return await _context.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => invitedUserIds.Contains(user.Id)
                && user.TenantId == meeting.TenantId
                && user.IsActive
                && !user.IsDeleted)
            .Select(user => user.Id)
            .ToArrayAsync(cancellationToken);
    }

    private async Task<bool> IsActiveInviteeAsync(Meeting meeting, MeetingParticipant invitee, CancellationToken cancellationToken)
    {
        if (invitee.Id == Guid.Empty
            || invitee.MeetingId != meeting.Id
            || invitee.TenantId != meeting.TenantId
            || !invitee.UserId.HasValue
            || invitee.UserId.Value == Guid.Empty
            || !IsActiveInvitationResponse(invitee.InvitationResponseStatus))
            return false;

        var currentInvite = await _context.MeetingParticipants.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == invitee.Id
                && item.MeetingId == meeting.Id
                && item.TenantId == meeting.TenantId
                && item.UserId == invitee.UserId.Value
                && !item.IsDeleted)
            .Select(item => new InviteeSnapshot(item.UserId!.Value, item.InvitationResponseStatus))
            .SingleOrDefaultAsync(cancellationToken);
        if (currentInvite is null || !IsActiveInvitationResponse(currentInvite.InvitationResponseStatus))
            return false;

        return await _context.Users.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(user => user.Id == invitee.UserId.Value
                && user.TenantId == meeting.TenantId
                && user.IsActive
                && !user.IsDeleted,
                cancellationToken);
    }

    private async Task<Result> QueueAsync(
        Meeting meeting,
        string type,
        string eventKey,
        IReadOnlyCollection<Guid> recipientIds,
        string title,
        string body,
        Guid? referencePublicId,
        DateTime? scheduledAt,
        CancellationToken cancellationToken)
    {
        var organizationId = meeting.ProjectId.HasValue && _projectOrganizationLookup is not null
            ? await _projectOrganizationLookup.ResolveOrganizationIdAsync(meeting.TenantId, meeting.ProjectId.Value, cancellationToken)
            : null;
        foreach (var recipientId in recipientIds.Distinct())
        {
            var notification = new Notification
            {
                Id = CreateNotificationId(meeting.TenantId, type, eventKey, recipientId),
                TenantId = meeting.TenantId,
                OrganizationId = organizationId,
                UserId = recipientId,
                Type = type,
                Title = title,
                Body = body,
                ReferenceType = ReferenceType,
                ReferenceId = meeting.PublicId,
                ReferencePublicId = referencePublicId,
                IsInAppVisible = true,
                ScheduledAt = scheduledAt,
                SentAt = DateTime.UtcNow,
                CreatedBy = CreatedBy,
            };

            // Push and email remain governed by the recipient's saved preferences and
            // provider availability. The shared service persists these intents without
            // sending to a provider inline and remains enlisted in the caller's transaction.
            var queued = await _notifications.SendIdempotentAsync(notification, allowPush: true, allowEmail: true, cancellationToken);
            if (queued.IsFailure) return Result.Failure(queued.Error!);
        }
        return Result.Success();
    }

    private static Error? ValidateMeetingIdentity(Meeting meeting)
    {
        if (meeting.Id == Guid.Empty || meeting.TenantId == Guid.Empty || meeting.PublicId == Guid.Empty)
            return Error.Validation("Meeting notifications require persisted tenant and public Meeting identifiers.");
        return null;
    }

    private async Task<bool> IsPublishedMinutesForMeetingAsync(Meeting meeting, MeetingMinutes minutes, CancellationToken cancellationToken)
    {
        if (meeting.Status != MeetingStatuses.Completed
            || minutes.Id == Guid.Empty
            || minutes.MeetingId != meeting.Id
            || minutes.TenantId != meeting.TenantId
            || minutes.IsDeleted
            || minutes.State != MeetingMinutesStates.Final)
            return false;

        var completed = await _context.Meetings.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.Id == meeting.Id
                && item.TenantId == meeting.TenantId
                && item.PublicId == meeting.PublicId
                && item.Status == MeetingStatuses.Completed
                && !item.IsDeleted,
                cancellationToken);
        if (!completed) return false;

        return await _context.MeetingMinutes.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.Id == minutes.Id
                && item.MeetingId == meeting.Id
                && item.TenantId == meeting.TenantId
                && item.State == MeetingMinutesStates.Final
                && !item.IsDeleted,
                cancellationToken);
    }

    private static Guid CreateNotificationId(Guid tenantId, string type, string eventKey, Guid recipientId)
    {
        var input = $"meeting-event|{tenantId:D}|{type}|{eventKey}|{recipientId:D}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var id = new Guid(hash.AsSpan(0, 16));
        return id == Guid.Empty ? new Guid(hash.AsSpan(16, 16)) : id;
    }

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static bool IsActiveInvitationResponse(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return true;
        var canonical = MeetingInvitationResponseStatuses.Canonicalize(status);
        return canonical is MeetingInvitationResponseStatuses.Pending
            or MeetingInvitationResponseStatuses.Accepted
            or MeetingInvitationResponseStatuses.Tentative;
    }

    private static string NormalizeMeetingTitle(string? title)
    {
        var normalized = (title ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "Meeting" : normalized;
    }

    private static string FormatUtc(DateTime value) => AsUtc(value).ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private sealed record InviteeSnapshot(Guid UserId, string? InvitationResponseStatus);
}
