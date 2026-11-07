using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Meetings.Application.DTOs;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.Meetings.Application;

public sealed class MeetingWorkspaceService : IMeetingWorkspaceService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IUserReferenceLookup _userLookup;
    private readonly IWorkItemReferenceLookup? _workItemLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor _managementContext;

    public MeetingWorkspaceService(
        DbContext context,
        IAuditService auditService,
        IUserReferenceLookup userLookup,
        IHttpContextAccessor httpContextAccessor,
        IManagementContextAccessor managementContext,
        IWorkItemReferenceLookup? workItemLookup = null)
    {
        _context = context;
        _auditService = auditService;
        _userLookup = userLookup;
        _workItemLookup = workItemLookup;
        _httpContextAccessor = httpContextAccessor;
        _managementContext = managementContext;
    }

    public async Task<Result<IReadOnlyList<MeetingAgendaItemDto>>> GetAgendaAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        if (await FindMeetingAsync(meetingId, cancellationToken) is null)
            return Result<IReadOnlyList<MeetingAgendaItemDto>>.Failure(Error.NotFound("Meeting not found"));

        var items = await _context.Set<MeetingAgenda>().AsNoTracking()
            .Where(item => item.MeetingId == meetingId)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var users = await _userLookup.GetManyAsync(items.Where(item => item.OwnerId.HasValue).Select(item => item.OwnerId!.Value), cancellationToken);
        return Result<IReadOnlyList<MeetingAgendaItemDto>>.Success(items.Select(item => ToAgendaItem(item, users)).ToArray());
    }

    public async Task<Result<MeetingAgendaItemDto>> CreateAgendaItemAsync(Guid meetingId, CreateMeetingAgendaItemRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingAgendaItemDto>.Failure(Error.NotFound("Meeting not found"));
        if (!CanEditAgenda(meeting)) return Result<MeetingAgendaItemDto>.Failure(Error.Conflict("The agenda is read-only after the meeting starts"));
        var validation = await ValidateAgendaItemAsync(request.Topic, request.Description, request.DurationMinutes, request.OwnerId, cancellationToken);
        if (validation is not null) return Result<MeetingAgendaItemDto>.Failure(validation);

        var nextOrder = await _context.Set<MeetingAgenda>().Where(item => item.MeetingId == meetingId)
            .Select(item => (int?)item.SortOrder).MaxAsync(cancellationToken) ?? -1;
        var item = new MeetingAgenda
        {
            TenantId = meeting.TenantId,
            MeetingId = meetingId,
            Topic = request.Topic.Trim(),
            Description = Normalize(request.Description),
            DurationMinutes = request.DurationMinutes,
            OwnerId = request.OwnerId,
            SortOrder = nextOrder + 1,
            CreatedBy = userId
        };
        _context.Set<MeetingAgenda>().Add(item);
        TouchMeeting(meeting, userId);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<MeetingAgendaItemDto>.Failure(Error.Conflict("The meeting was changed by another user")); }
        await AuditAsync(meeting, "AgendaItemAdded", userId, new { item.Id, item.Topic, item.SortOrder }, cancellationToken);
        return Result<MeetingAgendaItemDto>.Success(await MapAgendaItemAsync(item, cancellationToken));
    }

    public async Task<Result<MeetingAgendaItemDto>> UpdateAgendaItemAsync(Guid meetingId, Guid itemId, UpdateMeetingAgendaItemRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingAgendaItemDto>.Failure(Error.NotFound("Meeting not found"));
        if (!CanEditAgenda(meeting)) return Result<MeetingAgendaItemDto>.Failure(Error.Conflict("The agenda is read-only after the meeting starts"));
        var item = await _context.Set<MeetingAgenda>().FirstOrDefaultAsync(candidate => candidate.MeetingId == meetingId && candidate.Id == itemId, cancellationToken);
        if (item is null) return Result<MeetingAgendaItemDto>.Failure(Error.NotFound("Agenda item not found"));
        var validation = await ValidateAgendaItemAsync(request.Topic, request.Description, request.DurationMinutes, request.OwnerId, cancellationToken);
        if (validation is not null) return Result<MeetingAgendaItemDto>.Failure(validation);
        SetOriginalRowVersion(item, request.RowVersion);
        item.Topic = request.Topic.Trim();
        item.Description = Normalize(request.Description);
        item.DurationMinutes = request.DurationMinutes;
        item.OwnerId = request.OwnerId;
        item.UpdatedBy = userId;
        TouchMeeting(meeting, userId);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<MeetingAgendaItemDto>.Failure(Error.Conflict("The agenda item was changed by another user")); }
        await AuditAsync(meeting, "AgendaItemUpdated", userId, new { item.Id, item.Topic, item.SortOrder }, cancellationToken);
        return Result<MeetingAgendaItemDto>.Success(await MapAgendaItemAsync(item, cancellationToken));
    }

    public async Task<Result> DeleteAgendaItemAsync(Guid meetingId, Guid itemId, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null) return Result.Failure(Error.NotFound("Meeting not found"));
        if (!CanEditAgenda(meeting)) return Result.Failure(Error.Conflict("The agenda is read-only after the meeting starts"));
        var item = await _context.Set<MeetingAgenda>().FirstOrDefaultAsync(candidate => candidate.MeetingId == meetingId && candidate.Id == itemId, cancellationToken);
        if (item is null) return Result.Failure(Error.NotFound("Agenda item not found"));
        var linkedActions = await _context.Set<MeetingAction>().Where(action => action.MeetingId == meetingId && action.AgendaItemId == itemId).ToListAsync(cancellationToken);
        foreach (var action in linkedActions)
        {
            action.AgendaItemId = null;
            action.UpdatedBy = userId;
        }
        item.IsDeleted = true;
        item.UpdatedBy = userId;
        TouchMeeting(meeting, userId);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(Error.Conflict("The agenda item was changed by another user")); }
        await AuditAsync(meeting, "AgendaItemRemoved", userId, new { item.Id, item.Topic, UnlinkedActionIds = linkedActions.Select(action => action.Id) }, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<MeetingAgendaItemDto>>> ReorderAgendaAsync(Guid meetingId, ReorderMeetingAgendaRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null) return Result<IReadOnlyList<MeetingAgendaItemDto>>.Failure(Error.NotFound("Meeting not found"));
        if (!CanEditAgenda(meeting)) return Result<IReadOnlyList<MeetingAgendaItemDto>>.Failure(Error.Conflict("The agenda is read-only after the meeting starts"));
        var items = await _context.Set<MeetingAgenda>().Where(item => item.MeetingId == meetingId)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var requestedIds = request.ItemIds ?? Array.Empty<Guid>();
        if (requestedIds.Count != items.Count || requestedIds.Distinct().Count() != requestedIds.Count
            || !requestedIds.ToHashSet().SetEquals(items.Select(item => item.Id)))
            return Result<IReadOnlyList<MeetingAgendaItemDto>>.Failure(Error.Validation("Reorder must include each agenda item exactly once"));

        var itemById = items.ToDictionary(item => item.Id);
        for (var index = 0; index < requestedIds.Count; index++)
        {
            itemById[requestedIds[index]].SortOrder = index;
            itemById[requestedIds[index]].UpdatedBy = userId;
        }
        TouchMeeting(meeting, userId);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<IReadOnlyList<MeetingAgendaItemDto>>.Failure(Error.Conflict("The meeting was changed by another user")); }
        await AuditAsync(meeting, "AgendaReordered", userId, new { ItemIds = requestedIds }, cancellationToken);
        var users = await _userLookup.GetManyAsync(items.Where(item => item.OwnerId.HasValue).Select(item => item.OwnerId!.Value), cancellationToken);
        return Result<IReadOnlyList<MeetingAgendaItemDto>>.Success(requestedIds.Select(id => ToAgendaItem(itemById[id], users)).ToArray());
    }

    public async Task<Result<IReadOnlyList<MeetingParticipantStatusDto>>> GetParticipantsAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null)
            return Result<IReadOnlyList<MeetingParticipantStatusDto>>.Failure(Error.NotFound("Meeting not found"));
        if (!CanAccessAttendance(meeting))
            return Result<IReadOnlyList<MeetingParticipantStatusDto>>.Failure(Error.Conflict("Attendance is available after the meeting starts"));
        var participants = await _context.Set<MeetingParticipant>().AsNoTracking()
            .Where(item => item.MeetingId == meetingId)
            .OrderBy(item => item.Name).ThenBy(item => item.Email)
            .ToListAsync(cancellationToken);
        var users = await _userLookup.GetManyAsync(participants.Where(item => item.UserId.HasValue).Select(item => item.UserId!.Value), cancellationToken);
        return Result<IReadOnlyList<MeetingParticipantStatusDto>>.Success(participants.Select(item => ToParticipant(item, users)).ToArray());
    }

    public async Task<Result<MeetingParticipantStatusDto>> UpdateMyAttendanceAsync(Guid meetingId, UpdateMeetingAttendanceRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingParticipantStatusDto>.Failure(Error.NotFound("Meeting not found"));
        if ((meeting.Status ?? MeetingStatuses.Scheduled) != MeetingStatuses.Scheduled)
            return Result<MeetingParticipantStatusDto>.Failure(Error.Conflict("Invitation responses are only available before the meeting starts"));
        if (!Guid.TryParse(userId, out var currentUserId)) return Result<MeetingParticipantStatusDto>.Failure(Error.Unauthorized("A valid user identity is required"));
        var status = MeetingInvitationResponseStatuses.Canonicalize(request.AttendanceStatus);
        if (status is null)
            return Result<MeetingParticipantStatusDto>.Failure(Error.Validation("Only Pending, Accepted, Declined, or Tentative can be self-reported"));
        var participant = await _context.Set<MeetingParticipant>().FirstOrDefaultAsync(item => item.MeetingId == meetingId && item.UserId == currentUserId, cancellationToken);
        if (participant is null) return Result<MeetingParticipantStatusDto>.Failure(Error.NotFound("Meeting participant not found"));
        return await SaveInvitationResponseAsync(meeting, participant, status, request.RowVersion, userId, cancellationToken);
    }

    public async Task<Result<MeetingParticipantStatusDto>> UpdateAttendanceAsync(Guid meetingId, Guid participantId, UpdateMeetingAttendanceRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingParticipantStatusDto>.Failure(Error.NotFound("Meeting not found"));
        if ((meeting.Status ?? MeetingStatuses.Scheduled) != MeetingStatuses.InProgress)
            return Result<MeetingParticipantStatusDto>.Failure(Error.Conflict("Attendance can only be recorded while the meeting is in progress"));
        var status = MeetingAttendanceStatuses.Canonicalize(request.ActualAttendanceStatus ?? request.AttendanceStatus);
        if (status is null) return Result<MeetingParticipantStatusDto>.Failure(Error.Validation("Invalid actual attendance status"));
        var participant = await _context.Set<MeetingParticipant>().FirstOrDefaultAsync(item => item.MeetingId == meetingId && item.Id == participantId, cancellationToken);
        if (participant is null) return Result<MeetingParticipantStatusDto>.Failure(Error.NotFound("Meeting participant not found"));
        return await SaveActualAttendanceAsync(meeting, participant, status, request.RowVersion, userId, cancellationToken);
    }

    public async Task<Result<MeetingParticipantStatusDto>> UpdateOrganizerAttendanceAsync(Guid meetingId, Guid participantId, UpdateMeetingAttendanceRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingParticipantStatusDto>.Failure(Error.NotFound("Meeting not found"));
        if (!Guid.TryParse(userId, out var organizerId) || meeting.OrganizerId != organizerId)
            return Result<MeetingParticipantStatusDto>.Failure(Error.Forbidden("Only the meeting organizer may use this endpoint"));
        if ((meeting.Status ?? MeetingStatuses.Scheduled) != MeetingStatuses.InProgress)
            return Result<MeetingParticipantStatusDto>.Failure(Error.Conflict("Attendance can only be recorded while the meeting is in progress"));
        var status = MeetingAttendanceStatuses.Canonicalize(request.ActualAttendanceStatus ?? request.AttendanceStatus);
        if (status is null) return Result<MeetingParticipantStatusDto>.Failure(Error.Validation("Invalid actual attendance status"));
        var participant = await _context.Set<MeetingParticipant>().FirstOrDefaultAsync(item => item.MeetingId == meetingId && item.Id == participantId, cancellationToken);
        if (participant is null) return Result<MeetingParticipantStatusDto>.Failure(Error.NotFound("Meeting participant not found"));
        return await SaveActualAttendanceAsync(meeting, participant, status, request.RowVersion, userId, cancellationToken);
    }

    public async Task<Result<MeetingDecisionDto>> GetDecisionsAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingDecisionDto>.Failure(Error.NotFound("Meeting not found"));
        if ((meeting.Status ?? MeetingStatuses.Scheduled) != MeetingStatuses.InProgress)
            return Result<MeetingDecisionDto>.Failure(Error.NotFound("Meeting decisions are only available while the meeting is in progress"));
        var minutes = await _context.Set<MeetingMinutes>().AsNoTracking().FirstOrDefaultAsync(item => item.MeetingId == meetingId, cancellationToken);
        return Result<MeetingDecisionDto>.Success(ToDecision(minutes));
    }

    public async Task<Result<MeetingDecisionDto>> SaveDecisionsAsync(Guid meetingId, UpdateMeetingDecisionsRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingDecisionDto>.Failure(Error.NotFound("Meeting not found"));
        if ((meeting.Status ?? MeetingStatuses.Scheduled) != MeetingStatuses.InProgress)
            return Result<MeetingDecisionDto>.Failure(Error.Conflict("Decisions can only be recorded while the meeting is in progress"));
        var minutes = await _context.Set<MeetingMinutes>().FirstOrDefaultAsync(item => item.MeetingId == meetingId, cancellationToken);
        if (minutes?.State == MeetingMinutesStates.Final)
            return Result<MeetingDecisionDto>.Failure(Error.Conflict("Final minutes cannot be changed during the meeting"));
        if (minutes is null && string.IsNullOrWhiteSpace(request.Decisions))
            return Result<MeetingDecisionDto>.Success(ToDecision(null));
        if (minutes is null)
        {
            minutes = new MeetingMinutes
            {
                MeetingId = meeting.Id,
                TenantId = meeting.TenantId,
                Decisions = Normalize(request.Decisions),
                State = MeetingMinutesStates.Draft,
                RecordedBy = Guid.TryParse(userId, out var recordedBy) ? recordedBy : null,
                CreatedBy = userId
            };
            _context.Set<MeetingMinutes>().Add(minutes);
        }
        else
        {
            SetOriginalRowVersion(minutes, request.RowVersion);
            minutes.Decisions = Normalize(request.Decisions);
            minutes.UpdatedBy = userId;
        }
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<MeetingDecisionDto>.Failure(Error.Conflict("Meeting decisions were changed by another user")); }
        await AuditAsync(meeting, "MeetingDecisionsRecorded", userId, new { minutes.Id }, cancellationToken);
        return Result<MeetingDecisionDto>.Success(ToDecision(minutes));
    }

    public async Task<Result<IReadOnlyList<MeetingActionItemDto>>> GetActionItemsAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null)
            return Result<IReadOnlyList<MeetingActionItemDto>>.Failure(Error.NotFound("Meeting not found"));
        if (!CanAccessActionItems(meeting))
            return Result<IReadOnlyList<MeetingActionItemDto>>.Failure(Error.Conflict("Action items are available after the meeting starts"));
        var items = await _context.Set<MeetingAction>().AsNoTracking().Where(item => item.MeetingId == meetingId)
            .Include(item => item.AgendaItem)
            .OrderBy(item => item.DueDate == null).ThenBy(item => item.DueDate).ThenBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var users = await _userLookup.GetManyAsync(items.Where(item => item.AssigneeId.HasValue).Select(item => item.AssigneeId!.Value), cancellationToken);
        var workItems = IsCompleted(meeting) ? await GetVisibleLinkedWorkItemsAsync(meeting, items, cancellationToken) : new Dictionary<Guid, WorkItemReference>();
        return Result<IReadOnlyList<MeetingActionItemDto>>.Success(items.Select(item => ToActionItem(item, users, item.AgendaItem?.Topic, GetLinkedWorkItemTitle(item, workItems), IsCompleted(meeting))).ToArray());
    }

    public async Task<Result<MeetingActionItemDto>> CreateActionItemAsync(Guid meetingId, CreateMeetingActionItemRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingActionItemDto>.Failure(Error.NotFound("Meeting not found"));
        if (!CanAccessActionItems(meeting)) return Result<MeetingActionItemDto>.Failure(Error.Conflict("Action items are available after the meeting starts"));
        var validation = await ValidateActionItemAsync(request.Title, request.Description, request.AssigneeId, cancellationToken);
        if (validation is not null) return Result<MeetingActionItemDto>.Failure(validation);
        var agendaValidation = await ValidateActionAgendaAsync(meetingId, request.AgendaItemId, cancellationToken);
        if (agendaValidation is not null) return Result<MeetingActionItemDto>.Failure(agendaValidation);
        var item = new MeetingAction
        {
            TenantId = meeting.TenantId,
            MeetingId = meetingId,
            Action = request.Title.Trim(),
            Description = Normalize(request.Description),
            AssigneeId = request.AssigneeId,
            DueDate = request.DueDate,
            AgendaItemId = request.AgendaItemId,
            Status = MeetingActionStatuses.Open,
            CreatedBy = userId
        };
        _context.Set<MeetingAction>().Add(item);
        TouchMeeting(meeting, userId);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<MeetingActionItemDto>.Failure(Error.Conflict("The meeting was changed by another user")); }
        await AuditAsync(meeting, "ActionItemAdded", userId, new { item.Id, item.Action, item.AssigneeId, item.DueDate }, cancellationToken);
        return Result<MeetingActionItemDto>.Success(await MapActionItemAsync(meeting, item, cancellationToken));
    }

    public async Task<Result<MeetingActionItemDto>> UpdateActionItemAsync(Guid meetingId, Guid itemId, UpdateMeetingActionItemRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingActionItemDto>.Failure(Error.NotFound("Meeting not found"));
        if (!CanAccessActionItems(meeting)) return Result<MeetingActionItemDto>.Failure(Error.Conflict("Action items are available after the meeting starts"));
        var item = await _context.Set<MeetingAction>().FirstOrDefaultAsync(candidate => candidate.MeetingId == meetingId && candidate.Id == itemId, cancellationToken);
        if (item is null) return Result<MeetingActionItemDto>.Failure(Error.NotFound("Action item not found"));
        var validation = await ValidateActionItemAsync(request.Title, request.Description, request.AssigneeId, cancellationToken);
        if (validation is not null) return Result<MeetingActionItemDto>.Failure(validation);
        var agendaValidation = await ValidateActionAgendaAsync(meetingId, request.AgendaItemId, cancellationToken);
        if (agendaValidation is not null) return Result<MeetingActionItemDto>.Failure(agendaValidation);
        var status = MeetingActionStatuses.Canonicalize(request.Status);
        if (status is null) return Result<MeetingActionItemDto>.Failure(Error.Validation("Invalid action item status"));
        SetOriginalRowVersion(item, request.RowVersion);
        item.Action = request.Title.Trim();
        item.Description = Normalize(request.Description);
        item.AssigneeId = request.AssigneeId;
        item.DueDate = request.DueDate;
        item.AgendaItemId = request.AgendaItemId;
        item.Status = status;
        item.CompletedDate = status == MeetingActionStatuses.Completed ? item.CompletedDate ?? DateTime.UtcNow : null;
        item.UpdatedBy = userId;
        TouchMeeting(meeting, userId);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<MeetingActionItemDto>.Failure(Error.Conflict("The action item was changed by another user")); }
        await AuditAsync(meeting, "ActionItemUpdated", userId, new { item.Id, item.Action, item.AssigneeId, item.DueDate, item.Status }, cancellationToken);
        return Result<MeetingActionItemDto>.Success(await MapActionItemAsync(meeting, item, cancellationToken));
    }

    public async Task<Result<MeetingActionItemDto>> LinkActionWorkItemAsync(Guid meetingId, Guid itemId, LinkMeetingActionWorkItemRequest request, string userId, CancellationToken cancellationToken = default)
    {
        if (request.WorkItemId == Guid.Empty) return Result<MeetingActionItemDto>.Failure(Error.Validation("A valid Work Item is required"));
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingActionItemDto>.Failure(Error.NotFound("Meeting not found"));
        if (!IsCompleted(meeting)) return Result<MeetingActionItemDto>.Failure(Error.Conflict("Work Items can only be linked after the meeting is completed"));
        var action = await _context.Set<MeetingAction>().FirstOrDefaultAsync(candidate => candidate.MeetingId == meetingId && candidate.Id == itemId, cancellationToken);
        if (action is null) return Result<MeetingActionItemDto>.Failure(Error.NotFound("Action item not found"));
        if (_workItemLookup is null) return Result<MeetingActionItemDto>.Failure(Error.NotFound("Linkable Work Item not found"));
        var workItem = await _workItemLookup.FindAsync(request.WorkItemId, cancellationToken);
        if (workItem is null
            || workItem.TenantId != meeting.TenantId
            || !meeting.ProjectId.HasValue
            || workItem.ProjectId != meeting.ProjectId
            || !IsProjectInScope(meeting.ProjectId.Value))
            return Result<MeetingActionItemDto>.Failure(Error.NotFound("Linkable Work Item not found"));
        if (action.WorkItemId == workItem.Id)
            return Result<MeetingActionItemDto>.Success(await MapActionItemAsync(meeting, action, cancellationToken));
        if (action.WorkItemId.HasValue)
            return Result<MeetingActionItemDto>.Failure(Error.Conflict("Unlink the current Work Item before linking another one"));

        action.WorkItemId = workItem.Id;
        action.UpdatedBy = userId;
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<MeetingActionItemDto>.Failure(Error.Conflict("The action item was changed by another user")); }
        await AuditAsync(meeting, "ActionItemWorkItemLinked", userId, new { ActionItemId = action.Id, WorkItemId = workItem.Id }, cancellationToken);
        return Result<MeetingActionItemDto>.Success(await MapActionItemAsync(meeting, action, cancellationToken));
    }

    public async Task<Result> UnlinkActionWorkItemAsync(Guid meetingId, Guid itemId, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null) return Result.Failure(Error.NotFound("Meeting not found"));
        if (!IsCompleted(meeting)) return Result.Failure(Error.Conflict("Work Items can only be unlinked after the meeting is completed"));
        var action = await _context.Set<MeetingAction>().FirstOrDefaultAsync(candidate => candidate.MeetingId == meetingId && candidate.Id == itemId, cancellationToken);
        if (action is null) return Result.Failure(Error.NotFound("Action item not found"));
        if (!action.WorkItemId.HasValue) return Result.Success();

        var workItemId = action.WorkItemId.Value;
        action.WorkItemId = null;
        action.UpdatedBy = userId;
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(Error.Conflict("The action item was changed by another user")); }
        await AuditAsync(meeting, "ActionItemWorkItemUnlinked", userId, new { ActionItemId = action.Id, WorkItemId = workItemId }, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteActionItemAsync(Guid meetingId, Guid itemId, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await FindMeetingAsync(meetingId, cancellationToken);
        if (meeting is null) return Result.Failure(Error.NotFound("Meeting not found"));
        if (!CanAccessActionItems(meeting)) return Result.Failure(Error.Conflict("Action items are available after the meeting starts"));
        var item = await _context.Set<MeetingAction>().FirstOrDefaultAsync(candidate => candidate.MeetingId == meetingId && candidate.Id == itemId, cancellationToken);
        if (item is null) return Result.Failure(Error.NotFound("Action item not found"));
        item.IsDeleted = true;
        item.UpdatedBy = userId;
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(Error.Conflict("The action item was changed by another user")); }
        await AuditAsync(meeting, "ActionItemRemoved", userId, new { item.Id, item.Action }, cancellationToken);
        return Result.Success();
    }

    private async Task<Result<MeetingParticipantStatusDto>> SaveInvitationResponseAsync(Meeting meeting, MeetingParticipant participant, string status, byte[]? rowVersion, string userId, CancellationToken cancellationToken)
    {
        SetOriginalRowVersion(participant, rowVersion);
        participant.InvitationResponseStatus = status;
        participant.RespondedAt = DateTime.UtcNow;
        return await SaveParticipantAsync(meeting, participant, "InvitationResponseRecorded", new { participant.Id, InvitationResponseStatus = status }, userId, cancellationToken);
    }

    private async Task<Result<MeetingParticipantStatusDto>> SaveActualAttendanceAsync(Meeting meeting, MeetingParticipant participant, string status, byte[]? rowVersion, string userId, CancellationToken cancellationToken)
    {
        SetOriginalRowVersion(participant, rowVersion);
        participant.AttendanceStatus = status;
        return await SaveParticipantAsync(meeting, participant, "AttendanceRecorded", new { participant.Id, ActualAttendanceStatus = status }, userId, cancellationToken);
    }

    private async Task<Result<MeetingParticipantStatusDto>> SaveParticipantAsync(Meeting meeting, MeetingParticipant participant, string auditAction, object changes, string userId, CancellationToken cancellationToken)
    {
        participant.UpdatedBy = userId;
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<MeetingParticipantStatusDto>.Failure(Error.Conflict("Attendance was changed by another user")); }
        await AuditAsync(meeting, auditAction, userId, changes, cancellationToken);
        var users = participant.UserId.HasValue ? await _userLookup.GetManyAsync([participant.UserId.Value], cancellationToken) : new Dictionary<Guid, UserReference>();
        return Result<MeetingParticipantStatusDto>.Success(ToParticipant(participant, users));
    }

    private async Task<Error?> ValidateAgendaItemAsync(string? topic, string? description, int? durationMinutes, Guid? ownerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(topic) || topic.Trim().Length > 500) return Error.Validation("Agenda topic is required and cannot exceed 500 characters");
        if (description?.Length > 4000) return Error.Validation("Agenda description cannot exceed 4000 characters");
        if (durationMinutes is <= 0 or > 1440) return Error.Validation("Agenda duration must be between 1 and 1440 minutes");
        if (ownerId.HasValue && await _userLookup.FindAsync(ownerId.Value, cancellationToken) is null) return Error.NotFound("Active agenda owner not found");
        return null;
    }

    private async Task<Error?> ValidateActionItemAsync(string? title, string? description, Guid? assigneeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 500) return Error.Validation("Action item title is required and cannot exceed 500 characters");
        if (description?.Length > 4000) return Error.Validation("Action item description cannot exceed 4000 characters");
        if (assigneeId.HasValue && await _userLookup.FindAsync(assigneeId.Value, cancellationToken) is null) return Error.NotFound("Active action item assignee not found");
        return null;
    }

    private async Task<Error?> ValidateActionAgendaAsync(Guid meetingId, Guid? agendaItemId, CancellationToken cancellationToken)
    {
        if (!agendaItemId.HasValue) return null;
        return await _context.Set<MeetingAgenda>().AnyAsync(item => item.Id == agendaItemId.Value && item.MeetingId == meetingId, cancellationToken)
            ? null
            : Error.NotFound("Agenda item not found in this meeting");
    }

    private static bool CanEditAgenda(Meeting meeting) => (meeting.Status ?? MeetingStatuses.Scheduled) == MeetingStatuses.Scheduled;

    private static bool IsCompleted(Meeting meeting) => (meeting.Status ?? MeetingStatuses.Scheduled) == MeetingStatuses.Completed;

    private static bool CanAccessAttendance(Meeting meeting) => (meeting.Status ?? MeetingStatuses.Scheduled) is MeetingStatuses.InProgress or MeetingStatuses.Completed;

    private static bool CanAccessActionItems(Meeting meeting) => CanAccessAttendance(meeting);

    private bool IsProjectInScope(Guid projectId)
    {
        var context = _managementContext.Current;
        return context is null || context.Scope.ProjectIds.Contains(projectId);
    }

    private void TouchMeeting(Meeting meeting, string userId)
    {
        meeting.UpdatedBy = userId;
        meeting.UpdatedAt = DateTime.UtcNow;
        _context.Entry(meeting).Property(item => item.UpdatedAt).IsModified = true;
    }

    private async Task<Meeting?> FindMeetingAsync(Guid meetingId, CancellationToken cancellationToken) =>
        await InScope(_context.Set<Meeting>()).FirstOrDefaultAsync(item => item.Id == meetingId, cancellationToken);

    private IQueryable<Meeting> InScope(IQueryable<Meeting> query)
    {
        var context = _managementContext.Current;
        if (context is null) return query;
        var userId = context.UserId;
        var projectIds = context.Scope.ProjectIds;
        return query.Where(meeting =>
            (meeting.ProjectId.HasValue && projectIds.Contains(meeting.ProjectId.Value))
            || meeting.OrganizerId == userId
            || meeting.Participants.Any(participant => participant.UserId == userId));
    }

    private async Task<MeetingAgendaItemDto> MapAgendaItemAsync(MeetingAgenda item, CancellationToken cancellationToken)
    {
        var users = item.OwnerId.HasValue ? await _userLookup.GetManyAsync([item.OwnerId.Value], cancellationToken) : new Dictionary<Guid, UserReference>();
        return ToAgendaItem(item, users);
    }

    private async Task<MeetingActionItemDto> MapActionItemAsync(Meeting meeting, MeetingAction item, CancellationToken cancellationToken)
    {
        var users = item.AssigneeId.HasValue ? await _userLookup.GetManyAsync([item.AssigneeId.Value], cancellationToken) : new Dictionary<Guid, UserReference>();
        var agendaTopic = item.AgendaItemId.HasValue
            ? await _context.Set<MeetingAgenda>().AsNoTracking().Where(agenda => agenda.Id == item.AgendaItemId.Value && agenda.MeetingId == item.MeetingId).Select(agenda => agenda.Topic).FirstOrDefaultAsync(cancellationToken)
            : null;
        var workItems = IsCompleted(meeting)
            ? await GetVisibleLinkedWorkItemsAsync(meeting, [item], cancellationToken)
            : new Dictionary<Guid, WorkItemReference>();
        return ToActionItem(item, users, agendaTopic, GetLinkedWorkItemTitle(item, workItems), IsCompleted(meeting));
    }

    private async Task<IReadOnlyDictionary<Guid, WorkItemReference>> GetVisibleLinkedWorkItemsAsync(Meeting meeting, IEnumerable<MeetingAction> actions, CancellationToken cancellationToken)
    {
        if (_workItemLookup is null
            || !CanViewLinkedWorkItemDetails()
            || !meeting.ProjectId.HasValue
            || !IsProjectInScope(meeting.ProjectId.Value))
            return new Dictionary<Guid, WorkItemReference>();

        var references = await _workItemLookup.GetManyAsync(actions.Where(item => item.WorkItemId.HasValue).Select(item => item.WorkItemId!.Value), cancellationToken);
        return references.Values
            .Where(item => item.TenantId == meeting.TenantId && item.ProjectId == meeting.ProjectId)
            .ToDictionary(item => item.Id);
    }

    private bool CanViewLinkedWorkItemDetails() => _httpContextAccessor.HttpContext?.User.HasClaim("permission", "WorkItem.View") == true;

    private static string? GetLinkedWorkItemTitle(MeetingAction action, IReadOnlyDictionary<Guid, WorkItemReference> workItems) =>
        action.WorkItemId.HasValue && workItems.TryGetValue(action.WorkItemId.Value, out var workItem) ? workItem.Title : null;

    private static MeetingAgendaItemDto ToAgendaItem(MeetingAgenda item, IReadOnlyDictionary<Guid, UserReference> users) => new()
    {
        Id = item.Id,
        MeetingId = item.MeetingId,
        Topic = item.Topic,
        Description = item.Description,
        SortOrder = item.SortOrder,
        DurationMinutes = item.DurationMinutes,
        OwnerId = item.OwnerId,
        OwnerName = item.OwnerId.HasValue && users.TryGetValue(item.OwnerId.Value, out var owner) ? owner.FullName : null,
        RowVersion = item.RowVersion
    };

    private static MeetingParticipantStatusDto ToParticipant(MeetingParticipant item, IReadOnlyDictionary<Guid, UserReference> users)
    {
        var user = item.UserId.HasValue && users.TryGetValue(item.UserId.Value, out var reference) ? reference : null;
        var invitationResponseStatus = MeetingInvitationResponseStatuses.Canonicalize(item.InvitationResponseStatus);
        var actualAttendanceStatus = MeetingAttendanceStatuses.Canonicalize(item.AttendanceStatus);
        return new MeetingParticipantStatusDto
        {
            Id = item.Id,
            MeetingId = item.MeetingId,
            UserId = item.UserId,
            DisplayName = user?.FullName ?? item.Name,
            Email = user?.Email ?? item.Email,
            IsInternalUser = item.UserId.HasValue,
            AttendanceStatus = GetLegacyAttendanceStatus(invitationResponseStatus, actualAttendanceStatus),
            InvitationResponseStatus = invitationResponseStatus,
            ActualAttendanceStatus = actualAttendanceStatus,
            Role = item.Role,
            RespondedAt = item.RespondedAt,
            RowVersion = item.RowVersion
        };
    }

    private static string GetLegacyAttendanceStatus(string? invitationResponseStatus, string? actualAttendanceStatus) => actualAttendanceStatus switch
    {
        MeetingAttendanceStatuses.Present => "Attended",
        MeetingAttendanceStatuses.Late => MeetingAttendanceStatuses.Late,
        MeetingAttendanceStatuses.Excused => MeetingAttendanceStatuses.Excused,
        MeetingAttendanceStatuses.Absent => MeetingAttendanceStatuses.Absent,
        _ => invitationResponseStatus ?? MeetingInvitationResponseStatuses.Pending
    };

    private static MeetingDecisionDto ToDecision(MeetingMinutes? minutes) => new()
    {
        Decisions = minutes?.Decisions,
        IsEditable = minutes?.State != MeetingMinutesStates.Final,
        RowVersion = minutes?.RowVersion ?? Array.Empty<byte>()
    };

    private static MeetingActionItemDto ToActionItem(MeetingAction item, IReadOnlyDictionary<Guid, UserReference> users, string? agendaItemTopic = null, string? workItemTitle = null, bool includeWorkItem = true) => new()
    {
        Id = item.Id,
        MeetingId = item.MeetingId,
        Title = item.Action,
        Description = item.Description,
        AssigneeId = item.AssigneeId,
        AssigneeName = item.AssigneeId.HasValue && users.TryGetValue(item.AssigneeId.Value, out var assignee) ? assignee.FullName : null,
        DueDate = item.DueDate,
        CompletedDate = item.CompletedDate,
        Status = MeetingActionStatuses.Canonicalize(item.Status) ?? MeetingActionStatuses.Open,
        WorkItemId = includeWorkItem ? item.WorkItemId : null,
        WorkItemTitle = includeWorkItem ? workItemTitle : null,
        AgendaItemId = item.AgendaItemId,
        AgendaItemTopic = agendaItemTopic,
        RowVersion = item.RowVersion
    };

    private async Task AuditAsync(Meeting meeting, string action, string userId, object changes, CancellationToken cancellationToken)
    {
        await _auditService.LogAsync(new AuditEntry
        {
            TenantId = meeting.TenantId,
            EntityType = nameof(Meeting),
            EntityId = meeting.Id,
            Action = action,
            Changes = JsonSerializer.Serialize(changes),
            UserId = Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : null,
            UserName = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);
    }

    private void SetOriginalRowVersion<TEntity>(TEntity entity, byte[]? rowVersion) where TEntity : class
    {
        if (rowVersion is { Length: > 0 }) _context.Entry(entity).Property("RowVersion").OriginalValue = rowVersion;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
