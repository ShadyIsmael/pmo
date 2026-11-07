using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Meetings.Application.DTOs;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.Meetings.Application;

public sealed class MeetingSharingService : IMeetingSharingService
{
    private const int MaxRecipientsPerRequest = 500;
    private const string MeetingReferenceType = "Meeting";
    private readonly DbContext _context;
    private readonly IMeetingService _meetingService;
    private readonly IMeetingWorkspaceService _workspaceService;
    private readonly IProjectAudienceLookup _projectAudience;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor _managementContext;
    private readonly MeetingSharingOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly IProjectOrganizationLookup? _projectOrganizationLookup;

    public MeetingSharingService(
        DbContext context,
        IMeetingService meetingService,
        IMeetingWorkspaceService workspaceService,
        IProjectAudienceLookup projectAudience,
        INotificationService notifications,
        IAuditService audit,
        IHttpContextAccessor httpContextAccessor,
        IManagementContextAccessor managementContext,
        IOptions<MeetingSharingOptions> options,
        IWebHostEnvironment environment,
        IProjectOrganizationLookup? projectOrganizationLookup = null)
    {
        _context = context;
        _meetingService = meetingService;
        _workspaceService = workspaceService;
        _projectAudience = projectAudience;
        _notifications = notifications;
        _audit = audit;
        _httpContextAccessor = httpContextAccessor;
        _managementContext = managementContext;
        _options = options.Value;
        _environment = environment;
        _projectOrganizationLookup = projectOrganizationLookup;
    }

    public async Task<Result<MeetingSharingRecipientsResponse>> GetRecipientsAsync(Guid meetingId, string? search, CancellationToken cancellationToken = default)
    {
        if (search is not null && search.Trim().Length > 100)
            return Result<MeetingSharingRecipientsResponse>.Failure(Error.Validation("Recipient search cannot exceed 100 characters."));
        var resolved = await GetCompletedMeetingAsync(meetingId, cancellationToken);
        if (resolved.IsFailure) return Result<MeetingSharingRecipientsResponse>.Failure(resolved.Error!);
        var context = resolved.Value!;
        var meetingUrl = BuildMeetingUrl(meetingId);

        var attendeeRows = await _context.Set<MeetingParticipant>().AsNoTracking()
            .Where(item => item.TenantId == context.TenantId && item.MeetingId == meetingId && !item.IsDeleted && item.UserId.HasValue)
            .Select(item => new { UserId = item.UserId!.Value, item.AttendanceStatus })
            .ToListAsync(cancellationToken);
        var attendeeStatuses = attendeeRows
            .Select(item => new { item.UserId, Status = NormalizeAttendeeStatus(item.AttendanceStatus) })
            .Where(item => item.Status is MeetingAttendanceStatuses.Present or MeetingAttendanceStatuses.Late)
            .GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => group.Any(item => item.Status == MeetingAttendanceStatuses.Late)
                ? MeetingAttendanceStatuses.Late : MeetingAttendanceStatuses.Present);

        var attendees = await _projectAudience.GetActiveUsersAsync(context.TenantId, attendeeStatuses.Keys, cancellationToken);
        var tenantUsers = await _projectAudience.SearchTenantUsersAsync(context.TenantId, search, 50, cancellationToken);
        var allUserIds = attendees.Keys.Concat(tenantUsers.Select(user => user.UserId)).Distinct().ToArray();
        var emailEligibility = await _notifications.GetChannelEligibilityAsync(context.TenantId, allUserIds, NotificationTypes.MeetingMinutesShared, cancellationToken, context.OrganizationId);
        var completionEligibility = await _notifications.GetChannelEligibilityAsync(context.TenantId, allUserIds, NotificationTypes.MeetingCompleted, cancellationToken, context.OrganizationId);

        MeetingSharingRecipientDto Map(ProjectAudienceUserReference user, bool attendeeShortcut, string? attendanceStatus)
        {
            emailEligibility.TryGetValue(user.UserId, out var email);
            completionEligibility.TryGetValue(user.UserId, out var completion);
            return new MeetingSharingRecipientDto(
                user.UserId,
                user.FullName,
                user.Email,
                attendeeShortcut,
                attendanceStatus,
                !string.IsNullOrWhiteSpace(user.Email) && email?.EmailEnabled == true && email.EmailAvailable,
                completion?.InAppEnabled == true,
                email?.EmailEnabled == true,
                email?.EmailAvailable == true);
        }

