using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Notifications.Application;

namespace NEXUS.Modules.Notifications.Api;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUserNotifications([FromQuery] Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? unreadOnly = null, CancellationToken cancellationToken = default)
    {
        var result = await _notificationService.GetUserNotificationsAsync(userId, page, pageSize, unreadOnly, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/mark-read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var result = await _notificationService.MarkAsReadAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok();
    }
}
