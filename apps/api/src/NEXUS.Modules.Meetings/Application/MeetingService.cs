using System.Security.Claims;
using System.Security.Cryptography;
using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Meetings.Application.DTOs;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.Meetings.Application;

public sealed class MeetingService : IMeetingService
{
    private const int MaxCalendarEvents = 2000;
    private static readonly TimeSpan MaxCalendarRange = TimeSpan.FromDays(366);
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IProjectReferenceLookup _projectLookup;
    private readonly IUserReferenceLookup _userLookup;
    private readonly IWorkItemReferenceLookup _workItemLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMeetingMinutesAttachmentStorage _attachmentStorage;
    private readonly IMeetingMinutesAttachmentPolicy _attachmentPolicy;
    private readonly IManagementContextAccessor _managementContext;
    private readonly IMeetingNotificationPublisher _meetingNotifications;

    public MeetingService(
        DbContext context,
        IAuditService auditService,
        IProjectReferenceLookup projectLookup,
        IUserReferenceLookup userLookup,
        IWorkItemReferenceLookup workItemLookup,
        IHttpContextAccessor httpContextAccessor,
        IMeetingMinutesAttachmentStorage attachmentStorage,
        IMeetingMinutesAttachmentPolicy attachmentPolicy,
        IManagementContextAccessor managementContext,
        IMeetingNotificationPublisher meetingNotifications)
    {
        _context = context;
        _auditService = auditService;
        _projectLookup = projectLookup;
        _userLookup = userLookup;
        _workItemLookup = workItemLookup;
        _httpContextAccessor = httpContextAccessor;
        _attachmentStorage = attachmentStorage;
        _attachmentPolicy = attachmentPolicy;
        _managementContext = managementContext;
        _meetingNotifications = meetingNotifications;
    }

    public async Task<PagedResult<MeetingListDto>> GetPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        Guid? projectId = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        IReadOnlyList<Guid>? projectIds = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = ApplyFilters(InScope(_context.Set<Meeting>().AsNoTracking()), search, status, projectId, projectIds);
        if (dateFrom.HasValue) query = query.Where(meeting => meeting.StartTime >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(meeting => meeting.StartTime <= dateTo.Value);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(meeting => meeting.StartTime)
            .ThenByDescending(meeting => meeting.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(meeting => new MeetingListRow(
                meeting.Id,
                meeting.PublicId,
                meeting.Title,
                meeting.ProjectId,
                meeting.OrganizerId,
                meeting.StartTime,
                meeting.EndTime,
                meeting.ActualStartTime,
                meeting.ActualEndTime,
                meeting.Status,
                meeting.ReminderOffsetMinutes,
                meeting.Participants.Count,
                meeting.Status == MeetingStatuses.Completed
                    ? meeting.Minutes.OrderByDescending(minutes => minutes.CreatedAt).Select(minutes => minutes.State).FirstOrDefault()
                    : null,
                meeting.Recurrence != null && meeting.Recurrence.StartsWith(MeetingRecurrenceMarkerCodec.Prefix)))
            .ToListAsync(cancellationToken);

        return new PagedResult<MeetingListDto>(await ToListDtosAsync(rows, cancellationToken), page, pageSize, total);
    }

    public async Task<Result<MeetingCalendarDto>> GetCalendarAsync(
        DateTime from,
        DateTime to,
        IReadOnlyList<Guid>? projectIds = null,
        string? status = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        from = AsUtc(from);
        to = AsUtc(to);
        if (to <= from) return Result<MeetingCalendarDto>.Failure(Error.Validation("The calendar range end must be after the range start."));
        if (to - from > MaxCalendarRange) return Result<MeetingCalendarDto>.Failure(Error.Validation("The calendar range cannot exceed 366 days."));

        var query = ApplyFilters(InScope(_context.Set<Meeting>().AsNoTracking()), search, status, null, projectIds)
            .Where(meeting => meeting.StartTime < to && meeting.EndTime > from);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(meeting => meeting.StartTime)
            .ThenBy(meeting => meeting.Title)
            .Take(MaxCalendarEvents + 1)
            .Select(meeting => new MeetingListRow(
                meeting.Id,
                meeting.PublicId,
                meeting.Title,
                meeting.ProjectId,
                meeting.OrganizerId,
                meeting.StartTime,
                meeting.EndTime,
                meeting.ActualStartTime,
                meeting.ActualEndTime,
                meeting.Status,
                meeting.ReminderOffsetMinutes,
                meeting.Participants.Count,
                meeting.Status == MeetingStatuses.Completed
                    ? meeting.Minutes.OrderByDescending(minutes => minutes.CreatedAt).Select(minutes => minutes.State).FirstOrDefault()
                    : null,
                meeting.Recurrence != null && meeting.Recurrence.StartsWith(MeetingRecurrenceMarkerCodec.Prefix)))
            .ToListAsync(cancellationToken);

        var isTruncated = rows.Count > MaxCalendarEvents;
        if (isTruncated) rows = rows.Take(MaxCalendarEvents).ToList();

        return Result<MeetingCalendarDto>.Success(new MeetingCalendarDto
        {
            From = from,
            To = to,
            TotalCount = total,
            IsTruncated = isTruncated,
            Items = await ToListDtosAsync(rows, cancellationToken)
        });
    }

    public async Task<IReadOnlyList<MeetingProjectOptionDto>> GetAccessibleProjectsAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        var term = search?.Trim();
        var projects = await _projectLookup.GetFinancialReferencesAsync(cancellationToken);
        return projects
            .Where(project => string.IsNullOrEmpty(term)
                || project.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (project.Code?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .Select(project => new MeetingProjectOptionDto
            {
                Id = project.Id,
                PublicId = project.PublicId,
                Name = project.Name,
                Code = project.Code
            })
            .ToList();
    }

    public async Task<ProjectMeetingOverviewDto> GetProjectOverviewAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var query = InScope(_context.Set<Meeting>().AsNoTracking()).Where(meeting => meeting.ProjectId == projectId);
        return new ProjectMeetingOverviewDto
        {
            MeetingCount = await query.CountAsync(cancellationToken),
            FinalMinutesCount = await query.CountAsync(meeting => meeting.Minutes.Any(minutes => minutes.State == MeetingMinutesStates.Final), cancellationToken)
        };
    }

    public async Task<Result<MeetingDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>().AsNoTracking()).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (meeting is null) return Result<MeetingDetailDto>.Failure(Error.NotFound("Meeting not found"));

