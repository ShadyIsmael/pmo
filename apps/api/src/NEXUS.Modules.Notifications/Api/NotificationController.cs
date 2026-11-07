using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Notifications.Application;
using NEXUS.SharedKernel;
using System.Security.Claims;

namespace NEXUS.Modules.Notifications.Api;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IDeviceTokenService _deviceTokenService;
    private readonly IProjectPublicIdLookup? _projectPublicIdLookup;
    private readonly IMeetingPublicIdLookup? _meetingPublicIdLookup;

    public NotificationController(INotificationService notificationService, IDeviceTokenService deviceTokenService, IProjectPublicIdLookup? projectPublicIdLookup = null, IMeetingPublicIdLookup? meetingPublicIdLookup = null)
    {
        _notificationService = notificationService;
        _deviceTokenService = deviceTokenService;
        _projectPublicIdLookup = projectPublicIdLookup;
        _meetingPublicIdLookup = meetingPublicIdLookup;
    }

    [HttpGet]
    public async Task<IActionResult> GetUserNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? unreadOnly = null, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var tenantId)) return Unauthorized();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _notificationService.GetUserNotificationsAsync(tenantId, userId, page, pageSize, unreadOnly, cancellationToken);
        var items = new List<NotificationListItemDto>(result.Items.Count);
        foreach (var notification in result.Items)
        {
            var projectReference = string.Equals(notification.ReferenceType, "Project", StringComparison.OrdinalIgnoreCase);
            var meetingReference = string.Equals(notification.ReferenceType, "Meeting", StringComparison.OrdinalIgnoreCase)
                || string.Equals(notification.ReferenceType, "MeetingMinutesComment", StringComparison.OrdinalIgnoreCase)
                || string.Equals(notification.ReferenceType, "MeetingMinutesCommentReply", StringComparison.OrdinalIgnoreCase)
                || IsMeetingNotificationType(notification.Type);
            var riskActionFollowUpReference = string.Equals(notification.Type, "RiskActionFollowUpDue", StringComparison.Ordinal)
                && string.Equals(notification.ReferenceType, "RiskActionFollowUpDue", StringComparison.Ordinal);
            Guid? referencePublicId = null;
            if (projectReference && notification.ReferenceId.HasValue && _projectPublicIdLookup is not null)
                referencePublicId = await _projectPublicIdLookup.FindPublicIdAsync(notification.ReferenceId.Value, tenantId, cancellationToken);
            else if (meetingReference && notification.ReferenceId.HasValue && _meetingPublicIdLookup is not null)
                referencePublicId = notification.ReferencePublicId;
            else if (riskActionFollowUpReference && notification.ReferenceId.HasValue)
                // Risk Action follow-up producers persist only the Action's public identifier in this reference field.
                referencePublicId = notification.ReferenceId.Value;
            var referenceId = projectReference || riskActionFollowUpReference ? null : notification.ReferenceId;
            if (meetingReference)
            {
                var resolvedMeetingPublicId = notification.ReferenceId.HasValue && _meetingPublicIdLookup is not null
                    ? await _meetingPublicIdLookup.FindPublicIdAsync(notification.ReferenceId.Value, tenantId, cancellationToken)
                    : null;
                // Older Meeting notifications stored the database ID in ReferenceId. Resolve both old
                // and new records to a safe public identifier; never expose an unresolved internal ID.
                referenceId = resolvedMeetingPublicId;
                if (resolvedMeetingPublicId is null) referencePublicId = null;
            }

            items.Add(new NotificationListItemDto(
                notification.Id,
                notification.Title,
                notification.Body,
                notification.Type,
                notification.IsRead,
                notification.ReadAt,
                notification.SentAt,
                meetingReference ? "Meeting" : notification.ReferenceType,
                referenceId,
                referencePublicId));
        }
        return Ok(new PagedResult<NotificationListItemDto>(items, result.Page, result.PageSize, result.TotalCount));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var tenantId)) return Unauthorized();
        return Ok(new { count = await _notificationService.GetUnreadCountAsync(tenantId, userId, cancellationToken) });
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var tenantId)) return Unauthorized();
        return Ok(await _notificationService.GetPreferencesAsync(tenantId, userId, cancellationToken));
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var tenantId)) return Unauthorized();
        var result = await _notificationService.UpdatePreferencesAsync(tenantId, userId, request, cancellationToken);
        return result.IsFailure ? BadRequest(new { error = result.Error?.Message }) : NoContent();
    }

    [HttpPost("{id:guid}/mark-read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var tenantId)) return Unauthorized();
        var result = await _notificationService.MarkAsReadAsync(tenantId, userId, id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var tenantId)) return Unauthorized();
        await _notificationService.MarkAllAsReadAsync(tenantId, userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] RegisterDeviceTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var tenantId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 4000
            || !new[] { "web", "android", "ios" }.Contains(request.Platform, StringComparer.OrdinalIgnoreCase)
            || request.DeviceName?.Length > 200)
            return BadRequest(new { error = "The device registration is invalid." });
        var device = await _deviceTokenService.RegisterAsync(tenantId, userId, request, cancellationToken);
        return Ok(device);
    }

    [HttpDelete("devices/{deviceId:guid}")]
    public async Task<IActionResult> DeactivateDevice(Guid deviceId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var tenantId)) return Unauthorized();
        var deactivated = await _deviceTokenService.DeactivateAsync(tenantId, userId, deviceId, cancellationToken);
        return deactivated ? NoContent() : NotFound();
    }

    private bool TryGetCurrentUser(out Guid userId, out Guid tenantId)
    {
        userId = Guid.Empty;
        tenantId = Guid.Empty;
        var hasUser = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        var hasTenant = Guid.TryParse(User.FindFirstValue("tenantId"), out tenantId);
        return hasUser && hasTenant;
    }

    private static bool IsMeetingNotificationType(string type) => type is
        "MeetingReminder" or "MeetingInvitation" or "MeetingStarted" or
        "MeetingMinutesPublished" or "MeetingMinutesComment" or
        "MeetingMinutesCommentReply" or "MeetingMinutesShared" or "MeetingCompleted";

    private sealed record NotificationListItemDto(
        Guid Id,
        string Title,
        string? Body,
        string Type,
        bool IsRead,
        DateTime? ReadAt,
        DateTime SentAt,
        string? ReferenceType,
        Guid? ReferenceId,
        Guid? ReferencePublicId);
}
