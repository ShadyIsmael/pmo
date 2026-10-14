using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Notifications.Application;
using System.Security.Claims;

namespace NEXUS.Modules.Notifications.Api;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IDeviceTokenService _deviceTokenService;

    public NotificationController(INotificationService notificationService, IDeviceTokenService deviceTokenService)
    {
        _notificationService = notificationService;
        _deviceTokenService = deviceTokenService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUserNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? unreadOnly = null, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var tenantId)) return Unauthorized();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _notificationService.GetUserNotificationsAsync(tenantId, userId, page, pageSize, unreadOnly, cancellationToken);
        return Ok(result);
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
}
