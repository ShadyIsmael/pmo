using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Meetings.Application.DTOs;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Api;

[ApiController]
[Route("api/meetings")]
[Authorize]
public sealed class MeetingController : ControllerBase
{
    private readonly IMeetingService _meetingService;
    private readonly IMeetingWorkspaceService _meetingWorkspaceService;

    public MeetingController(IMeetingService meetingService, IMeetingWorkspaceService meetingWorkspaceService)
    {
        _meetingService = meetingService;
        _meetingWorkspaceService = meetingWorkspaceService;
    }

    [HttpGet]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] Guid? projectId = null, [FromQuery] DateTime? dateFrom = null, [FromQuery] DateTime? dateTo = null, [FromQuery] string? projectIds = null, CancellationToken cancellationToken = default)
    {
        var parsedProjectIds = ParseProjectIds(projectIds);
        if (parsedProjectIds.Error is not null) return BadRequest(new { error = parsedProjectIds.Error });
        return Ok(await _meetingService.GetPagedAsync(page, pageSize, search, status, projectId, dateFrom, dateTo, parsedProjectIds.Value, cancellationToken));
    }

    [HttpGet("calendar")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetCalendar([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] string? projectIds = null, [FromQuery] string? status = null, [FromQuery] string? search = null, CancellationToken cancellationToken = default)
    {
        if (!from.HasValue || !to.HasValue) return BadRequest(new { error = "A calendar range with both from and to is required." });
        var parsedProjectIds = ParseProjectIds(projectIds);
        if (parsedProjectIds.Error is not null) return BadRequest(new { error = parsedProjectIds.Error });
        var result = await _meetingService.GetCalendarAsync(from.Value, to.Value, parsedProjectIds.Value, status, search, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [HttpGet("projects")]
    [Authorize(Policy = "WorkflowLookup.Meeting.Projects")]
    public async Task<IActionResult> GetProjects([FromQuery] string? search = null, CancellationToken cancellationToken = default)
        => Ok(await _meetingService.GetAccessibleProjectsAsync(search, cancellationToken));

    [HttpPost("series/preview")]
    [Authorize(Policy = MeetingPermissions.Create)]
    public IActionResult PreviewSeries([FromBody] MeetingRecurrenceRule rule)
    {
        var result = MeetingRecurrenceSchedule.Build(rule);
        return result.IsSuccess ? Ok(new MeetingRecurrencePreviewDto(
            result.Value!.Occurrences.Take(MeetingRecurrenceSchedule.PreviewOccurrences).ToArray(),
            result.Value.TotalOccurrenceCount, result.Value.SkippedDaylightSavingCount, result.Value.TimeZoneId)) : ToActionResult(result.Error);
    }

    [HttpPost("series")]
    [Authorize(Policy = MeetingPermissions.Create)]
    public async Task<IActionResult> CreateSeries([FromBody] CreateMeetingSeriesRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _meetingService.CreateSeriesAsync(request, actorId, cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(GetByPublicId), new { publicId = result.Value!.PublicId }, result.Value) : ToActionResult(result.Error);
    }

    [HttpGet("{publicId:guid}")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetByPublicId(Guid publicId, CancellationToken cancellationToken)
    {
        var result = await _meetingService.GetByPublicIdAsync(publicId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [HttpGet("{id:guid}/series")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetSeries(Guid id, CancellationToken cancellationToken)
    {
        var result = await _meetingService.GetSeriesAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [HttpPost]
    [Authorize(Policy = MeetingPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateMeetingRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _meetingService.CreateAsync(request, actorId, cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(GetByPublicId), new { publicId = result.Value!.PublicId }, result.Value) : ToActionResult(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MeetingPermissions.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMeetingRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _meetingService.UpdateAsync(id, request, actorId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [HttpPost("{id:guid}/start")]
    [Authorize(Policy = MeetingPermissions.Edit)]
    public async Task<IActionResult> Start(Guid id, [FromBody] MeetingLifecycleRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _meetingService.StartAsync(id, request, actorId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [HttpPost("{id:guid}/end")]
    [Authorize(Policy = MeetingPermissions.Edit)]
    public async Task<IActionResult> End(Guid id, [FromBody] MeetingLifecycleRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _meetingService.EndAsync(id, request, actorId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MeetingPermissions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _meetingService.DeleteAsync(id, actorId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result.Error);
    }

    [HttpGet("{id:guid}/attendees")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetAttendees(Guid id, CancellationToken cancellationToken) => ToActionResult(await _meetingService.GetAttendeesAsync(id, cancellationToken));

    [HttpPost("{id:guid}/attendees")]
    [Authorize(Policy = MeetingPermissions.Edit)]
    public async Task<IActionResult> AddAttendee(Guid id, [FromBody] MeetingAttendeeInput request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingService.AddAttendeeAsync(id, request, actorId, cancellationToken));
    }

    [HttpDelete("{id:guid}/attendees/{participantId:guid}")]
    [Authorize(Policy = MeetingPermissions.Edit)]
    public async Task<IActionResult> RemoveAttendee(Guid id, Guid participantId, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _meetingService.RemoveAttendeeAsync(id, participantId, actorId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result.Error);
    }

    [HttpGet("{id:guid}/participants")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetParticipants(Guid id, CancellationToken cancellationToken) => ToActionResult(await _meetingWorkspaceService.GetParticipantsAsync(id, cancellationToken));

    [HttpPut("{id:guid}/attendance")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> UpdateMyAttendance(Guid id, [FromBody] UpdateMeetingAttendanceRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingWorkspaceService.UpdateMyAttendanceAsync(id, request, actorId, cancellationToken));
    }

    [HttpPut("{id:guid}/participants/{participantId:guid}/attendance")]
    [Authorize(Policy = MeetingPermissions.Edit)]
    public async Task<IActionResult> UpdateAttendance(Guid id, Guid participantId, [FromBody] UpdateMeetingAttendanceRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingWorkspaceService.UpdateAttendanceAsync(id, participantId, request, actorId, cancellationToken));
    }

    [HttpPut("{id:guid}/participants/{participantId:guid}/attendance/organizer")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> UpdateOrganizerAttendance(Guid id, Guid participantId, [FromBody] UpdateMeetingAttendanceRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingWorkspaceService.UpdateOrganizerAttendanceAsync(id, participantId, request, actorId, cancellationToken));
    }

    [HttpGet("{id:guid}/agenda")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetAgenda(Guid id, CancellationToken cancellationToken) => ToActionResult(await _meetingWorkspaceService.GetAgendaAsync(id, cancellationToken));

    [HttpPost("{id:guid}/agenda")]
    [Authorize(Policy = MeetingPermissions.Edit)]
    public async Task<IActionResult> CreateAgendaItem(Guid id, [FromBody] CreateMeetingAgendaItemRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingWorkspaceService.CreateAgendaItemAsync(id, request, actorId, cancellationToken));
    }

    [HttpPut("{id:guid}/agenda/reorder")]
    [Authorize(Policy = MeetingPermissions.Edit)]
    public async Task<IActionResult> ReorderAgenda(Guid id, [FromBody] ReorderMeetingAgendaRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingWorkspaceService.ReorderAgendaAsync(id, request, actorId, cancellationToken));
    }

    [HttpPut("{id:guid}/agenda/{itemId:guid}")]
    [Authorize(Policy = MeetingPermissions.Edit)]
    public async Task<IActionResult> UpdateAgendaItem(Guid id, Guid itemId, [FromBody] UpdateMeetingAgendaItemRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingWorkspaceService.UpdateAgendaItemAsync(id, itemId, request, actorId, cancellationToken));
    }

    [HttpDelete("{id:guid}/agenda/{itemId:guid}")]
    [Authorize(Policy = MeetingPermissions.Edit)]
    public async Task<IActionResult> DeleteAgendaItem(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _meetingWorkspaceService.DeleteAgendaItemAsync(id, itemId, actorId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result.Error);
    }

    [HttpGet("{id:guid}/actions")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetActionItems(Guid id, CancellationToken cancellationToken) => ToActionResult(await _meetingWorkspaceService.GetActionItemsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/actions")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> CreateActionItem(Guid id, [FromBody] CreateMeetingActionItemRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingWorkspaceService.CreateActionItemAsync(id, request, actorId, cancellationToken));
    }

    [HttpPut("{id:guid}/actions/{itemId:guid}")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> UpdateActionItem(Guid id, Guid itemId, [FromBody] UpdateMeetingActionItemRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingWorkspaceService.UpdateActionItemAsync(id, itemId, request, actorId, cancellationToken));
    }

    [HttpPost("{id:guid}/actions/{itemId:guid}/work-item")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    [Authorize(Policy = "WorkItem.View")]
    public async Task<IActionResult> LinkActionWorkItem(Guid id, Guid itemId, [FromBody] LinkMeetingActionWorkItemRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingWorkspaceService.LinkActionWorkItemAsync(id, itemId, request, actorId, cancellationToken));
    }

    [HttpDelete("{id:guid}/actions/{itemId:guid}/work-item")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> UnlinkActionWorkItem(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _meetingWorkspaceService.UnlinkActionWorkItemAsync(id, itemId, actorId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result.Error);
    }

    [HttpDelete("{id:guid}/actions/{itemId:guid}")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> DeleteActionItem(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _meetingWorkspaceService.DeleteActionItemAsync(id, itemId, actorId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result.Error);
    }

    [HttpGet("{id:guid}/decisions")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetDecisions(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await _meetingWorkspaceService.GetDecisionsAsync(id, cancellationToken));

    [HttpPut("{id:guid}/decisions")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> SaveDecisions(Guid id, [FromBody] UpdateMeetingDecisionsRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingWorkspaceService.SaveDecisionsAsync(id, request, actorId, cancellationToken));
    }

    [HttpGet("{id:guid}/minutes")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetMinutes(Guid id, CancellationToken cancellationToken)
    {
        var result = await _meetingService.GetMinutesAsync(id, cancellationToken);
        if (result.IsFailure) return ToActionResult(result.Error);

        // MVC's object-result null formatter can turn Ok(null) into HTTP 204.
        // React Query requires a defined value, so preserve this endpoint's 200/null contract explicitly.
        return result.Value is null
            ? new JsonResult(null) { StatusCode = StatusCodes.Status200OK }
            : Ok(result.Value);
    }

    [HttpPost("{id:guid}/minutes")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> CreateMinutes(Guid id, [FromBody] CreateMeetingMinutesRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingService.CreateMinutesAsync(id, request, actorId, cancellationToken));
    }

    [HttpPut("{id:guid}/minutes")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> UpdateMinutes(Guid id, [FromBody] UpdateMeetingMinutesRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingService.UpdateMinutesAsync(id, request, actorId, cancellationToken));
    }

    [HttpPost("{id:guid}/minutes/finalize")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> FinalizeMinutes(Guid id, [FromBody] FinalizeMeetingMinutesRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingService.FinalizeMinutesAsync(id, request, actorId, cancellationToken));
    }

    [HttpGet("{id:guid}/minutes/comments")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetComments(Guid id, CancellationToken cancellationToken) => ToActionResult(await _meetingService.GetCommentsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/minutes/comments")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] CreateMeetingMinutesCommentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingService.AddCommentAsync(id, request, actorId, cancellationToken));
    }

    [HttpGet("{id:guid}/minutes/checklist")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetChecklist(Guid id, CancellationToken cancellationToken) => ToActionResult(await _meetingService.GetChecklistAsync(id, cancellationToken));

    [HttpPost("{id:guid}/minutes/checklist")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> CreateChecklistItem(Guid id, [FromBody] CreateMeetingMinutesChecklistItemRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingService.CreateChecklistItemAsync(id, request, actorId, cancellationToken));
    }

    [HttpPut("{id:guid}/minutes/checklist/reorder")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> ReorderChecklist(Guid id, [FromBody] ReorderMeetingMinutesChecklistRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingService.ReorderChecklistAsync(id, request, actorId, cancellationToken));
    }

    [HttpPut("{id:guid}/minutes/checklist/{itemId:guid}")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> UpdateChecklistItem(Guid id, Guid itemId, [FromBody] UpdateMeetingMinutesChecklistItemRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingService.UpdateChecklistItemAsync(id, itemId, request, actorId, cancellationToken));
    }

    [HttpDelete("{id:guid}/minutes/checklist/{itemId:guid}")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> DeleteChecklistItem(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _meetingService.DeleteChecklistItemAsync(id, itemId, actorId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result.Error);
    }

    [HttpGet("{id:guid}/minutes/attachments")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetAttachments(Guid id, CancellationToken cancellationToken) => ToActionResult(await _meetingService.GetAttachmentsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/minutes/attachments")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> AddAttachments(Guid id, [FromForm] List<IFormFile> files, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingService.AddAttachmentsAsync(id, files, actorId, cancellationToken));
    }

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
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _meetingService.DeleteAttachmentAsync(id, attachmentId, actorId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result.Error);
    }

    [HttpGet("{id:guid}/minutes/work-items")]
    [Authorize(Policy = MeetingPermissions.View)]
    public async Task<IActionResult> GetWorkItemLinks(Guid id, CancellationToken cancellationToken) => ToActionResult(await _meetingService.GetWorkItemLinksAsync(id, cancellationToken));

    [HttpPost("{id:guid}/minutes/work-items")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> AddWorkItemLink(Guid id, [FromBody] CreateMeetingWorkItemLinkRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _meetingService.AddWorkItemLinkAsync(id, request, actorId, cancellationToken));
    }

    [HttpDelete("{id:guid}/minutes/work-items/{workItemId:guid}")]
    [Authorize(Policy = MeetingPermissions.Manage)]
    public async Task<IActionResult> RemoveWorkItemLink(Guid id, Guid workItemId, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _meetingService.RemoveWorkItemLinkAsync(id, workItemId, actorId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result.Error);
    }

    private bool TryGetActorId(out string actorId)
    {
        if (User.Identity?.IsAuthenticated == true
            && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
            && parsed != Guid.Empty)
        {
            actorId = parsed.ToString();
            return true;
        }

        actorId = string.Empty;
        return false;
    }

    private static (IReadOnlyList<Guid>? Value, string? Error) ParseProjectIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (null, null);
        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return (null, null);
        var ids = new List<Guid>(parts.Length);
        foreach (var part in parts)
        {
            if (!Guid.TryParse(part, out var id) || id == Guid.Empty) return (null, "projectIds must contain valid project identifiers.");
            if (!ids.Contains(id)) ids.Add(id);
        }
        return (ids, null);
    }

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
