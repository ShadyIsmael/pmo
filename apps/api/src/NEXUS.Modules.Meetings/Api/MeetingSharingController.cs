using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Meetings.Application;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Api;

[ApiController]
[Route("api/meetings/{id:guid}/sharing")]
[Authorize(Policy = MeetingPermissions.Manage)]
public sealed class MeetingSharingController(IMeetingSharingService sharingService) : ControllerBase
{
    [HttpGet("recipients")]
    public async Task<IActionResult> GetRecipients(Guid id, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        var result = await sharingService.GetRecipientsAsync(id, search, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken cancellationToken)
    {
        var result = await sharingService.GetHistoryAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [HttpPost("minutes-email")]
    public async Task<IActionResult> SendMinutesEmail(Guid id, [FromBody] SendMeetingMinutesEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await sharingService.SendMinutesEmailAsync(id, request, CurrentUserId(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [HttpPost("completion")]
    public async Task<IActionResult> NotifyCompletion(Guid id, [FromBody] SendMeetingCompletedNotificationRequest request, CancellationToken cancellationToken)
    {
        var result = await sharingService.NotifyCompletionAsync(id, request, CurrentUserId(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : Guid.Empty;

    private IActionResult ToActionResult(Error? error) => error?.StatusCode switch
    {
        401 => Unauthorized(),
        403 => StatusCode(StatusCodes.Status403Forbidden),
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message ?? "Request failed" })
    };
}
