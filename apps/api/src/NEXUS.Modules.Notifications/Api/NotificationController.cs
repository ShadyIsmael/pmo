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
        var result = await _notificationService.GetUserNotificationsAsync(tenantId, userId, page, pageSize, unreadOnly, cancellationToken);
        return Ok(result);
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
