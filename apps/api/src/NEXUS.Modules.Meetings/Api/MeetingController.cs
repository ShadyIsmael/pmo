using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Meetings.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Api;

[ApiController]
[Route("api/meetings")]
[Authorize]
public sealed class MeetingController : ControllerBase
{
    private readonly IMeetingService _meetingService;

    public MeetingController(IMeetingService meetingService) => _meetingService = meetingService;

    [HttpGet]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] Guid? projectId = null, [FromQuery] DateTime? dateFrom = null, [FromQuery] DateTime? dateTo = null, CancellationToken cancellationToken = default)
        => Ok(await _meetingService.GetPagedAsync(page, pageSize, search, status, projectId, dateFrom, dateTo, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _meetingService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [HttpPost]
    [Authorize(Policy = MeetingPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateMeetingRequest request, CancellationToken cancellationToken)
    {
        var result = await _meetingService.CreateAsync(request, CurrentUserId(), cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value) : ToActionResult(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MeetingPermissions.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMeetingRequest request, CancellationToken cancellationToken)
    {
        var result = await _meetingService.UpdateAsync(id, request, CurrentUserId(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MeetingPermissions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _meetingService.DeleteAsync(id, CurrentUserId(), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result.Error);
    }

    [HttpGet("{id:guid}/attendees")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetAttendees(Guid id, CancellationToken cancellationToken) => ToActionResult(await _meetingService.GetAttendeesAsync(id, cancellationToken));

    [HttpPost("{id:guid}/attendees")]
    [Authorize(Policy = MeetingPermissions.Edit)]
    public async Task<IActionResult> AddAttendee(Guid id, [FromBody] MeetingAttendeeInput request, CancellationToken cancellationToken) => ToActionResult(await _meetingService.AddAttendeeAsync(id, request, CurrentUserId(), cancellationToken));

    [HttpDelete("{id:guid}/attendees/{participantId:guid}")]
    [Authorize(Policy = MeetingPermissions.Edit)]
    public async Task<IActionResult> RemoveAttendee(Guid id, Guid participantId, CancellationToken cancellationToken)
    {
        var result = await _meetingService.RemoveAttendeeAsync(id, participantId, CurrentUserId(), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result.Error);
    }

    [HttpGet("{id:guid}/minutes")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetMinutes(Guid id, CancellationToken cancellationToken) => ToActionResult(await _meetingService.GetMinutesAsync(id, cancellationToken));

    [HttpPost("{id:guid}/minutes")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> CreateMinutes(Guid id, [FromBody] CreateMeetingMinutesRequest request, CancellationToken cancellationToken) => ToActionResult(await _meetingService.CreateMinutesAsync(id, request, CurrentUserId(), cancellationToken));

    [HttpPut("{id:guid}/minutes")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> UpdateMinutes(Guid id, [FromBody] UpdateMeetingMinutesRequest request, CancellationToken cancellationToken) => ToActionResult(await _meetingService.UpdateMinutesAsync(id, request, CurrentUserId(), cancellationToken));

    [HttpPost("{id:guid}/minutes/finalize")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> FinalizeMinutes(Guid id, [FromBody] FinalizeMeetingMinutesRequest request, CancellationToken cancellationToken) => ToActionResult(await _meetingService.FinalizeMinutesAsync(id, request, CurrentUserId(), cancellationToken));

    [HttpGet("{id:guid}/minutes/comments")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetComments(Guid id, CancellationToken cancellationToken) => ToActionResult(await _meetingService.GetCommentsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/minutes/comments")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] CreateMeetingMinutesCommentRequest request, CancellationToken cancellationToken) => ToActionResult(await _meetingService.AddCommentAsync(id, request, CurrentUserId(), cancellationToken));

    [HttpGet("{id:guid}/minutes/checklist")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetChecklist(Guid id, CancellationToken cancellationToken) => ToActionResult(await _meetingService.GetChecklistAsync(id, cancellationToken));

    [HttpPost("{id:guid}/minutes/checklist")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> CreateChecklistItem(Guid id, [FromBody] CreateMeetingMinutesChecklistItemRequest request, CancellationToken cancellationToken) => ToActionResult(await _meetingService.CreateChecklistItemAsync(id, request, CurrentUserId(), cancellationToken));

    [HttpPut("{id:guid}/minutes/checklist/reorder")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> ReorderChecklist(Guid id, [FromBody] ReorderMeetingMinutesChecklistRequest request, CancellationToken cancellationToken) => ToActionResult(await _meetingService.ReorderChecklistAsync(id, request, CurrentUserId(), cancellationToken));

    [HttpPut("{id:guid}/minutes/checklist/{itemId:guid}")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> UpdateChecklistItem(Guid id, Guid itemId, [FromBody] UpdateMeetingMinutesChecklistItemRequest request, CancellationToken cancellationToken) => ToActionResult(await _meetingService.UpdateChecklistItemAsync(id, itemId, request, CurrentUserId(), cancellationToken));

    [HttpDelete("{id:guid}/minutes/checklist/{itemId:guid}")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> DeleteChecklistItem(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        var result = await _meetingService.DeleteChecklistItemAsync(id, itemId, CurrentUserId(), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result.Error);
    }

    [HttpGet("{id:guid}/minutes/attachments")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetAttachments(Guid id, CancellationToken cancellationToken) => ToActionResult(await _meetingService.GetAttachmentsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/minutes/attachments")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> AddAttachments(Guid id, [FromForm] List<IFormFile> files, CancellationToken cancellationToken) => ToActionResult(await _meetingService.AddAttachmentsAsync(id, files, CurrentUserId(), cancellationToken));

    [HttpGet("{id:guid}/minutes/attachments/{attachmentId:guid}/content")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetAttachmentContent(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var result = await _meetingService.GetAttachmentContentAsync(id, attachmentId, cancellationToken);
        if (result.IsFailure) return ToActionResult(result.Error);
        return File(result.Value!.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}/minutes/attachments/{attachmentId:guid}")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var result = await _meetingService.DeleteAttachmentAsync(id, attachmentId, CurrentUserId(), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result.Error);
    }

    [HttpGet("{id:guid}/minutes/work-items")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetWorkItemLinks(Guid id, CancellationToken cancellationToken) => ToActionResult(await _meetingService.GetWorkItemLinksAsync(id, cancellationToken));

    [HttpPost("{id:guid}/minutes/work-items")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> AddWorkItemLink(Guid id, [FromBody] CreateMeetingWorkItemLinkRequest request, CancellationToken cancellationToken) => ToActionResult(await _meetingService.AddWorkItemLinkAsync(id, request, CurrentUserId(), cancellationToken));

    [HttpDelete("{id:guid}/minutes/work-items/{workItemId:guid}")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> RemoveWorkItemLink(Guid id, Guid workItemId, CancellationToken cancellationToken)
    {
        var result = await _meetingService.RemoveWorkItemLinkAsync(id, workItemId, CurrentUserId(), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result.Error);
    }

    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";

    private IActionResult ToActionResult(Error? error) => error?.StatusCode switch
    {
        401 => Unauthorized(),
        403 => StatusCode(StatusCodes.Status403Forbidden),
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message ?? "Request failed" })
    };

    private IActionResult ToActionResult<T>(NEXUS.SharedKernel.Result<T> result) => result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
}