        var attendeeDtos = attendees.Values
            .OrderBy(user => user.FullName, StringComparer.CurrentCultureIgnoreCase)
            .Select(user => Map(user, true, attendeeStatuses[user.UserId]))
            .ToArray();
        var tenantUserDtos = tenantUsers
            .OrderBy(user => user.FullName, StringComparer.CurrentCultureIgnoreCase)
            .Select(user => Map(user, attendeeStatuses.TryGetValue(user.UserId, out var status), attendeeStatuses.GetValueOrDefault(user.UserId)))
            .ToArray();
        return Result<MeetingSharingRecipientsResponse>.Success(new MeetingSharingRecipientsResponse(attendeeDtos, tenantUserDtos, meetingUrl.Url ?? string.Empty));
    }

    public async Task<Result<IReadOnlyList<MeetingSharingHistoryEntryDto>>> GetHistoryAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        var resolved = await GetCompletedMeetingAsync(meetingId, cancellationToken);
        if (resolved.IsFailure) return Result<IReadOnlyList<MeetingSharingHistoryEntryDto>>.Failure(resolved.Error!);
        var tenantId = resolved.Value!.TenantId;
        var actions = new[] { "MeetingMinutesSharedByEmail", "MeetingCompletionNotified" };
        var auditRows = await _context.Set<AuditEntry>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.EntityType == nameof(Meeting)
                && item.EntityId == meetingId && actions.Contains(item.Action))
            .OrderByDescending(item => item.Timestamp)
            .Take(100)
            .Select(item => new SharingAuditRow(item.Id, item.Action, item.Changes, item.UserId, item.UserName, item.CreatedBy, item.Timestamp))
            .ToListAsync(cancellationToken);

        var parsedOperations = new List<SharingAuditOperation>();
        foreach (var row in auditRows)
        {
            if (TryParseSharingAudit(row, out var operation)) parsedOperations.Add(operation!);
        }

        var operations = parsedOperations
            .GroupBy(item => (item.Type, item.RequestId, item.ActorId))
            .Select(group =>
            {
                var first = group.OrderBy(item => item.SentAt).First();
                return new SharingAuditOperation(first.AuditEntryId, first.RequestId, first.Type, first.ActorId,
                    group.Select(item => item.SenderName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                    group.Min(item => item.SentAt), group.SelectMany(item => item.RecipientIds).Distinct().ToArray());
            })
            .OrderByDescending(item => item.SentAt)
            .Take(25)
            .ToArray();

        if (operations.Length == 0)
            return Result<IReadOnlyList<MeetingSharingHistoryEntryDto>>.Success(Array.Empty<MeetingSharingHistoryEntryDto>());

        var expected = operations.SelectMany(operation => operation.RecipientIds.Select(recipientId =>
            new ExpectedSharingNotification(operation, recipientId,
                CreateNotificationId(tenantId, operation.RequestId, operation.ActorId, operation.Type, recipientId)))).ToArray();
        var notificationIds = expected.Select(item => item.NotificationId).Distinct().ToArray();
        var notifications = new Dictionary<Guid, SharingNotificationRow>();
        var deliveries = new List<SharingDeliveryRow>();

        foreach (var batch in notificationIds.Chunk(1000))
        {
            var rows = await _context.Set<Notification>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ReferenceType == MeetingReferenceType
                    && item.ReferenceId == meetingId && batch.Contains(item.Id))
                .Select(item => new SharingNotificationRow(item.Id, item.Email, item.IsInAppVisible))
                .ToListAsync(cancellationToken);
            foreach (var row in rows) notifications[row.Id] = row;
        }

        foreach (var batch in notificationIds.Chunk(1000))
        {
            var rows = await _context.Set<NotificationDelivery>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && batch.Contains(item.NotificationId))
                .Select(item => new SharingDeliveryRow(item.NotificationId, item.Channel, item.Status))
                .ToListAsync(cancellationToken);
            deliveries.AddRange(rows);
        }

        var identityIds = expected.Select(item => item.RecipientId).Concat(operations.Select(item => item.ActorId)).Distinct().ToArray();
        var identities = new Dictionary<Guid, ProjectAudienceUserReference>();
        foreach (var batch in identityIds.Chunk(500))
        {
            var rows = await _projectAudience.GetActiveUsersAsync(tenantId, batch, cancellationToken);
            foreach (var row in rows) identities[row.Key] = row.Value;
        }

        var entries = operations.Select(operation =>
        {
            var recipients = operation.RecipientIds.Select(recipientId =>
            {
                var notificationId = CreateNotificationId(tenantId, operation.RequestId, operation.ActorId, operation.Type, recipientId);
                notifications.TryGetValue(notificationId, out var notification);
                var recipient = identities.GetValueOrDefault(recipientId);
                var recipientDeliveries = deliveries.Where(item => item.NotificationId == notificationId).ToArray();
                var channelResults = new List<MeetingSharingHistoryDeliveryDto>();

                if (operation.Type == NotificationTypes.MeetingMinutesShared)
                {
                    var email = recipientDeliveries.FirstOrDefault(item => item.Channel == NotificationDeliveryChannel.Email);
                    channelResults.Add(new MeetingSharingHistoryDeliveryDto("Email",
                        notification is null || email is null ? "Failed" : ToHistoryStatus(email.Status)));
                }
                else
                {
                    channelResults.Add(new MeetingSharingHistoryDeliveryDto("InApp", notification?.IsInAppVisible == true ? "Sent" : "Failed"));
                    channelResults.AddRange(recipientDeliveries.Select(item => new MeetingSharingHistoryDeliveryDto(ChannelName(item.Channel), ToHistoryStatus(item.Status))));
                }

                return new MeetingSharingHistoryRecipientDto(recipientId, recipient?.FullName ?? string.Empty,
                    recipient?.Email ?? notification?.Email, AggregateHistoryStatus(channelResults.Select(item => item.Status)), channelResults);
            }).ToArray();
            var senderName = operation.SenderName;
            if (string.IsNullOrWhiteSpace(senderName) && identities.TryGetValue(operation.ActorId, out var sender))
                senderName = sender.FullName;
            var status = AggregateHistoryStatus(recipients.Select(item => item.Status));
            return new MeetingSharingHistoryEntryDto(operation.AuditEntryId, operation.Type, senderName ?? string.Empty,
                operation.SentAt, recipients.Length, status, recipients);
        }).ToArray();

        return Result<IReadOnlyList<MeetingSharingHistoryEntryDto>>.Success(entries);
    }

    public async Task<Result<MeetingSharingOperationResult>> SendMinutesEmailAsync(Guid meetingId, SendMeetingMinutesEmailRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var validation = ValidateRequest(request.RequestId, request.Locale, request.RecipientUserIds, actorId, out var locale, out var recipientIds);
        if (validation is not null) return Result<MeetingSharingOperationResult>.Failure(validation);

        var resolved = await GetCompletedMeetingAsync(meetingId, cancellationToken);
        if (resolved.IsFailure) return Result<MeetingSharingOperationResult>.Failure(resolved.Error!);
        var context = resolved.Value!;
        var minutesResult = await _meetingService.GetMinutesAsync(meetingId, cancellationToken);
        if (minutesResult.IsFailure || minutesResult.Value is null || minutesResult.Value.State != MeetingMinutesStates.Final)
            return Result<MeetingSharingOperationResult>.Failure(Error.Conflict("Only published final minutes can be shared."));
        var minutes = minutesResult.Value;
        if (request.MinutesId != minutes.Id || request.MinutesRowVersion is null || !request.MinutesRowVersion.AsSpan().SequenceEqual(minutes.RowVersion))
            return Result<MeetingSharingOperationResult>.Failure(Error.Conflict("The published minutes changed. Refresh the preview before sending."));

        var eligibleRecipients = await ResolveEligibleEmailRecipientsAsync(context, recipientIds, cancellationToken);
        if (eligibleRecipients.IsFailure) return Result<MeetingSharingOperationResult>.Failure(eligibleRecipients.Error!);
        var url = BuildMeetingUrl(meetingId);
        if (url.Error is not null) return Result<MeetingSharingOperationResult>.Failure(url.Error);

        var actionResult = await _workspaceService.GetActionItemsAsync(meetingId, cancellationToken);
        if (actionResult.IsFailure) return Result<MeetingSharingOperationResult>.Failure(actionResult.Error!);
        var body = BuildMinutesEmailBody(locale, context.Detail, minutes, actionResult.Value ?? Array.Empty<MeetingActionItemDto>(), url.Url!);
        var title = locale == "ar" ? $"محضر الاجتماع: {context.Meeting.Title}" : $"Meeting minutes: {context.Meeting.Title}";
        var queued = 0;
        var alreadyProcessed = 0;
        foreach (var recipient in eligibleRecipients.Value!)
        {
            var notification = CreateNotification(context, recipient.UserId, NotificationTypes.MeetingMinutesShared,
                title, body, actorId, false, CreateNotificationId(context.TenantId, request.RequestId, actorId, NotificationTypes.MeetingMinutesShared, recipient.UserId));
            var result = await _notifications.SendIdempotentAsync(notification, allowPush: false, allowEmail: true, cancellationToken: cancellationToken);
            if (result.IsFailure)
            {
                if (queued > 0)
                    await AuditShareAsync(context, actorId, "MeetingMinutesSharedByEmail", request.RequestId, recipientIds, cancellationToken);
                return Result<MeetingSharingOperationResult>.Failure(result.Error!);
            }
            if (result.Value == true) queued++;
            else alreadyProcessed++;
        }

        if (queued > 0)
            await AuditShareAsync(context, actorId, "MeetingMinutesSharedByEmail", request.RequestId, recipientIds, cancellationToken);
        return Result<MeetingSharingOperationResult>.Success(new MeetingSharingOperationResult(queued, alreadyProcessed));
    }

    public async Task<Result<MeetingSharingOperationResult>> NotifyCompletionAsync(Guid meetingId, SendMeetingCompletedNotificationRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var validation = ValidateRequest(request.RequestId, request.Locale, request.RecipientUserIds, actorId, out var locale, out var recipientIds);
        if (validation is not null) return Result<MeetingSharingOperationResult>.Failure(validation);

        var resolved = await GetCompletedMeetingAsync(meetingId, cancellationToken);
        if (resolved.IsFailure) return Result<MeetingSharingOperationResult>.Failure(resolved.Error!);
        var context = resolved.Value!;
        if (!context.Meeting.ProjectId.HasValue)
            return Result<MeetingSharingOperationResult>.Failure(Error.Validation("Completion notifications require a project-associated meeting."));

        var recipients = await _projectAudience.GetActiveUsersAsync(context.TenantId, recipientIds, cancellationToken);
        if (recipients.Count != recipientIds.Length)
            return Result<MeetingSharingOperationResult>.Failure(Error.Validation("Every recipient must be an active user in the current tenant."));
        var eligibility = await _notifications.GetChannelEligibilityAsync(context.TenantId, recipientIds, NotificationTypes.MeetingCompleted, cancellationToken, context.OrganizationId);
        if (recipientIds.Any(id => !eligibility.TryGetValue(id, out var value) || !value.InAppEnabled))
            return Result<MeetingSharingOperationResult>.Failure(Error.Validation("One or more selected users have disabled in-app notifications for meeting completion."));

        var url = BuildMeetingUrl(meetingId);
        if (url.Error is not null) return Result<MeetingSharingOperationResult>.Failure(url.Error);
        var body = BuildCompletionBody(locale, context.Detail, context.Meeting, url.Url!);
        var title = locale == "ar" ? $"اكتمل الاجتماع: {context.Meeting.Title}" : $"Meeting completed: {context.Meeting.Title}";
        var queued = 0;
        var alreadyProcessed = 0;
        foreach (var recipientId in recipientIds)
        {
            var notification = CreateNotification(context, recipientId, NotificationTypes.MeetingCompleted,
                title, body, actorId, true, CreateNotificationId(context.TenantId, request.RequestId, actorId, NotificationTypes.MeetingCompleted, recipientId));
            var result = await _notifications.SendIdempotentAsync(notification, allowPush: true, allowEmail: true, cancellationToken: cancellationToken);
            if (result.IsFailure)
            {
                if (queued > 0)
                    await AuditShareAsync(context, actorId, "MeetingCompletionNotified", request.RequestId, recipientIds, cancellationToken);
                return Result<MeetingSharingOperationResult>.Failure(result.Error!);
            }
            if (result.Value == true) queued++;
            else alreadyProcessed++;
        }

        if (queued > 0)
            await AuditShareAsync(context, actorId, "MeetingCompletionNotified", request.RequestId, recipientIds, cancellationToken);
        return Result<MeetingSharingOperationResult>.Success(new MeetingSharingOperationResult(queued, alreadyProcessed));
    }

    private async Task<Result<MeetingShareContext>> GetCompletedMeetingAsync(Guid meetingId, CancellationToken cancellationToken)
    {
        if (meetingId == Guid.Empty) return Result<MeetingShareContext>.Failure(Error.NotFound("Meeting not found."));
        var detailResult = await _meetingService.GetByIdAsync(meetingId, cancellationToken);
        if (detailResult.IsFailure || detailResult.Value is null)
            return Result<MeetingShareContext>.Failure(detailResult.Error ?? Error.NotFound("Meeting not found."));

        var tenantId = _managementContext.Current?.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            tenantId = Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("tenantId"), out var claimTenant) ? claimTenant : null;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            return Result<MeetingShareContext>.Failure(Error.Unauthorized("A valid tenant context is required to share a meeting."));

        var meeting = await _context.Set<Meeting>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == meetingId && item.TenantId == tenantId.Value && !item.IsDeleted, cancellationToken);
        if (meeting is null) return Result<MeetingShareContext>.Failure(Error.NotFound("Meeting not found."));
        if ((meeting.Status ?? MeetingStatuses.Scheduled) != MeetingStatuses.Completed)
            return Result<MeetingShareContext>.Failure(Error.Conflict("Meeting sharing is available only after the meeting is completed."));
        var organizationId = detailResult.Value.ProjectId.HasValue && _projectOrganizationLookup is not null
            ? await _projectOrganizationLookup.ResolveOrganizationIdAsync(tenantId.Value, detailResult.Value.ProjectId.Value, cancellationToken)
            : null;
        return Result<MeetingShareContext>.Success(new MeetingShareContext(tenantId.Value, meeting, detailResult.Value, organizationId));
    }

    private async Task<Result<IReadOnlyList<ProjectAudienceUserReference>>> ResolveEligibleEmailRecipientsAsync(MeetingShareContext context, Guid[] recipientIds, CancellationToken cancellationToken)
    {
        var allowed = await _projectAudience.GetActiveUsersAsync(context.TenantId, recipientIds, cancellationToken);
        if (allowed.Count != recipientIds.Length)
            return Result<IReadOnlyList<ProjectAudienceUserReference>>.Failure(Error.Validation("Every email recipient must be an active user in the current tenant."));

        var eligibility = await _notifications.GetChannelEligibilityAsync(context.TenantId, recipientIds, NotificationTypes.MeetingMinutesShared, cancellationToken, context.OrganizationId);
        var selected = recipientIds.Select(id => allowed[id]).ToArray();
        if (selected.Any(user => string.IsNullOrWhiteSpace(user.Email) || !MailAddress.TryCreate(user.Email, out _)
            || !eligibility.TryGetValue(user.UserId, out var value) || !value.EmailEnabled || !value.EmailAvailable))
            return Result<IReadOnlyList<ProjectAudienceUserReference>>.Failure(Error.Validation("Every selected recipient must have a valid email address, an enabled Meeting Minutes email preference, and an available shared email provider."));
        return Result<IReadOnlyList<ProjectAudienceUserReference>>.Success(selected);
    }

    private Error? ValidateRequest(Guid requestId, string? requestedLocale, IReadOnlyList<Guid>? requestedIds, Guid actorId, out string locale, out Guid[] recipientIds)
    {
        locale = string.Empty;
        recipientIds = Array.Empty<Guid>();
        if (actorId == Guid.Empty || (_managementContext.Current is { } current && current.UserId != actorId))
            return Error.Unauthorized("The current user could not be verified.");
        if (requestId == Guid.Empty) return Error.Validation("A request ID is required for safe retry handling.");
        locale = requestedLocale?.Trim().ToLowerInvariant() switch
        {
            "ar" => "ar",
            "en" => "en",
            _ => string.Empty
        };
        if (locale.Length == 0) return Error.Validation("Locale must be en or ar.");
        recipientIds = (requestedIds ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToArray();
        if (recipientIds.Length == 0 || recipientIds.Length > MaxRecipientsPerRequest)
            return Error.Validation($"Select between 1 and {MaxRecipientsPerRequest} unique recipients.");
        return null;
    }

    private Notification CreateNotification(MeetingShareContext context, Guid recipientId, string type, string title, string body, Guid actorId, bool visibleInApp, Guid id) => new()
    {
        Id = id,
        TenantId = context.TenantId,
        OrganizationId = context.OrganizationId,
        UserId = recipientId,
        Title = title,
        Body = body,
        Type = type,
        ReferenceType = MeetingReferenceType,
        ReferenceId = context.Meeting.Id,
        IsInAppVisible = visibleInApp,
        SentAt = DateTime.UtcNow,
        CreatedBy = actorId.ToString("D"),
    };

    private async Task AuditShareAsync(MeetingShareContext context, Guid actorId, string action, Guid requestId, Guid[] recipientIds, CancellationToken cancellationToken)
    {
        await _audit.LogAsync(new AuditEntry
        {
            TenantId = context.TenantId,
            EntityType = nameof(Meeting),
            EntityId = context.Meeting.Id,
            Action = action,
            Changes = JsonSerializer.Serialize(new { requestId, recipientUserIds = recipientIds }),
            UserId = actorId,
            UserName = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name),
            Timestamp = DateTime.UtcNow,
            CreatedBy = actorId.ToString("D"),
        }, cancellationToken);
    }

    private (string? Url, Error? Error) BuildMeetingUrl(Guid meetingId)
    {
        var baseText = _options.ApplicationBaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseText))
            return (null, Error.Validation("Configure Meetings:Sharing:ApplicationBaseUrl as the trusted HTTPS application URL before sharing."));
        if (!Uri.TryCreate(baseText.EndsWith('/') ? baseText : baseText + "/", UriKind.Absolute, out var baseUri)
            || !string.IsNullOrEmpty(baseUri.UserInfo) || !string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment)
            || (baseUri.Scheme != Uri.UriSchemeHttps && !(_environment.IsDevelopment() && baseUri.Scheme == Uri.UriSchemeHttp && IsLoopback(baseUri.Host))))
            return (null, Error.Validation("Configure Meetings:Sharing:ApplicationBaseUrl as the trusted HTTPS application URL before sharing."));
        return (new Uri(baseUri, $"meetings/{meetingId:D}").ToString(), null);
    }

    private static string BuildMinutesEmailBody(string locale, MeetingDetailDto meeting, MeetingMinutesDto minutes, IReadOnlyList<MeetingActionItemDto> actions, string url)
    {
        var isArabic = locale == "ar";
        var culture = CultureInfo.GetCultureInfo(isArabic ? "ar-EG" : "en-US");
        var project = meeting.ProjectName ?? (isArabic ? "بدون مشروع" : "No project");
        var organizer = meeting.OrganizerName ?? (isArabic ? "غير محدد" : "Not specified");
        var lines = new List<string>
        {
            isArabic ? "محضر الاجتماع المنشور" : "Published meeting minutes",
            $"{(isArabic ? "الاجتماع" : "Meeting")}: {meeting.Title}",
            $"{(isArabic ? "المشروع" : "Project")}: {project}",
            $"{(isArabic ? "التاريخ" : "Date")}: {FormatUtc(meeting.StartTime, culture)} UTC",
            $"{(isArabic ? "المنظم" : "Organizer")}: {organizer}",
            string.Empty,
            isArabic ? "الملخص" : "Summary",
            ContentOrEmpty(minutes.Summary, isArabic),
            string.Empty,
            isArabic ? "ملاحظات النقاش" : "Discussion notes",
            ContentOrEmpty(minutes.DiscussionNotes, isArabic),
            string.Empty,
            isArabic ? "القرارات" : "Decisions",
            ContentOrEmpty(minutes.Decisions, isArabic),
            string.Empty,
            isArabic ? "المخرجات" : "Outcomes",
            ContentOrEmpty(minutes.Outcomes, isArabic),
            string.Empty,
            isArabic ? "إجراءات المتابعة" : "Follow-up actions",
        };

        var orderedActions = actions.OrderBy(item => item.DueDate ?? DateTime.MaxValue).ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (orderedActions.Length == 0)
            lines.Add(isArabic ? "لا توجد إجراءات متابعة مسجلة." : "No follow-up actions recorded.");
        else
            lines.AddRange(orderedActions.Select(item => $"• {item.Title} — {(isArabic ? "المسؤول" : "Owner")}: {item.AssigneeName ?? (isArabic ? "غير محدد" : "Unassigned")}; {(isArabic ? "الاستحقاق" : "Due")}: {(item.DueDate.HasValue ? item.DueDate.Value.ToString("d", culture) : (isArabic ? "غير محدد" : "Not set"))}; {(isArabic ? "الحالة" : "Status")}: {item.Status}"));

        lines.Add(string.Empty);
        lines.Add($"{(isArabic ? "فتح الاجتماع" : "Open meeting")}: {url}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildCompletionBody(string locale, MeetingDetailDto detail, Meeting meeting, string url)
    {
        var isArabic = locale == "ar";
        var culture = CultureInfo.GetCultureInfo(isArabic ? "ar-EG" : "en-US");
        var completedAt = meeting.ActualEndTime ?? meeting.UpdatedAt ?? meeting.EndTime;
        return isArabic
            ? $"اكتمل الاجتماع وأصبح سجل الاجتماع متاحًا للمراجعة.\nالاجتماع: {meeting.Title}\nالمشروع: {detail.ProjectName ?? "بدون مشروع"}\nتاريخ الإكمال: {FormatUtc(completedAt, culture)} UTC\nفتح الاجتماع: {url}"
            : $"The meeting has been completed and its record is ready to review.\nMeeting: {meeting.Title}\nProject: {detail.ProjectName ?? "No project"}\nCompleted: {FormatUtc(completedAt, culture)} UTC\nOpen meeting: {url}";
    }

    private static string ContentOrEmpty(string? value, bool isArabic)
    {
        if (string.IsNullOrWhiteSpace(value)) return isArabic ? "لا يوجد محتوى مسجل." : "No content recorded.";
        var plainText = Regex.Replace(value, @"<(script|style)\b[^>]*>.*?</\1\s*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        plainText = Regex.Replace(plainText, @"<\s*br\s*/?\s*>|</\s*(p|div|li|h[1-6])\s*>", Environment.NewLine, RegexOptions.IgnoreCase);
        plainText = Regex.Replace(plainText, @"<[^>]+>", string.Empty, RegexOptions.Singleline);
        plainText = WebUtility.HtmlDecode(plainText);
        plainText = Regex.Replace(plainText, @"[\t ]+", " ");
        plainText = Regex.Replace(plainText, @"(\r?\n){3,}", Environment.NewLine + Environment.NewLine);
        return plainText.Trim();
    }

    private static string FormatUtc(DateTime value, CultureInfo culture) => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("f", culture);

    private static string? NormalizeAttendeeStatus(string? value) => value?.Trim() switch
    {
        MeetingAttendanceStatuses.Present or "Attended" => MeetingAttendanceStatuses.Present,
        MeetingAttendanceStatuses.Late => MeetingAttendanceStatuses.Late,
        _ => null,
    };

    private static Guid CreateNotificationId(Guid tenantId, Guid requestId, Guid actorId, string type, Guid recipientId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"meeting-share|{tenantId:D}|{requestId:D}|{actorId:D}|{type}|{recipientId:D}"));
        var id = new Guid(bytes.AsSpan(0, 16));
        return id == Guid.Empty ? new Guid(bytes.AsSpan(16, 16)) : id;
    }

    private static bool TryParseSharingAudit(SharingAuditRow row, out SharingAuditOperation? operation)
    {
        operation = null;
        var type = row.Action switch
        {
            "MeetingMinutesSharedByEmail" => NotificationTypes.MeetingMinutesShared,
            "MeetingCompletionNotified" => NotificationTypes.MeetingCompleted,
            _ => null,
        };
        var actorId = row.UserId ?? ParseUserId(row.CreatedBy);
        if (type is null || !actorId.HasValue || actorId.Value == Guid.Empty || string.IsNullOrWhiteSpace(row.Changes))
            return false;

        try
        {
            using var document = JsonDocument.Parse(row.Changes);
            var root = document.RootElement;
            if (!root.TryGetProperty("requestId", out var requestElement)
                || requestElement.ValueKind != JsonValueKind.String
                || !Guid.TryParse(requestElement.GetString(), out var requestId)
                || requestId == Guid.Empty
                || !root.TryGetProperty("recipientUserIds", out var recipientsElement)
                || recipientsElement.ValueKind != JsonValueKind.Array)
                return false;

            var recipientIds = recipientsElement.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String && Guid.TryParse(item.GetString(), out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .Take(500)
                .ToArray();
            if (recipientIds.Length == 0) return false;

            operation = new SharingAuditOperation(row.Id, requestId, type, actorId.Value, row.UserName, row.SentAt, recipientIds);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string AggregateHistoryStatus(IEnumerable<string> statuses)
    {
        var values = statuses.ToArray();
        if (values.Length == 0) return "Failed";
        var hasFailed = values.Contains("Failed", StringComparer.Ordinal);
        var hasPending = values.Contains("Pending", StringComparer.Ordinal);
        var hasRetrying = values.Contains("Retrying", StringComparer.Ordinal);
        var hasSent = values.Contains("Sent", StringComparer.Ordinal);
        var hasPartial = values.Contains("Partial", StringComparer.Ordinal);
        if (hasPartial) return "Partial";
        if (hasFailed) return hasPending || hasRetrying || hasSent ? "Partial" : "Failed";
        if (hasRetrying) return hasSent ? "Partial" : "Retrying";
        if (hasPending) return hasSent ? "Partial" : "Pending";
        return "Sent";
    }

    private static Guid? ParseUserId(string? value) => Guid.TryParse(value, out var id) ? id : null;

    private static bool IsLoopback(string host) => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("::1", StringComparison.OrdinalIgnoreCase);

    private static string ChannelName(NotificationDeliveryChannel channel) => channel switch
    {
        NotificationDeliveryChannel.Email => "Email",
        NotificationDeliveryChannel.Push => "Push",
        _ => channel.ToString(),
    };

    private static string ToHistoryStatus(NotificationDeliveryStatus status) => status switch
    {
        NotificationDeliveryStatus.Pending or NotificationDeliveryStatus.Processing => "Pending",
        NotificationDeliveryStatus.RetryScheduled => "Retrying",
        NotificationDeliveryStatus.Accepted => "Sent",
        NotificationDeliveryStatus.Delivered => "Sent",
        NotificationDeliveryStatus.FailedPermanent or NotificationDeliveryStatus.Cancelled => "Failed",
        _ => "Pending",
    };

    private sealed record MeetingShareContext(Guid TenantId, Meeting Meeting, MeetingDetailDto Detail, Guid? OrganizationId);
    private sealed record SharingAuditRow(Guid Id, string Action, string? Changes, Guid? UserId, string? UserName, string? CreatedBy, DateTime SentAt);
    private sealed record SharingAuditOperation(Guid AuditEntryId, Guid RequestId, string Type, Guid ActorId, string? SenderName, DateTime SentAt, Guid[] RecipientIds);
    private sealed record ExpectedSharingNotification(SharingAuditOperation Operation, Guid RecipientId, Guid NotificationId);
    private sealed record SharingNotificationRow(Guid Id, string? Email, bool IsInAppVisible);
    private sealed record SharingDeliveryRow(Guid NotificationId, NotificationDeliveryChannel Channel, NotificationDeliveryStatus Status);
}