        return await BuildDetailAsync(meeting, cancellationToken);
    }

    public async Task<Result<MeetingDetailDto>> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>().AsNoTracking())
            .FirstOrDefaultAsync(item => item.PublicId == publicId, cancellationToken);
        if (meeting is null) return Result<MeetingDetailDto>.Failure(Error.NotFound("Meeting not found"));

        return await BuildDetailAsync(meeting, cancellationToken);
    }

    private async Task<Result<MeetingDetailDto>> BuildDetailAsync(Meeting meeting, CancellationToken cancellationToken)
    {
        var participants = await _context.Set<MeetingParticipant>().AsNoTracking()
            .Where(item => item.MeetingId == meeting.Id)
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var userIds = participants.Where(item => item.UserId.HasValue).Select(item => item.UserId.GetValueOrDefault()).Append(meeting.OrganizerId ?? Guid.Empty);
        var users = await _userLookup.GetManyAsync(userIds, cancellationToken);
        var project = meeting.ProjectId.HasValue ? await _projectLookup.FindAsync(meeting.ProjectId.Value, cancellationToken) : null;
        var minutesState = (meeting.Status ?? MeetingStatuses.Scheduled) == MeetingStatuses.Completed
            ? await _context.Set<MeetingMinutes>().AsNoTracking()
                .Where(item => item.MeetingId == meeting.Id)
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => item.State)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var isRecurring = MeetingRecurrenceMarkerCodec.TryDecode(meeting.Recurrence, out var recurrenceMarker);
        return Result<MeetingDetailDto>.Success(new MeetingDetailDto
        {
            Id = meeting.Id,
            PublicId = meeting.PublicId,
            Title = meeting.Title,
            Objective = meeting.Description,
            ProjectId = meeting.ProjectId,
            ProjectPublicId = project?.PublicId,
            ProjectName = project?.Name,
            OrganizerId = meeting.OrganizerId,
            OrganizerName = meeting.OrganizerId.HasValue && users.TryGetValue(meeting.OrganizerId.Value, out var organizer) ? organizer.FullName : null,
            StartTime = AsUtc(meeting.StartTime),
            EndTime = AsUtc(meeting.EndTime),
            ActualStartTime = AsUtc(meeting.ActualStartTime),
            ActualEndTime = AsUtc(meeting.ActualEndTime),
            Location = meeting.Location,
            MeetingLink = meeting.MeetingLink,
            Status = meeting.Status,
            ReminderOffsetMinutes = meeting.ReminderOffsetMinutes,
            Attendees = participants.Select(item => ToAttendee(item, users)).ToList(),
            MinutesState = minutesState,
            RowVersion = meeting.RowVersion,
            IsRecurring = isRecurring,
            SeriesId = recurrenceMarker?.SeriesId,
            OccurrenceKey = recurrenceMarker?.OccurrenceKey
        });
    }

    public async Task<Result<MeetingDetailDto>> CreateAsync(CreateMeetingRequest request, string userId, CancellationToken cancellationToken = default)
    {
        if (request.Status is not null && CanonicalStatus(request.Status) != MeetingStatuses.Scheduled)
            return Result<MeetingDetailDto>.Failure(Error.Validation("New meetings must start in the Scheduled status"));
        var validation = await ValidateMeetingAsync(request, userId, cancellationToken);
        if (validation is not null) return Result<MeetingDetailDto>.Failure(validation);

        var agendaInputs = request.AgendaItems ?? Array.Empty<CreateMeetingAgendaItemRequest>();
        foreach (var agendaInput in agendaInputs)
        {
            if (string.IsNullOrWhiteSpace(agendaInput.Topic) || agendaInput.Topic.Trim().Length > 500)
                return Result<MeetingDetailDto>.Failure(Error.Validation("Agenda topic is required and cannot exceed 500 characters"));
            if (agendaInput.Description?.Length > 4000)
                return Result<MeetingDetailDto>.Failure(Error.Validation("Agenda description cannot exceed 4000 characters"));
            if (agendaInput.DurationMinutes is <= 0 or > 1440)
                return Result<MeetingDetailDto>.Failure(Error.Validation("Agenda duration must be between 1 and 1440 minutes"));
            if (agendaInput.OwnerId.HasValue && await _userLookup.FindAsync(agendaInput.OwnerId.Value, cancellationToken) is null)
                return Result<MeetingDetailDto>.Failure(Error.NotFound("Active agenda owner not found"));
        }

        var organizerId = request.OrganizerId ?? ParseUserId(userId);
        var attendees = await ResolveAttendeesAsync(request.Attendees, cancellationToken);
        if (!attendees.IsSuccess || attendees.Value is null) return Result<MeetingDetailDto>.Failure(attendees.Error ?? Error.Validation("Meeting attendees are invalid"));
        var meeting = new Meeting
        {
            TenantId = GetCurrentTenantId(),
            Title = request.Title.Trim(),
            Description = Normalize(request.Objective),
            ProjectId = request.ProjectId,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Location = Normalize(request.Location),
            MeetingLink = MeetingLinkPolicy.Normalize(request.MeetingLink),
            OrganizerId = organizerId,
            Status = CanonicalStatus(request.Status) ?? MeetingStatuses.Scheduled,
            ReminderOffsetMinutes = request.ReminderOffsetMinutes,
            CreatedBy = userId
        };
        var sortOrder = 0;
        foreach (var agendaInput in agendaInputs)
        {
            meeting.AgendaItems.Add(new MeetingAgenda
            {
                TenantId = meeting.TenantId,
                Topic = agendaInput.Topic.Trim(),
                Description = Normalize(agendaInput.Description),
                DurationMinutes = agendaInput.DurationMinutes,
                OwnerId = agendaInput.OwnerId,
                SortOrder = sortOrder++,
                CreatedBy = userId
            });
        }
        var newInvitations = await SynchronizeAttendeesAsync(meeting, attendees.Value, userId, cancellationToken);

        await using (var transaction = await BeginNotificationTransactionAsync(cancellationToken))
        {
            _context.Set<Meeting>().Add(meeting);
            await _context.SaveChangesAsync(cancellationToken);
            var invitationResult = await QueueInvitationsAsync(meeting, newInvitations, ParseOptionalUserId(userId), cancellationToken);
            if (invitationResult.IsFailure)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                return Result<MeetingDetailDto>.Failure(invitationResult.Error ?? Error.Validation("Meeting invitations could not be queued."));
            }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        await AuditAsync(meeting, "Created", userId, new { meeting.Title, meeting.ProjectId, meeting.Status }, cancellationToken);
        return await GetByIdAsync(meeting.Id, cancellationToken);
    }

    public async Task<Result<MeetingDetailDto>> CreateSeriesAsync(CreateMeetingSeriesRequest request, string userId, CancellationToken cancellationToken = default)
    {
        if (request.RequestId == Guid.Empty)
            return Result<MeetingDetailDto>.Failure(Error.Validation("A request ID is required for safe recurring-series retries."));
        var schedule = MeetingRecurrenceSchedule.Build(request.Rule);
        if (schedule.IsFailure) return Result<MeetingDetailDto>.Failure(schedule.Error!);
        var first = schedule.Value!.Occurrences[0];
        var input = request.Template;
        var template = new CreateMeetingRequest
        {
            Title = input.Title,
            Objective = input.Objective,
            ProjectId = input.ProjectId,
            StartTime = first.StartTime,
            EndTime = first.EndTime,
            Location = input.Location,
            MeetingLink = input.MeetingLink,
            OrganizerId = input.OrganizerId,
            Attendees = input.Attendees,
            AgendaItems = input.AgendaItems,
            Status = input.Status,
            ReminderOffsetMinutes = input.ReminderOffsetMinutes,
        };
        if (template.Status is not null && CanonicalStatus(template.Status) != MeetingStatuses.Scheduled)
            return Result<MeetingDetailDto>.Failure(Error.Validation("Recurring meetings must start in the Scheduled status."));
        var validation = await ValidateMeetingAsync(template, userId, cancellationToken);
        if (validation is not null) return Result<MeetingDetailDto>.Failure(validation);

        var agendaInputs = template.AgendaItems ?? Array.Empty<CreateMeetingAgendaItemRequest>();
        foreach (var agendaInput in agendaInputs)
        {
            if (string.IsNullOrWhiteSpace(agendaInput.Topic) || agendaInput.Topic.Trim().Length > 500)
                return Result<MeetingDetailDto>.Failure(Error.Validation("Agenda topic is required and cannot exceed 500 characters."));
            if (agendaInput.Description?.Length > 4000 || agendaInput.DurationMinutes is <= 0 or > 1440)
                return Result<MeetingDetailDto>.Failure(Error.Validation("Agenda details or duration are invalid."));
            if (agendaInput.OwnerId.HasValue && await _userLookup.FindAsync(agendaInput.OwnerId.Value, cancellationToken) is null)
                return Result<MeetingDetailDto>.Failure(Error.NotFound("Active agenda owner not found."));
        }
        var attendeesResult = await ResolveAttendeesAsync(template.Attendees, cancellationToken);
        if (!attendeesResult.IsSuccess || attendeesResult.Value is null)
            return Result<MeetingDetailDto>.Failure(attendeesResult.Error ?? Error.Validation("Meeting attendees are invalid."));

        var tenantId = GetCurrentTenantId();
        var seriesId = MeetingRecurrenceMarkerCodec.DeriveSeriesId(tenantId, request.RequestId);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { request.Template, request.Rule }, new JsonSerializerOptions(JsonSerializerDefaults.Web)))));
        var existing = await _context.Set<Meeting>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == seriesId && item.TenantId == tenantId, cancellationToken);
        if (existing is not null)
        {
            if (MeetingRecurrenceMarkerCodec.TryDecode(existing.Recurrence, out var marker)
                && marker!.IsSeriesAnchor && marker.RequestId == request.RequestId
                && string.Equals(marker.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                return await GetByIdAsync(existing.Id, cancellationToken);
            return Result<MeetingDetailDto>.Failure(Error.Conflict("This recurring-series request ID has already been used."));
        }

        var organizerId = template.OrganizerId ?? ParseUserId(userId);
        var meetings = new List<Meeting>(schedule.Value.TotalOccurrenceCount);
        var occurrenceInvitations = new List<(Meeting Meeting, IReadOnlyList<MeetingParticipant> Participants)>();
        foreach (var occurrence in schedule.Value.Occurrences)
        {
            var isAnchor = meetings.Count == 0;
            var meetingId = isAnchor ? seriesId : MeetingRecurrenceMarkerCodec.DeriveOccurrenceId(seriesId, occurrence.OccurrenceKey);
            var meeting = new Meeting
            {
                Id = meetingId,
                TenantId = tenantId,
                Title = template.Title.Trim(),
                Description = Normalize(template.Objective),
                ProjectId = template.ProjectId,
                StartTime = occurrence.StartTime,
                EndTime = occurrence.EndTime,
                Location = Normalize(template.Location),
                MeetingLink = MeetingLinkPolicy.Normalize(template.MeetingLink),
                OrganizerId = organizerId,
                Status = MeetingStatuses.Scheduled,
                ReminderOffsetMinutes = template.ReminderOffsetMinutes,
                Recurrence = MeetingRecurrenceMarkerCodec.Encode(new MeetingRecurrenceMarker
                {
                    SeriesId = seriesId,
                    OccurrenceKey = occurrence.OccurrenceKey,
                    IsSeriesAnchor = isAnchor,
                    Rule = isAnchor ? request.Rule : null,
                    RequestId = isAnchor ? request.RequestId : null,
                    RequestFingerprint = isAnchor ? fingerprint : null,
                }),
                CreatedBy = userId,
            };

            var agendaOrder = 0;
            foreach (var agendaInput in agendaInputs)
                meeting.AgendaItems.Add(new MeetingAgenda
                {
                    TenantId = tenantId,
                    Topic = agendaInput.Topic.Trim(),
                    Description = Normalize(agendaInput.Description),
                    DurationMinutes = agendaInput.DurationMinutes,
                    OwnerId = agendaInput.OwnerId,
                    SortOrder = agendaOrder++,
                    CreatedBy = userId,
                });
            var participants = new List<MeetingParticipant>();
            foreach (var attendee in attendeesResult.Value)
            {
                var participant = new MeetingParticipant
                {
                    TenantId = tenantId,
                    UserId = attendee.UserId,
                    Email = attendee.Email,
                    Name = attendee.DisplayName,
                    InvitationResponseStatus = MeetingInvitationResponseStatuses.Pending,
                    CreatedBy = userId,
                };
                meeting.Participants.Add(participant);
                participants.Add(participant);
            }
            meetings.Add(meeting);
            occurrenceInvitations.Add((meeting, participants));
        }

        _context.Set<Meeting>().AddRange(meetings);
        var occurrenceDataPersisted = false;
        try
        {
            // Persist all occurrences and invitation events atomically, so retries never
            // leave a committed series without its corresponding notification records.
            await using var transaction = await BeginNotificationTransactionAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            occurrenceDataPersisted = true;
            foreach (var occurrence in occurrenceInvitations)
            {
                var invitationResult = await QueueInvitationsAsync(occurrence.Meeting, occurrence.Participants,
                    ParseOptionalUserId(userId), cancellationToken);
                if (invitationResult.IsFailure)
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    return Result<MeetingDetailDto>.Failure(invitationResult.Error ?? Error.Validation("Meeting invitations could not be queued."));
                }
            }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (occurrenceDataPersisted) throw;
            foreach (var entry in _context.ChangeTracker.Entries<MeetingAgenda>().Where(entry => entry.State == EntityState.Added && meetings.Any(meeting => meeting.Id == entry.Entity.MeetingId)).ToArray())
                entry.State = EntityState.Detached;
            foreach (var entry in _context.ChangeTracker.Entries<MeetingParticipant>().Where(entry => entry.State == EntityState.Added && meetings.Any(meeting => meeting.Id == entry.Entity.MeetingId)).ToArray())
                entry.State = EntityState.Detached;
            foreach (var entry in _context.ChangeTracker.Entries<Meeting>().Where(entry => entry.State == EntityState.Added && meetings.Any(meeting => meeting.Id == entry.Entity.Id)).ToArray())
                entry.State = EntityState.Detached;
            existing = await _context.Set<Meeting>().IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == seriesId && item.TenantId == tenantId, cancellationToken);
            if (existing is not null && MeetingRecurrenceMarkerCodec.TryDecode(existing.Recurrence, out var marker)
                && marker!.IsSeriesAnchor && marker.RequestId == request.RequestId
                && string.Equals(marker.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                return await GetByIdAsync(existing.Id, cancellationToken);
            return Result<MeetingDetailDto>.Failure(Error.Conflict("The recurring series could not be created because its request ID conflicts with an existing meeting."));
        }

        await AuditAsync(meetings[0], "RecurringSeriesCreated", userId,
            new { seriesId, occurrenceIds = meetings.Select(meeting => meeting.Id).ToArray(), schedule.Value.TotalOccurrenceCount }, cancellationToken);
        return await GetByIdAsync(meetings[0].Id, cancellationToken);
    }

    public async Task<Result<MeetingSeriesDto>> GetSeriesAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        var current = await InScope(_context.Set<Meeting>().AsNoTracking())
            .FirstOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (current is null || !MeetingRecurrenceMarkerCodec.TryDecode(current.Recurrence, out var currentMarker))
            return Result<MeetingSeriesDto>.Failure(Error.NotFound("Recurring series not found."));

        var seriesId = currentMarker!.SeriesId;
        var anchor = await InScope(_context.Set<Meeting>().AsNoTracking())
            .FirstOrDefaultAsync(item => item.Id == seriesId && item.TenantId == current.TenantId, cancellationToken);
        if (anchor is null || !MeetingRecurrenceMarkerCodec.TryDecode(anchor.Recurrence, out var anchorMarker)
            || !anchorMarker!.IsSeriesAnchor || anchorMarker.Rule is null)
            return Result<MeetingSeriesDto>.Failure(Error.NotFound("Recurring series settings are unavailable."));

        var candidates = await InScope(_context.Set<Meeting>().AsNoTracking())
            .Where(item => item.TenantId == current.TenantId && item.Recurrence != null && item.Recurrence.Contains(seriesId.ToString("D")))
            .ToListAsync(cancellationToken);
        var occurrences = candidates
            .Select(item => (Meeting: item, Valid: MeetingRecurrenceMarkerCodec.TryDecode(item.Recurrence, out var marker), Marker: marker))
            .Where(item => item.Valid && item.Marker!.SeriesId == seriesId)
            .OrderBy(item => item.Marker!.OccurrenceKey, StringComparer.Ordinal)
            .Select(item => new MeetingSeriesOccurrenceDto(item.Meeting.Id, item.Marker!.OccurrenceKey, item.Meeting.Title,
                AsUtc(item.Meeting.StartTime), AsUtc(item.Meeting.EndTime), item.Meeting.Status, item.Marker.IsSeriesAnchor))
            .ToArray();
        return Result<MeetingSeriesDto>.Success(new MeetingSeriesDto(seriesId, anchorMarker.Rule, anchor.Title, occurrences));
    }

    public async Task<Result<MeetingDetailDto>> UpdateAsync(Guid id, UpdateMeetingRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>()).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (meeting is null) return Result<MeetingDetailDto>.Failure(Error.NotFound("Meeting not found"));
        var validation = await ValidateMeetingAsync(request, userId, cancellationToken);
        if (validation is not null) return Result<MeetingDetailDto>.Failure(validation);
        var attendees = await ResolveAttendeesAsync(request.Attendees, cancellationToken);
        if (!attendees.IsSuccess || attendees.Value is null) return Result<MeetingDetailDto>.Failure(attendees.Error ?? Error.Validation("Meeting attendees are invalid"));

        var currentStatus = meeting.Status ?? MeetingStatuses.Scheduled;
        var requestedStatus = CanonicalStatus(request.Status) ?? currentStatus;
        if (currentStatus != MeetingStatuses.Scheduled
            || requestedStatus is not (MeetingStatuses.Scheduled or MeetingStatuses.Cancelled))
        {
            return Result<MeetingDetailDto>.Failure(Error.Conflict("Only scheduled meetings can be edited here. Use the lifecycle actions to start or end a meeting."));
        }
        if (request.RowVersion is { Length: > 0 }) _context.Entry(meeting).Property(item => item.RowVersion).OriginalValue = request.RowVersion;
        var reminderConfigurationChanged = meeting.StartTime != request.StartTime || meeting.ReminderOffsetMinutes != request.ReminderOffsetMinutes || !string.Equals(meeting.Status, requestedStatus, StringComparison.Ordinal);

        meeting.Title = request.Title.Trim();
        meeting.Description = Normalize(request.Objective);
        meeting.ProjectId = request.ProjectId;
        meeting.StartTime = request.StartTime;
        meeting.EndTime = request.EndTime;
        meeting.Location = Normalize(request.Location);
        meeting.MeetingLink = MeetingLinkPolicy.Normalize(request.MeetingLink);
        meeting.OrganizerId = request.OrganizerId ?? meeting.OrganizerId ?? ParseUserId(userId);
        meeting.Status = requestedStatus;
        meeting.ReminderOffsetMinutes = request.ReminderOffsetMinutes;
        meeting.UpdatedBy = userId;
        if (reminderConfigurationChanged) await DeactivatePendingRemindersAsync(meeting.Id, userId, cancellationToken);
        var newInvitations = await SynchronizeAttendeesAsync(meeting, attendees.Value, userId, cancellationToken);
        try
        {
            await using var transaction = await BeginNotificationTransactionAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            var invitationResult = await QueueInvitationsAsync(meeting, newInvitations, ParseOptionalUserId(userId), cancellationToken);
            if (invitationResult.IsFailure)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                return Result<MeetingDetailDto>.Failure(invitationResult.Error ?? Error.Validation("Meeting invitations could not be queued."));
            }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<MeetingDetailDto>.Failure(Error.Conflict("The meeting was changed by another user"));
        }

        await AuditAsync(meeting, requestedStatus == MeetingStatuses.Completed ? "Completed" : requestedStatus == MeetingStatuses.Cancelled ? "Cancelled" : "Updated", userId, new { meeting.Title, meeting.ProjectId, meeting.Status }, cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result<MeetingDetailDto>> StartAsync(Guid id, MeetingLifecycleRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>()).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (meeting is null) return Result<MeetingDetailDto>.Failure(Error.NotFound("Meeting not found"));
        var currentStatus = meeting.Status ?? MeetingStatuses.Scheduled;
        if (currentStatus == MeetingStatuses.InProgress) return await GetByIdAsync(id, cancellationToken);
        if (currentStatus != MeetingStatuses.Scheduled)
            return Result<MeetingDetailDto>.Failure(Error.Conflict("Only a scheduled meeting can be started"));

        if (request.RowVersion is { Length: > 0 }) _context.Entry(meeting).Property(item => item.RowVersion).OriginalValue = request.RowVersion;
        var startedAtUtc = DateTime.UtcNow;
        meeting.Status = MeetingStatuses.InProgress;
        meeting.ActualStartTime = startedAtUtc;
        meeting.ActualEndTime = null;
        meeting.UpdatedBy = userId;
        await DeactivatePendingRemindersAsync(meeting.Id, userId, cancellationToken);
        try
        {
            await using var transaction = await BeginNotificationTransactionAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            var actorId = ParseOptionalUserId(userId);
            if (actorId is null)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                return Result<MeetingDetailDto>.Failure(Error.Unauthorized("The meeting actor could not be resolved."));
            }
            var notificationResult = await _meetingNotifications.QueueStartedAsync(meeting, actorId.Value, cancellationToken);
            if (notificationResult.IsFailure)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                return Result<MeetingDetailDto>.Failure(notificationResult.Error ?? Error.Validation("Meeting start notifications could not be queued."));
            }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return Result<MeetingDetailDto>.Failure(Error.Conflict("The meeting was changed by another user")); }
        await AuditAsync(meeting, "Started", userId, new { meeting.ActualStartTime, ActorId = ParseOptionalUserId(userId) }, cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result<MeetingDetailDto>> EndAsync(Guid id, MeetingLifecycleRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>()).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (meeting is null) return Result<MeetingDetailDto>.Failure(Error.NotFound("Meeting not found"));
        if ((meeting.Status ?? MeetingStatuses.Scheduled) != MeetingStatuses.InProgress || !meeting.ActualStartTime.HasValue)
            return Result<MeetingDetailDto>.Failure(Error.Conflict("Only an in-progress meeting can be ended"));

        if (request.RowVersion is { Length: > 0 }) _context.Entry(meeting).Property(item => item.RowVersion).OriginalValue = request.RowVersion;
        var endedAtUtc = DateTime.UtcNow;
        if (endedAtUtc < meeting.ActualStartTime.Value) endedAtUtc = meeting.ActualStartTime.Value;
        meeting.Status = MeetingStatuses.Completed;
        meeting.ActualEndTime = endedAtUtc;
        meeting.UpdatedBy = userId;
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<MeetingDetailDto>.Failure(Error.Conflict("The meeting was changed by another user")); }
        await AuditAsync(meeting, "Ended", userId, new { meeting.ActualEndTime, ActorId = ParseOptionalUserId(userId) }, cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>()).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (meeting is null) return Result.Failure(Error.NotFound("Meeting not found"));
        meeting.IsDeleted = true;
        meeting.UpdatedBy = userId;
        await DeactivatePendingRemindersAsync(meeting.Id, userId, cancellationToken);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(Error.Conflict("The meeting was changed by another user")); }
        await AuditAsync(meeting, "Deleted", userId, new { meeting.Title }, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<MeetingAttendeeDto>>> GetAttendeesAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        if (!await MeetingExistsAsync(meetingId, cancellationToken)) return Result<IReadOnlyList<MeetingAttendeeDto>>.Failure(Error.NotFound("Meeting not found"));
        var participants = await _context.Set<MeetingParticipant>().AsNoTracking().Where(item => item.MeetingId == meetingId).OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var users = await _userLookup.GetManyAsync(participants.Where(item => item.UserId.HasValue).Select(item => item.UserId.GetValueOrDefault()), cancellationToken);
        return Result<IReadOnlyList<MeetingAttendeeDto>>.Success(participants.Select(item => ToAttendee(item, users)).ToList());
    }

    public async Task<Result<MeetingAttendeeDto>> AddAttendeeAsync(Guid meetingId, MeetingAttendeeInput request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>()).FirstOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingAttendeeDto>.Failure(Error.NotFound("Meeting not found"));
        var resolved = await ResolveAttendeesAsync([request], cancellationToken);
        if (!resolved.IsSuccess || resolved.Value is null) return Result<MeetingAttendeeDto>.Failure(resolved.Error ?? Error.Validation("Meeting attendee is invalid"));
        var target = resolved.Value[0];
        var current = await GetCurrentAttendeesAsync(meeting, cancellationToken);
        if (current.Any(existing => SameAttendee(existing, target))) return Result<MeetingAttendeeDto>.Failure(Error.Conflict("The attendee is already in this meeting"));
        current.Add(target);
        var newInvitations = await SynchronizeAttendeesAsync(meeting, current, userId, cancellationToken);
        try
        {
            await using var transaction = await BeginNotificationTransactionAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            var invitationResult = await QueueInvitationsAsync(meeting, newInvitations, ParseOptionalUserId(userId), cancellationToken);
            if (invitationResult.IsFailure)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                return Result<MeetingAttendeeDto>.Failure(invitationResult.Error ?? Error.Validation("Meeting invitation could not be queued."));
            }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<MeetingAttendeeDto>.Failure(Error.Conflict("The attendee could not be saved"));
        }
        await AuditAsync(meeting, "AttendeeAdded", userId, new { target.UserId, target.Email }, cancellationToken);
        var participant = await _context.Set<MeetingParticipant>().FirstOrDefaultAsync(item => item.MeetingId == meetingId && SameAttendee(item, target), cancellationToken);
        if (participant is null) return Result<MeetingAttendeeDto>.Failure(Error.Conflict("The attendee could not be saved"));
        var users = target.UserId.HasValue ? await _userLookup.GetManyAsync([target.UserId.Value], cancellationToken) : new Dictionary<Guid, UserReference>();
        return Result<MeetingAttendeeDto>.Success(ToAttendee(participant, users));
    }

    public async Task<Result> RemoveAttendeeAsync(Guid meetingId, Guid participantId, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>()).FirstOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null) return Result.Failure(Error.NotFound("Meeting not found"));
        var participant = await _context.Set<MeetingParticipant>().FirstOrDefaultAsync(item => item.MeetingId == meetingId && item.Id == participantId, cancellationToken);
        if (participant is null) return Result.Failure(Error.NotFound("Attendee not found"));
        participant.IsDeleted = true;
        participant.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync(meeting, "AttendeeRemoved", userId, new { ParticipantId = participantId }, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<MeetingMinutesDto?>> GetMinutesAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>()).AsNoTracking().FirstOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingMinutesDto?>.Failure(Error.NotFound("Meeting not found"));
        if ((meeting.Status ?? MeetingStatuses.Scheduled) != MeetingStatuses.Completed)
            return Result<MeetingMinutesDto?>.Failure(Error.NotFound("Meeting minutes are available after the meeting is completed"));
        var minutes = await _context.Set<MeetingMinutes>().AsNoTracking().Where(item => item.MeetingId == meetingId).OrderByDescending(item => item.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        return Result<MeetingMinutesDto?>.Success(minutes is null ? null : ToMinutes(minutes));
    }

    public async Task<Result<MeetingMinutesDto>> CreateMinutesAsync(Guid meetingId, CreateMeetingMinutesRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>()).FirstOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingMinutesDto>.Failure(Error.NotFound("Meeting not found"));
        if ((meeting.Status ?? MeetingStatuses.Scheduled) != MeetingStatuses.Completed)
            return Result<MeetingMinutesDto>.Failure(Error.Conflict("Meeting minutes can only be created after the meeting is completed"));

        // A completed meeting has one Minutes draft. Make initialization safe to retry and
        // serialize relational callers so two empty-state requests cannot both insert one.
        // MeetingId already has an index, which lets SQL Server take a serializable key-range lock.
        MeetingMinutes? minutes = null;
        try
        {
            await using (var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null)
            {
                var existing = await FindMinutesForMeetingAsync(meetingId, cancellationToken);
                if (existing is not null)
                {
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    return Result<MeetingMinutesDto>.Success(ToMinutes(existing));
                }

                minutes = new MeetingMinutes
                {
                    MeetingId = meetingId, TenantId = meeting.TenantId, Summary = Normalize(request.Summary), DiscussionNotes = Normalize(request.DiscussionNotes),
                    Decisions = Normalize(request.Decisions), Outcomes = Normalize(request.Outcomes), State = MeetingMinutesStates.Draft, RecordedBy = ParseOptionalUserId(userId), CreatedBy = userId
                };
                _context.Set<MeetingMinutes>().Add(minutes);
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
        }
        catch (DbUpdateException)
        {
            if (minutes is not null) _context.Entry(minutes).State = EntityState.Detached;

            // The failed transaction has been disposed before this lookup. Return a live,
            // tenant-visible record only if another request actually persisted it.
            var createdByAnotherRequest = await FindMinutesForMeetingAsync(meetingId, cancellationToken);
            if (createdByAnotherRequest is null) throw;
            return Result<MeetingMinutesDto>.Success(ToMinutes(createdByAnotherRequest));
        }
        await AuditAsync(meeting, "MinutesCreated", userId, new { minutes!.Id }, cancellationToken);
        return Result<MeetingMinutesDto>.Success(ToMinutes(minutes!));
    }

    private Task<MeetingMinutes?> FindMinutesForMeetingAsync(Guid meetingId, CancellationToken cancellationToken) =>
        _context.Set<MeetingMinutes>()
            .Where(item => item.MeetingId == meetingId)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Result<MeetingMinutesDto>> UpdateMinutesAsync(Guid meetingId, UpdateMeetingMinutesRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>()).FirstOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingMinutesDto>.Failure(Error.NotFound("Meeting not found"));
        if ((meeting.Status ?? MeetingStatuses.Scheduled) != MeetingStatuses.Completed)
            return Result<MeetingMinutesDto>.Failure(Error.Conflict("Meeting minutes can only be edited after the meeting is completed"));
        var minutes = await _context.Set<MeetingMinutes>().FirstOrDefaultAsync(item => item.MeetingId == meetingId, cancellationToken);
        if (minutes is null) return Result<MeetingMinutesDto>.Failure(Error.NotFound("Minutes not found"));
        if (minutes.State == MeetingMinutesStates.Final) return Result<MeetingMinutesDto>.Failure(Error.Conflict("Final minutes cannot be edited"));
        if (request.RowVersion is { Length: > 0 }) _context.Entry(minutes).Property(item => item.RowVersion).OriginalValue = request.RowVersion;
        ApplyMinutes(minutes, request);
        minutes.UpdatedBy = userId;
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<MeetingMinutesDto>.Failure(Error.Conflict("The minutes were changed by another user")); }
        await AuditAsync(meeting, "MinutesUpdated", userId, new { minutes.Id }, cancellationToken);
        return Result<MeetingMinutesDto>.Success(ToMinutes(minutes));
    }

    public async Task<Result<MeetingMinutesDto>> FinalizeMinutesAsync(Guid meetingId, FinalizeMeetingMinutesRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>()).FirstOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingMinutesDto>.Failure(Error.NotFound("Meeting not found"));
        if ((meeting.Status ?? MeetingStatuses.Scheduled) != MeetingStatuses.Completed)
            return Result<MeetingMinutesDto>.Failure(Error.Conflict("Meeting minutes can only be finalized after the meeting is completed"));
        var minutes = await _context.Set<MeetingMinutes>().FirstOrDefaultAsync(item => item.MeetingId == meetingId, cancellationToken);
        if (minutes is null) return Result<MeetingMinutesDto>.Failure(Error.NotFound("Minutes not found"));
        if (minutes.State == MeetingMinutesStates.Final) return Result<MeetingMinutesDto>.Success(ToMinutes(minutes));
        if (request.RowVersion is { Length: > 0 }) _context.Entry(minutes).Property(item => item.RowVersion).OriginalValue = request.RowVersion;
        minutes.State = MeetingMinutesStates.Final;
        minutes.RecordedAt = DateTime.UtcNow;
        minutes.RecordedBy = ParseOptionalUserId(userId);
        minutes.UpdatedBy = userId;
        try
        {
            await using var transaction = await BeginNotificationTransactionAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            var actorId = ParseOptionalUserId(userId);
            if (actorId is null)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                return Result<MeetingMinutesDto>.Failure(Error.Unauthorized("The meeting actor could not be resolved."));
            }
            var notificationResult = await _meetingNotifications.QueueMinutesPublishedAsync(meeting, minutes, actorId.Value, cancellationToken);
            if (notificationResult.IsFailure)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                return Result<MeetingMinutesDto>.Failure(notificationResult.Error ?? Error.Validation("Meeting minutes notifications could not be queued."));
            }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return Result<MeetingMinutesDto>.Failure(Error.Conflict("The minutes were changed by another user")); }
        await AuditAsync(meeting, "MinutesFinalized", userId, new { minutes.Id }, cancellationToken);
        return Result<MeetingMinutesDto>.Success(ToMinutes(minutes));
    }

    public async Task<Result<IReadOnlyList<MeetingMinutesCommentDto>>> GetCommentsAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        var minutes = await GetMinutesEntityAsync(meetingId, cancellationToken);
        if (minutes is null) return Result<IReadOnlyList<MeetingMinutesCommentDto>>.Failure(Error.NotFound("Meeting minutes not found"));
        var comments = await _context.Set<MeetingMinutesComment>().AsNoTracking().Where(item => item.MeetingMinutesId == minutes.Id).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var users = await _userLookup.GetManyAsync(comments.Select(item => ParseOptionalUserId(item.CreatedBy).GetValueOrDefault()), cancellationToken);
        return Result<IReadOnlyList<MeetingMinutesCommentDto>>.Success(comments.Select(item => ToComment(item, users)).ToList());
    }

    public async Task<Result<MeetingMinutesCommentDto>> AddCommentAsync(Guid meetingId, CreateMeetingMinutesCommentRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>()).FirstOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingMinutesCommentDto>.Failure(Error.NotFound("Meeting not found"));
        var minutes = await GetMinutesEntityAsync(meetingId, cancellationToken);
        if (minutes is null) return Result<MeetingMinutesCommentDto>.Failure(Error.NotFound("Meeting minutes not found"));
        var text = request.CommentText.Trim();
        if (text.Length == 0 || text.Length > 4000) return Result<MeetingMinutesCommentDto>.Failure(Error.Validation("Comment text is required and cannot exceed 4000 characters"));
        if (request.ParentCommentId.HasValue)
        {
            var parent = await _context.Set<MeetingMinutesComment>().FirstOrDefaultAsync(item => item.Id == request.ParentCommentId.Value && item.MeetingMinutesId == minutes.Id, cancellationToken);
            if (parent is null) return Result<MeetingMinutesCommentDto>.Failure(Error.NotFound("Parent comment not found"));
            if (parent.ParentCommentId.HasValue) return Result<MeetingMinutesCommentDto>.Failure(Error.Validation("Replies can only be one level deep"));
        }
        var comment = new MeetingMinutesComment { MeetingMinutesId = minutes.Id, TenantId = minutes.TenantId, ParentCommentId = request.ParentCommentId, CommentText = text, CreatedBy = userId };
        _context.Set<MeetingMinutesComment>().Add(comment);
        await using (var transaction = await BeginNotificationTransactionAsync(cancellationToken))
        {
            await _context.SaveChangesAsync(cancellationToken);
            var actorId = ParseOptionalUserId(userId);
            if (actorId is null)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                return Result<MeetingMinutesCommentDto>.Failure(Error.Unauthorized("The meeting actor could not be resolved."));
            }
            var notificationResult = await _meetingNotifications.QueueMinutesCommentAsync(meeting, minutes, comment, actorId.Value, cancellationToken);
            if (notificationResult.IsFailure)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                return Result<MeetingMinutesCommentDto>.Failure(notificationResult.Error ?? Error.Validation("Meeting comment notifications could not be queued."));
            }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        var users = await _userLookup.GetManyAsync([ParseOptionalUserId(userId).GetValueOrDefault()], cancellationToken);
        return Result<MeetingMinutesCommentDto>.Success(ToComment(comment, users));
    }

    public async Task<Result<IReadOnlyList<MeetingMinutesChecklistItemDto>>> GetChecklistAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        var minutes = await GetMinutesEntityAsync(meetingId, cancellationToken);
        if (minutes is null) return Result<IReadOnlyList<MeetingMinutesChecklistItemDto>>.Failure(Error.NotFound("Meeting minutes not found"));
        var items = await _context.Set<MeetingMinutesChecklistItem>().AsNoTracking()
            .Where(item => item.MeetingMinutesId == minutes.Id)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<MeetingMinutesChecklistItemDto>>.Success(items.Select(ToChecklistItem).ToList());
    }

    public async Task<Result<MeetingMinutesChecklistItemDto>> CreateChecklistItemAsync(Guid meetingId, CreateMeetingMinutesChecklistItemRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var minutes = await GetMinutesEntityAsync(meetingId, cancellationToken);
        if (minutes is null) return Result<MeetingMinutesChecklistItemDto>.Failure(Error.NotFound("Meeting minutes not found"));
        var titleError = ValidateChecklistTitle(request.Title, out var title);
        if (titleError is not null) return Result<MeetingMinutesChecklistItemDto>.Failure(titleError);
        var nextSortOrder = await _context.Set<MeetingMinutesChecklistItem>()
            .Where(item => item.MeetingMinutesId == minutes.Id)
            .Select(item => (int?)item.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;
        var item = new MeetingMinutesChecklistItem
        {
            TenantId = minutes.TenantId,
            MeetingMinutesId = minutes.Id,
            Title = title,
            SortOrder = nextSortOrder + 1,
            CreatedBy = userId
        };
        _context.Set<MeetingMinutesChecklistItem>().Add(item);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<MeetingMinutesChecklistItemDto>.Success(ToChecklistItem(item));
    }

    public async Task<Result<MeetingMinutesChecklistItemDto>> UpdateChecklistItemAsync(Guid meetingId, Guid itemId, UpdateMeetingMinutesChecklistItemRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var minutes = await GetMinutesEntityAsync(meetingId, cancellationToken);
        if (minutes is null) return Result<MeetingMinutesChecklistItemDto>.Failure(Error.NotFound("Meeting minutes not found"));
        var item = await _context.Set<MeetingMinutesChecklistItem>().FirstOrDefaultAsync(candidate => candidate.Id == itemId && candidate.MeetingMinutesId == minutes.Id, cancellationToken);
        if (item is null) return Result<MeetingMinutesChecklistItemDto>.Failure(Error.NotFound("Checklist item not found"));
        var titleError = ValidateChecklistTitle(request.Title, out var title);
        if (titleError is not null) return Result<MeetingMinutesChecklistItemDto>.Failure(titleError);
        item.Title = title;
        item.IsCompleted = request.IsCompleted;
        item.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        return Result<MeetingMinutesChecklistItemDto>.Success(ToChecklistItem(item));
    }

    public async Task<Result> DeleteChecklistItemAsync(Guid meetingId, Guid itemId, string userId, CancellationToken cancellationToken = default)
    {
        var minutes = await GetMinutesEntityAsync(meetingId, cancellationToken);
        if (minutes is null) return Result.Failure(Error.NotFound("Meeting minutes not found"));
        var item = await _context.Set<MeetingMinutesChecklistItem>().FirstOrDefaultAsync(candidate => candidate.Id == itemId && candidate.MeetingMinutesId == minutes.Id, cancellationToken);
        if (item is null) return Result.Failure(Error.NotFound("Checklist item not found"));
        item.IsDeleted = true;
        item.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<MeetingMinutesChecklistItemDto>>> ReorderChecklistAsync(Guid meetingId, ReorderMeetingMinutesChecklistRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var minutes = await GetMinutesEntityAsync(meetingId, cancellationToken);
        if (minutes is null) return Result<IReadOnlyList<MeetingMinutesChecklistItemDto>>.Failure(Error.NotFound("Meeting minutes not found"));
        var requestedIds = request.ItemIds ?? Array.Empty<Guid>();
        var items = await _context.Set<MeetingMinutesChecklistItem>()
            .Where(item => item.MeetingMinutesId == minutes.Id)
            .ToListAsync(cancellationToken);
        if (requestedIds.Count != items.Count || requestedIds.Distinct().Count() != requestedIds.Count || items.Any(item => !requestedIds.Contains(item.Id)))
        {
            return Result<IReadOnlyList<MeetingMinutesChecklistItemDto>>.Failure(Error.Validation("The checklist order must contain every item exactly once"));
        }
        var positions = requestedIds.Select((id, index) => (id, index)).ToDictionary(value => value.id, value => value.index);
        foreach (var item in items)
        {
            item.SortOrder = positions[item.Id];
            item.UpdatedBy = userId;
        }
        await _context.SaveChangesAsync(cancellationToken);
        return Result<IReadOnlyList<MeetingMinutesChecklistItemDto>>.Success(items.OrderBy(item => item.SortOrder).ThenBy(item => item.Id).Select(ToChecklistItem).ToList());
    }

    public async Task<Result<IReadOnlyList<MeetingMinutesAttachmentDto>>> GetAttachmentsAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        var minutes = await GetMinutesEntityAsync(meetingId, cancellationToken);
        if (minutes is null) return Result<IReadOnlyList<MeetingMinutesAttachmentDto>>.Failure(Error.NotFound("Meeting minutes not found"));
        var attachments = await _context.Set<MeetingMinutesAttachment>().AsNoTracking()
            .Where(attachment => attachment.MeetingMinutesId == minutes.Id)
            .OrderByDescending(attachment => attachment.CreatedAt)
            .ThenByDescending(attachment => attachment.Id)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<MeetingMinutesAttachmentDto>>.Success(attachments.Select(ToAttachmentDto).ToList());
    }

    public async Task<Result<IReadOnlyList<MeetingMinutesAttachmentDto>>> AddAttachmentsAsync(Guid meetingId, IReadOnlyList<IFormFile> files, string userId, CancellationToken cancellationToken = default)
    {
        var minutes = await GetMinutesEntityAsync(meetingId, cancellationToken);
        if (minutes is null) return Result<IReadOnlyList<MeetingMinutesAttachmentDto>>.Failure(Error.NotFound("Meeting minutes not found"));
        if (files.Count == 0) return Result<IReadOnlyList<MeetingMinutesAttachmentDto>>.Failure(Error.Validation("Choose at least one file"));
        if (_attachmentPolicy.AllowedContentTypes.Count == 0) return Result<IReadOnlyList<MeetingMinutesAttachmentDto>>.Failure(Error.Validation("No attachment file types are configured"));

        var validatedFiles = new List<(IFormFile File, string FileName, string ContentType)>();
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file.FileName);
            var contentType = file.ContentType?.Trim() ?? string.Empty;
            if (file.Length <= 0) return Result<IReadOnlyList<MeetingMinutesAttachmentDto>>.Failure(Error.Validation("Empty files cannot be attached"));
            if (file.Length > _attachmentPolicy.MaximumFileSizeBytes) return Result<IReadOnlyList<MeetingMinutesAttachmentDto>>.Failure(Error.Validation("One or more files exceed the configured attachment size limit"));
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255) return Result<IReadOnlyList<MeetingMinutesAttachmentDto>>.Failure(Error.Validation("Attachment file name is invalid"));
            if (!_attachmentPolicy.AllowedContentTypes.Contains(contentType)) return Result<IReadOnlyList<MeetingMinutesAttachmentDto>>.Failure(Error.Validation("One or more attachment file types are not allowed"));
            validatedFiles.Add((file, fileName, contentType));
        }

        var savedPaths = new List<string>();
        var attachments = new List<MeetingMinutesAttachment>();
        try
        {
            foreach (var entry in validatedFiles)
            {
                await using var input = entry.File.OpenReadStream();
                await using var buffer = new MemoryStream();
                await input.CopyToAsync(buffer, cancellationToken);
                var storageFileName = $"{Guid.NewGuid():N}{Path.GetExtension(entry.FileName).ToLowerInvariant()}";
                var storagePath = await _attachmentStorage.SaveAsync(storageFileName, buffer.ToArray(), $"meeting-minutes/{minutes.TenantId:N}/{minutes.Id:N}", cancellationToken);
                savedPaths.Add(storagePath);
                attachments.Add(new MeetingMinutesAttachment
                {
                    TenantId = minutes.TenantId,
                    MeetingMinutesId = minutes.Id,
                    FileName = entry.FileName,
                    ContentType = entry.ContentType,
                    FileSize = entry.File.Length,
                    StoragePath = storagePath,
                    CreatedBy = userId
                });
            }
            _context.Set<MeetingMinutesAttachment>().AddRange(attachments);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<IReadOnlyList<MeetingMinutesAttachmentDto>>.Success(attachments.Select(ToAttachmentDto).ToList());
        }
        catch
        {
            foreach (var path in savedPaths)
            {
                try { await _attachmentStorage.DeleteAsync(path, cancellationToken); } catch { }
            }
            throw;
        }
    }

    public async Task<Result<MeetingMinutesAttachmentContent>> GetAttachmentContentAsync(Guid meetingId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var minutes = await GetMinutesEntityAsync(meetingId, cancellationToken);
        if (minutes is null) return Result<MeetingMinutesAttachmentContent>.Failure(Error.NotFound("Meeting minutes not found"));
        var attachment = await _context.Set<MeetingMinutesAttachment>().FirstOrDefaultAsync(candidate => candidate.Id == attachmentId && candidate.MeetingMinutesId == minutes.Id, cancellationToken);
        if (attachment is null) return Result<MeetingMinutesAttachmentContent>.Failure(Error.NotFound("Attachment not found"));
        try
        {
            return Result<MeetingMinutesAttachmentContent>.Success(new MeetingMinutesAttachmentContent
            {
                Content = await _attachmentStorage.GetAsync(attachment.StoragePath, cancellationToken),
                FileName = attachment.FileName,
                ContentType = attachment.ContentType
            });
        }
        catch (FileNotFoundException)
        {
            return Result<MeetingMinutesAttachmentContent>.Failure(Error.NotFound("Attachment file not found"));
        }
    }

    public async Task<Result> DeleteAttachmentAsync(Guid meetingId, Guid attachmentId, string userId, CancellationToken cancellationToken = default)
    {
        var minutes = await GetMinutesEntityAsync(meetingId, cancellationToken);
        if (minutes is null) return Result.Failure(Error.NotFound("Meeting minutes not found"));
        var attachment = await _context.Set<MeetingMinutesAttachment>().FirstOrDefaultAsync(candidate => candidate.Id == attachmentId && candidate.MeetingMinutesId == minutes.Id, cancellationToken);
        if (attachment is null) return Result.Failure(Error.NotFound("Attachment not found"));
        try { await _attachmentStorage.DeleteAsync(attachment.StoragePath, cancellationToken); } catch (FileNotFoundException) { }
        attachment.IsDeleted = true;
        attachment.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<MeetingWorkItemLinkDto>>> GetWorkItemLinksAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        var minutes = await GetMinutesEntityAsync(meetingId, cancellationToken);
        if (minutes is null) return Result<IReadOnlyList<MeetingWorkItemLinkDto>>.Failure(Error.NotFound("Meeting minutes not found"));
        var links = await _context.Set<MeetingMinutesWorkItemLink>().AsNoTracking().Where(item => item.MeetingMinutesId == minutes.Id).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var workItems = await _workItemLookup.GetManyAsync(links.Select(item => item.WorkItemId), cancellationToken);
        var visibleWorkItemIds = workItems.Values
            .Where(item => item.TenantId == minutes.TenantId && IsWorkItemInScope(item))
            .Select(item => item.Id)
            .ToHashSet();
        links = links.Where(item => visibleWorkItemIds.Contains(item.WorkItemId)).ToList();
        return Result<IReadOnlyList<MeetingWorkItemLinkDto>>.Success(links.Select(item => ToLink(item, workItems)).ToList());
    }

    public async Task<Result<MeetingWorkItemLinkDto>> AddWorkItemLinkAsync(Guid meetingId, CreateMeetingWorkItemLinkRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>()).FirstOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null) return Result<MeetingWorkItemLinkDto>.Failure(Error.NotFound("Meeting not found"));
        var minutes = await GetMinutesEntityAsync(meetingId, cancellationToken);
        if (minutes is null) return Result<MeetingWorkItemLinkDto>.Failure(Error.NotFound("Meeting minutes not found"));
        var workItem = await _workItemLookup.FindAsync(request.WorkItemId, cancellationToken);
        if (workItem is null) return Result<MeetingWorkItemLinkDto>.Failure(Error.NotFound("Linkable task or issue not found"));
        if (workItem.TenantId != meeting.TenantId) return Result<MeetingWorkItemLinkDto>.Failure(Error.NotFound("Linkable task or issue not found"));
        if (!IsWorkItemInScope(workItem)) return Result<MeetingWorkItemLinkDto>.Failure(Error.NotFound("Linkable task or issue not found"));
        if (await _context.Set<MeetingMinutesWorkItemLink>().AnyAsync(item => item.MeetingMinutesId == minutes.Id && item.WorkItemId == request.WorkItemId, cancellationToken)) return Result<MeetingWorkItemLinkDto>.Failure(Error.Conflict("The work item is already linked"));
        var link = await _context.Set<MeetingMinutesWorkItemLink>().IgnoreQueryFilters().FirstOrDefaultAsync(item => item.MeetingMinutesId == minutes.Id && item.WorkItemId == request.WorkItemId && item.TenantId == minutes.TenantId, cancellationToken);
        if (link is null)
        {
            link = new MeetingMinutesWorkItemLink { MeetingMinutesId = minutes.Id, TenantId = minutes.TenantId, WorkItemId = request.WorkItemId, CreatedBy = userId };
            _context.Set<MeetingMinutesWorkItemLink>().Add(link);
        }
        link.IsDeleted = false;
        link.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync(meeting, "WorkItemLinked", userId, new { link.WorkItemId, link.MeetingMinutesId }, cancellationToken);
        return Result<MeetingWorkItemLinkDto>.Success(ToLink(link, new Dictionary<Guid, WorkItemReference> { [workItem.Id] = workItem }));
    }

    public async Task<Result> RemoveWorkItemLinkAsync(Guid meetingId, Guid workItemId, string userId, CancellationToken cancellationToken = default)
    {
        var meeting = await InScope(_context.Set<Meeting>()).FirstOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null) return Result.Failure(Error.NotFound("Meeting not found"));
        var minutes = await GetMinutesEntityAsync(meetingId, cancellationToken);
        if (minutes is null) return Result.Failure(Error.NotFound("Meeting minutes not found"));
        var link = await _context.Set<MeetingMinutesWorkItemLink>().FirstOrDefaultAsync(item => item.MeetingMinutesId == minutes.Id && item.WorkItemId == workItemId, cancellationToken);
        if (link is null) return Result.Failure(Error.NotFound("Work item link not found"));
        link.IsDeleted = true;
        link.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync(meeting, "WorkItemUnlinked", userId, new { link.WorkItemId, link.MeetingMinutesId }, cancellationToken);
        return Result.Success();
    }

    private async Task<Error?> ValidateMeetingAsync(CreateMeetingRequest request, string userId, CancellationToken cancellationToken)
    {
        if (!IsTrustedActor(userId))
            return Error.Unauthorized("A valid authenticated actor and tenant management context are required.");

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 500) return Error.Validation("Title is required and cannot exceed 500 characters");
        if (!MeetingLinkPolicy.TryNormalize(request.MeetingLink, out _)) return Error.Validation("Meeting link must be an absolute http or https URL");
        if (request.EndTime <= request.StartTime) return Error.Validation("End time must be later than start time");
        if (request.Status is not null && CanonicalStatus(request.Status) is null) return Error.Validation("Invalid meeting status");
        if (!MeetingReminderOffsets.IsSupported(request.ReminderOffsetMinutes)) return Error.Validation("Invalid meeting reminder offset");
        if (request.ProjectId.HasValue)
        {
            if (!IsProjectInScope(request.ProjectId.Value)) return Error.NotFound("Project not found");
            if (await _projectLookup.FindAsync(request.ProjectId.Value, cancellationToken) is null) return Error.NotFound("Project not found");
        }
        var organizerId = request.OrganizerId ?? ParseUserId(userId);
        var organizer = organizerId.HasValue ? await _userLookup.FindAsync(organizerId.Value, cancellationToken) : null;
        if (organizer is null) return Error.NotFound("Active organizer not found");
        if (!MeetingEmailRules.IsUsable(organizer.Email)) return Error.Validation("The selected organizer must have a usable email address");
        return null;
    }

    private async Task<Result<IReadOnlyList<ResolvedAttendee>>> ResolveAttendeesAsync(
        IReadOnlyList<MeetingAttendeeInput>? inputs,
        CancellationToken cancellationToken)
    {
        var requested = inputs ?? Array.Empty<MeetingAttendeeInput>();
        var internalIds = requested
            .Where(input => input.UserId.HasValue)
            .Select(input => input.UserId.GetValueOrDefault())
            .ToList();
        var externalEmails = requested
            .Where(input => !input.UserId.HasValue && !string.IsNullOrWhiteSpace(input.Email))
            .Select(input => MeetingEmailRules.Normalize(input.Email ?? string.Empty))
            .ToList();
        var users = await _userLookup.GetManyAsync(internalIds, cancellationToken);
        var usersByEmail = await _userLookup.FindByEmailsAsync(externalEmails, cancellationToken);
        var resolved = new List<ResolvedAttendee>(requested.Count);
        var seenUsers = new HashSet<Guid>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in requested)
        {
            var hasUserId = input.UserId.HasValue;
            var hasEmail = !string.IsNullOrWhiteSpace(input.Email);
            if (hasUserId && input.UserId.GetValueOrDefault() == Guid.Empty) return Result<IReadOnlyList<ResolvedAttendee>>.Failure(Error.Validation("Attendee UserId must be valid"));
            if (hasUserId == hasEmail) return Result<IReadOnlyList<ResolvedAttendee>>.Failure(Error.Validation("Each attendee must be an internal user or an external email"));

            if (hasUserId)
            {
                var userId = input.UserId.GetValueOrDefault();
                if (!users.TryGetValue(userId, out var user)) return Result<IReadOnlyList<ResolvedAttendee>>.Failure(Error.NotFound("One or more active attendees were not found"));
                if (!MeetingEmailRules.IsUsable(user.Email)) return Result<IReadOnlyList<ResolvedAttendee>>.Failure(Error.Validation("Every internal attendee must have a usable email address"));
                if (!seenUsers.Add(user.Id) || !seenEmails.Add(MeetingEmailRules.Normalize(user.Email))) return Result<IReadOnlyList<ResolvedAttendee>>.Failure(Error.Conflict("Duplicate attendee or email in this meeting"));
                resolved.Add(new ResolvedAttendee(user.Id, MeetingEmailRules.Normalize(user.Email), user.FullName));
                continue;
            }

            var email = MeetingEmailRules.Normalize(input.Email ?? string.Empty);
            if (!MeetingEmailRules.IsUsable(email)) return Result<IReadOnlyList<ResolvedAttendee>>.Failure(Error.Validation("Every external attendee must have a valid email address"));
            if (input.DisplayName?.Trim().Length > 200) return Result<IReadOnlyList<ResolvedAttendee>>.Failure(Error.Validation("External attendee display name cannot exceed 200 characters"));
            if (usersByEmail.TryGetValue(email, out var matchingUser))
            {
                if (!MeetingEmailRules.IsUsable(matchingUser.Email)) return Result<IReadOnlyList<ResolvedAttendee>>.Failure(Error.Validation("Every internal attendee must have a usable email address"));
                if (!seenUsers.Add(matchingUser.Id) || !seenEmails.Add(MeetingEmailRules.Normalize(matchingUser.Email))) return Result<IReadOnlyList<ResolvedAttendee>>.Failure(Error.Conflict("Duplicate attendee or email in this meeting"));
                resolved.Add(new ResolvedAttendee(matchingUser.Id, MeetingEmailRules.Normalize(matchingUser.Email), matchingUser.FullName));
                continue;
            }

            if (!seenEmails.Add(email)) return Result<IReadOnlyList<ResolvedAttendee>>.Failure(Error.Conflict("Duplicate attendee or email in this meeting"));
            resolved.Add(new ResolvedAttendee(null, email, string.IsNullOrWhiteSpace(input.DisplayName) ? null : input.DisplayName.Trim()));
        }

        return Result<IReadOnlyList<ResolvedAttendee>>.Success(resolved);
    }

    private async Task<List<ResolvedAttendee>> GetCurrentAttendeesAsync(Meeting meeting, CancellationToken cancellationToken)
    {
        var participants = await _context.Set<MeetingParticipant>()
            .Where(item => item.MeetingId == meeting.Id)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var users = await _userLookup.GetManyAsync(participants.Where(item => item.UserId.HasValue).Select(item => item.UserId.GetValueOrDefault()), cancellationToken);
        return participants.Select(item => item.UserId.HasValue && users.TryGetValue(item.UserId.Value, out var user)
            ? new ResolvedAttendee(user.Id, MeetingEmailRules.Normalize(user.Email), user.FullName)
            : new ResolvedAttendee(item.UserId, MeetingEmailRules.Normalize(item.Email), item.Name)).ToList();
    }

    private async Task<IReadOnlyList<MeetingParticipant>> SynchronizeAttendeesAsync(Meeting meeting, IReadOnlyList<ResolvedAttendee> requested, string userId, CancellationToken cancellationToken)
    {
        var existing = await _context.Set<MeetingParticipant>()
            .IgnoreQueryFilters()
            .Where(item => item.MeetingId == meeting.Id && item.TenantId == meeting.TenantId)
            .ToListAsync(cancellationToken);
        var claimed = new HashSet<Guid>();
        var newlyInvitedParticipants = new List<MeetingParticipant>();

        foreach (var attendee in requested)
        {
            var participant = existing
                .Where(item => !item.IsDeleted && !claimed.Contains(item.Id))
                .FirstOrDefault(item => SameAttendee(item, attendee));
            if (participant is null)
            {
                participant = new MeetingParticipant
                {
                    MeetingId = meeting.Id,
                    TenantId = meeting.TenantId,
                    InvitationResponseStatus = MeetingInvitationResponseStatuses.Pending,
                    CreatedBy = userId
                };
                _context.Set<MeetingParticipant>().Add(participant);
                newlyInvitedParticipants.Add(participant);
            }
            participant.UserId = attendee.UserId;
            participant.Email = attendee.Email;
            participant.Name = attendee.DisplayName;
            participant.IsDeleted = false;
            participant.UpdatedBy = userId;
            claimed.Add(participant.Id);
        }

        foreach (var participant in existing.Where(item => !item.IsDeleted && !claimed.Contains(item.Id)))
        {
            participant.IsDeleted = true;
            participant.UpdatedBy = userId;
        }

        return newlyInvitedParticipants;
    }

    private async Task<IDbContextTransaction?> BeginNotificationTransactionAsync(CancellationToken cancellationToken) =>
        _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

    private async Task<Result> QueueInvitationsAsync(
        Meeting meeting,
        IEnumerable<MeetingParticipant> invitees,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        if (actorId is null) return Result.Failure(Error.Unauthorized("The meeting actor could not be resolved."));
        foreach (var invitee in invitees.Where(participant => participant.UserId.HasValue && participant.UserId != actorId))
        {
            var queued = await _meetingNotifications.QueueInvitationAsync(meeting, invitee, actorId.Value, cancellationToken);
            if (queued.IsFailure) return queued;
        }
        return Result.Success();
    }

    private static bool SameAttendee(ResolvedAttendee left, ResolvedAttendee right) =>
        (left.UserId.HasValue && right.UserId.HasValue && left.UserId.Value == right.UserId.Value) ||
        string.Equals(left.Email, right.Email, StringComparison.OrdinalIgnoreCase);

    private static bool SameAttendee(MeetingParticipant participant, ResolvedAttendee attendee) =>
        (participant.UserId.HasValue && attendee.UserId.HasValue && participant.UserId.Value == attendee.UserId.Value) ||
        string.Equals(participant.Email?.Trim(), attendee.Email, StringComparison.OrdinalIgnoreCase);

    private bool IsProjectInScope(Guid projectId)
    {
        var context = _managementContext?.Current;
        return HasTrustedManagementContext(context) && context!.Scope.ProjectIds.Contains(projectId);
    }

    private bool IsTrustedActor(string userId)
    {
        if (!Guid.TryParse(userId, out var actorId) || actorId == Guid.Empty)
            return false;

        var context = _managementContext.Current;
        if (!HasTrustedManagementContext(context) || context!.UserId != actorId)
            return false;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return true;

        return httpContext.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var claimActorId)
            && claimActorId == actorId;
    }

    private bool HasTrustedManagementContext(NEXUS.SharedKernel.Management.ManagementContext? context)
    {
        if (context is null || context.UserId == Guid.Empty || context.TenantId == Guid.Empty)
            return false;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return true;

        return httpContext.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)
            && actorId == context.UserId
            && Guid.TryParse(httpContext.User.FindFirstValue("tenantId"), out var tenantId)
            && tenantId == context.TenantId;
    }

    private bool IsWorkItemInScope(WorkItemReference workItem) =>
        !workItem.ProjectId.HasValue || IsProjectInScope(workItem.ProjectId.Value);

    private async Task<bool> MeetingExistsAsync(Guid id, CancellationToken cancellationToken) => await InScope(_context.Set<Meeting>()).AnyAsync(item => item.Id == id, cancellationToken);

    private async Task<IReadOnlyList<MeetingListDto>> ToListDtosAsync(IReadOnlyList<MeetingListRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return Array.Empty<MeetingListDto>();

        var projectIds = rows.Where(row => row.ProjectId.HasValue).Select(row => row.ProjectId!.Value).Distinct().ToList();
        var projectById = projectIds.Count == 0
            ? new Dictionary<Guid, ProjectReference>()
            : await _projectLookup.GetReferencesAsync(projectIds, cancellationToken);
        var organizerNames = await _userLookup.GetManyAsync(rows.Where(row => row.OrganizerId.HasValue).Select(row => row.OrganizerId.GetValueOrDefault()), cancellationToken);
        return rows.Select(row =>
        {
            var project = row.ProjectId.HasValue && projectById.TryGetValue(row.ProjectId.Value, out var reference) ? reference : null;
            return new MeetingListDto
            {
                Id = row.Id,
                PublicId = row.PublicId,
                Title = row.Title,
                ProjectId = row.ProjectId,
                ProjectPublicId = project?.PublicId,
                ProjectName = project?.Name,
                ProjectCode = project?.Code,
                OrganizerId = row.OrganizerId,
                OrganizerName = row.OrganizerId.HasValue && organizerNames.TryGetValue(row.OrganizerId.Value, out var organizerName) ? organizerName.FullName : null,
                StartTime = AsUtc(row.StartTime),
                EndTime = AsUtc(row.EndTime),
                ActualStartTime = AsUtc(row.ActualStartTime),
                ActualEndTime = AsUtc(row.ActualEndTime),
                Status = row.Status,
                ReminderOffsetMinutes = row.ReminderOffsetMinutes,
                AttendeeCount = row.AttendeeCount,
                MinutesState = row.MinutesState,
                IsRecurring = row.IsRecurring
            };
        }).ToList();
    }

    private static IQueryable<Meeting> ApplyFilters(
        IQueryable<Meeting> query,
        string? search,
        string? status,
        Guid? projectId,
        IReadOnlyList<Guid>? projectIds)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(meeting => meeting.Title.Contains(term) || (meeting.Description != null && meeting.Description.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(meeting => meeting.Status == status.Trim());

        var selectedProjectIds = (projectIds ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();
        if (projectId.HasValue && projectId.Value != Guid.Empty) selectedProjectIds.Add(projectId.Value);
        if (selectedProjectIds.Count > 0)
        {
            var ids = selectedProjectIds.Distinct().ToArray();
            query = query.Where(meeting => meeting.ProjectId.HasValue && ids.Contains(meeting.ProjectId.Value));
        }

        return query;
    }

    private IQueryable<Meeting> InScope(IQueryable<Meeting> query)
    {
        var context = _managementContext?.Current;
        if (!HasTrustedManagementContext(context)) return query.Where(_ => false);

        var userId = context!.UserId;
        var projectIds = context.Scope.ProjectIds;
        return query.Where(meeting =>
            (meeting.ProjectId.HasValue && projectIds.Contains(meeting.ProjectId.Value)) ||
            meeting.OrganizerId == userId ||
            meeting.Participants.Any(participant => participant.UserId == userId));
    }

    private async Task DeactivatePendingRemindersAsync(Guid meetingId, string userId, CancellationToken cancellationToken)
    {
        var pending = await _context.Set<MeetingReminderDispatch>()
            .Where(dispatch => dispatch.MeetingId == meetingId && dispatch.Status == MeetingReminderDispatchStatuses.Pending)
            .ToListAsync(cancellationToken);
        foreach (var dispatch in pending)
        {
            dispatch.IsDeleted = true;
            dispatch.UpdatedBy = userId;
        }
    }

    private async Task<MeetingMinutes?> GetMinutesEntityAsync(Guid meetingId, CancellationToken cancellationToken)
    {
        var meeting = await InScope(_context.Set<Meeting>()).FirstOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null || (meeting.Status ?? MeetingStatuses.Scheduled) != MeetingStatuses.Completed) return null;
        return await _context.Set<MeetingMinutes>().FirstOrDefaultAsync(item => item.MeetingId == meetingId, cancellationToken);
    }

    private async Task AuditAsync(Meeting meeting, string action, string userId, object changes, CancellationToken cancellationToken)
    {
        await _auditService.LogAsync(new AuditEntry
        {
            TenantId = meeting.TenantId,
            EntityType = nameof(Meeting),
            EntityId = meeting.Id,
            Action = action,
            Changes = JsonSerializer.Serialize(changes),
            UserId = ParseOptionalUserId(userId),
            UserName = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);
    }

    private Guid GetCurrentTenantId() => Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("tenantId"), out var tenantId) && tenantId != Guid.Empty
        ? tenantId
        : throw new InvalidOperationException("A trusted tenant context is required.");
    private static Guid? ParseUserId(string userId) => Guid.TryParse(userId, out var value) ? value : null;
    private static Guid? ParseOptionalUserId(string userId) => ParseUserId(userId);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTime? AsUtc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static Error? ValidateChecklistTitle(string? value, out string title)
    {
        title = value?.Trim() ?? string.Empty;
        if (title.Length == 0) return Error.Validation("Checklist item title is required");
        if (title.Length > 500) return Error.Validation("Checklist item title cannot exceed 500 characters");
        return null;
    }
    private static string? CanonicalStatus(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "scheduled" => MeetingStatuses.Scheduled,
        "inprogress" or "in progress" => MeetingStatuses.InProgress,
        "completed" => MeetingStatuses.Completed,
        "cancelled" or "canceled" => MeetingStatuses.Cancelled,
        _ => null
    };
    private static void ApplyMinutes(MeetingMinutes minutes, CreateMeetingMinutesRequest request)
    {
        minutes.Summary = Normalize(request.Summary);
        minutes.DiscussionNotes = Normalize(request.DiscussionNotes);
        minutes.Decisions = Normalize(request.Decisions);
        minutes.Outcomes = Normalize(request.Outcomes);
    }
    private static MeetingAttendeeDto ToAttendee(MeetingParticipant participant, IReadOnlyDictionary<Guid, UserReference> users)
    {
        var hasInternalUser = participant.UserId.HasValue;
        var user = hasInternalUser && users.TryGetValue(participant.UserId.GetValueOrDefault(), out var reference) ? reference : null;
        return new MeetingAttendeeDto
        {
            Id = participant.Id,
            MeetingId = participant.MeetingId,
            UserId = participant.UserId,
            DisplayName = user?.FullName ?? participant.Name,
            Email = user?.Email ?? participant.Email,
            IsInternalUser = hasInternalUser
        };
    }
    private static MeetingMinutesDto ToMinutes(MeetingMinutes minutes) => new()
    {
        Id = minutes.Id, MeetingId = minutes.MeetingId, Summary = minutes.Summary, DiscussionNotes = minutes.DiscussionNotes,
        Decisions = minutes.Decisions, Outcomes = minutes.Outcomes, State = minutes.State, RecordedBy = minutes.RecordedBy,
        RecordedAt = minutes.RecordedAt, RowVersion = minutes.RowVersion
    };
    private static MeetingMinutesCommentDto ToComment(MeetingMinutesComment comment, IReadOnlyDictionary<Guid, UserReference> users) => new()
    {
        Id = comment.Id, PublicId = comment.PublicId, MeetingMinutesId = comment.MeetingMinutesId, ParentCommentId = comment.ParentCommentId,
        CommentText = comment.CommentText, CreatedBy = comment.CreatedBy, AuthorName = ParseOptionalUserId(comment.CreatedBy) is { } id && users.TryGetValue(id, out var user) ? user.FullName : null, CreatedAt = comment.CreatedAt
    };
    private static MeetingMinutesChecklistItemDto ToChecklistItem(MeetingMinutesChecklistItem item) => new()
    {
        Id = item.Id,
        MeetingMinutesId = item.MeetingMinutesId,
        Title = item.Title,
        IsCompleted = item.IsCompleted,
        SortOrder = item.SortOrder
    };
    private static MeetingMinutesAttachmentDto ToAttachmentDto(MeetingMinutesAttachment attachment) => new()
    {
        Id = attachment.Id,
        MeetingMinutesId = attachment.MeetingMinutesId,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        FileSize = attachment.FileSize,
        CreatedAt = attachment.CreatedAt
    };
    private static MeetingWorkItemLinkDto ToLink(MeetingMinutesWorkItemLink link, IReadOnlyDictionary<Guid, WorkItemReference> workItems) => new()
    {
        Id = link.Id, MeetingMinutesId = link.MeetingMinutesId, WorkItemId = link.WorkItemId,
        WorkItemTitle = workItems.TryGetValue(link.WorkItemId, out var item) ? item.Title : null,
        WorkItemType = workItems.TryGetValue(link.WorkItemId, out item) ? item.Type : null,
        WorkItemStatus = workItems.TryGetValue(link.WorkItemId, out item) ? item.Status : null,
        ProjectId = workItems.TryGetValue(link.WorkItemId, out item) ? item.ProjectId : null
    };

    private sealed record MeetingListRow(Guid Id, Guid PublicId, string Title, Guid? ProjectId, Guid? OrganizerId, DateTime StartTime, DateTime EndTime, DateTime? ActualStartTime, DateTime? ActualEndTime, string? Status, int? ReminderOffsetMinutes, int AttendeeCount, string? MinutesState, bool IsRecurring);
    private sealed record ResolvedAttendee(Guid? UserId, string Email, string? DisplayName);
}
