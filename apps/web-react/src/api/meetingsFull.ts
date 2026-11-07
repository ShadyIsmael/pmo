import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

type PagedResult<T> = { items: T[]; totalCount: number; page?: number; pageSize?: number; totalPages?: number };
export interface MeetingListDto { id: string; publicId?: string | null; title: string; projectId?: string | null; projectPublicId?: string | null; projectName?: string | null; projectCode?: string | null; organizerId?: string | null; organizerName?: string | null; startTime: string; endTime: string; actualStartTime?: string | null; actualEndTime?: string | null; status?: string | null; attendeeCount: number; minutesState?: "Draft" | "Final" | null; reminderOffsetMinutes?: number | null; isRecurring: boolean; }
export interface MeetingCalendarDto { from: string; to: string; totalCount: number; isTruncated: boolean; items: MeetingListDto[]; }
export interface MeetingProjectOptionDto { id: string; publicId: string; name: string; code?: string | null; }
export interface MeetingAttendeeDto { id: string; meetingId: string; userId?: string | null; displayName?: string | null; email: string; isInternalUser: boolean; }
export interface MeetingDetailDto extends MeetingListDto { objective?: string | null; location?: string | null; meetingLink?: string | null; attendees: readonly MeetingAttendeeDto[]; rowVersion: string; seriesId?: string | null; occurrenceKey?: string | null; }
export interface MeetingRecurrenceRule { frequency: "Daily" | "EveryTwoDays" | "Weekly" | "Monthly"; startDate: string; endDate: string; localStartTime: string; durationMinutes: number; timeZoneId: string; weekdays: number[]; }
export interface MeetingRecurrenceOccurrencePreview { occurrenceKey: string; startTime: string; endTime: string; timeZoneId: string; }
export interface MeetingRecurrencePreviewDto { occurrences: MeetingRecurrenceOccurrencePreview[]; totalOccurrenceCount: number; skippedDaylightSavingCount: number; timeZoneId: string; }
export interface MeetingSeriesDto { id: string; rule: MeetingRecurrenceRule; templateTitle: string; occurrences: { id: string; occurrenceKey: string; title: string; startTime: string; endTime: string; status?: string | null; isSeriesAnchor: boolean }[]; }
export interface MeetingMinutesDto { id: string; meetingId: string; summary?: string | null; discussionNotes?: string | null; decisions?: string | null; outcomes?: string | null; state: "Draft" | "Final"; recordedBy?: string | null; recordedAt: string; rowVersion: string; }
export interface ChecklistItemDto { id: string; meetingMinutesId: string; title: string; isCompleted: boolean; sortOrder: number; }
export interface MeetingAttachmentDto { id: string; meetingMinutesId: string; fileName: string; contentType: string; fileSize: number; createdAt: string; }
export interface MeetingCommentDto { id: string; publicId?: string | null; meetingMinutesId: string; parentCommentId?: string | null; commentText: string; createdBy: string; authorName?: string | null; createdAt: string; }
export interface MeetingAgendaItemDto { id: string; meetingId: string; topic: string; description?: string | null; sortOrder: number; durationMinutes?: number | null; ownerId?: string | null; ownerName?: string | null; rowVersion: string; }
export type MeetingInvitationResponseStatus = "Pending" | "Accepted" | "Declined" | "Tentative";
export type MeetingAttendanceStatus = "Present" | "Late" | "Excused" | "Absent";
export interface MeetingParticipantStatusDto { id: string; meetingId: string; userId?: string | null; displayName?: string | null; email: string; isInternalUser: boolean; attendanceStatus: MeetingInvitationResponseStatus | MeetingAttendanceStatus | "Attended"; invitationResponseStatus?: MeetingInvitationResponseStatus | null; actualAttendanceStatus?: MeetingAttendanceStatus | null; role?: string | null; respondedAt?: string | null; rowVersion: string; }
export interface MeetingDecisionDto { decisions?: string | null; isEditable: boolean; rowVersion: string; }
export interface MeetingActionItemDto { id: string; meetingId: string; title: string; description?: string | null; assigneeId?: string | null; assigneeName?: string | null; dueDate?: string | null; completedDate?: string | null; status: "Pending" | "Completed"; workItemId?: string | null; workItemTitle?: string | null; agendaItemId?: string | null; agendaItemTopic?: string | null; rowVersion: string; }
export interface MeetingSharingRecipientDto { userId: string; displayName: string; email: string; isAttendeeShortcut: boolean; attendanceStatus?: string | null; canReceiveMinutesEmail: boolean; canReceiveCompletionNotification: boolean; minutesEmailPreferenceEnabled: boolean; minutesEmailProviderAvailable: boolean; }
export interface MeetingSharingRecipientsResponse { attendees: MeetingSharingRecipientDto[]; tenantUsers: MeetingSharingRecipientDto[]; applicationLink: string; }
export interface MeetingSharingHistoryEntryDto { id: string; type: "MeetingMinutesShared" | "MeetingCompleted"; senderName: string; sentAt: string; recipientCount: number; status: "Pending" | "Sent" | "Failed" | "Partial"; recipients: MeetingSharingHistoryRecipientDto[]; }
export interface MeetingSharingHistoryRecipientDto { userId: string; recipientName: string; recipientEmail?: string | null; status: "Pending" | "Sent" | "Failed" | "Partial"; deliveries: { channel: "Email" | "InApp" | "Push"; status: "Pending" | "Sent" | "Failed" }[]; }
export interface MeetingSharingOperationResult { queuedCount: number; alreadyProcessedCount: number; }
export interface MeetingInput { title: string; objective?: string; projectId?: string; startTime: string; endTime: string; location?: string; meetingLink?: string; reminderOffsetMinutes?: number | null; organizerId: string; attendees: readonly { userId?: string; email?: string; displayName?: string }[]; agendaItems?: readonly { topic: string; description?: string; durationMinutes?: number; ownerId?: string }[]; }

export const meetingKeys = { all: ["meetings"] as const, lists: () => ["meetings", "list"] as const, list: (params: Record<string, unknown>) => [...meetingKeys.lists(), params] as const, calendars: () => ["meetings", "calendar"] as const, calendar: (params: Record<string, unknown>) => [...meetingKeys.calendars(), params] as const, projectOptions: () => ["meetings", "projects"] as const, detail: (publicId: string) => ["meetings", "detail", "public-id", publicId] as const, minutes: (id: string) => ["meetings", "minutes", id] as const, checklist: (id: string) => ["meetings", "minutes", id, "checklist"] as const, attachments: (id: string) => ["meetings", "minutes", id, "attachments"] as const, comments: (id: string) => ["meetings", "minutes", id, "comments"] as const, agenda: (id: string) => ["meetings", "agenda", id] as const, participants: (id: string) => ["meetings", "participants", id] as const, actions: (id: string) => ["meetings", "actions", id] as const, decisions: (id: string) => ["meetings", "decisions", id] as const, sharing: (id: string) => ["meetings", "sharing", id] as const };
export const meetingsApiFull = {
  list: (params: Record<string, string | number | boolean | undefined>) => api.get<PagedResult<MeetingListDto>>(endpoints.meetings.root, params),
  calendar: (params: Record<string, string | number | boolean | undefined>) => api.get<MeetingCalendarDto>(endpoints.meetings.calendar, params),
  projectOptions: (params: Record<string, string | number | boolean | undefined> = {}) => api.get<MeetingProjectOptionDto[]>(endpoints.meetings.projects, params),
  detail: (publicId: string) => api.get<MeetingDetailDto>(endpoints.meetings.detail(publicId)),
  create: (body: MeetingInput) => api.post<MeetingDetailDto>(endpoints.meetings.root, body),
  createSeries: (body: { requestId: string; template: MeetingInput; rule: MeetingRecurrenceRule }) => api.post<MeetingDetailDto>(`${endpoints.meetings.root}/series`, body),
  recurrencePreview: (rule: MeetingRecurrenceRule) => api.post<MeetingRecurrencePreviewDto>(endpoints.meetings.recurrencePreview, rule),
  series: (id: string) => api.get<MeetingSeriesDto>(endpoints.meetings.series(id)),
  update: (id: string, body: MeetingInput & { rowVersion?: string; status?: string }) => api.put<MeetingDetailDto>(endpoints.meetings.detail(id), body),
  start: (id: string, rowVersion?: string) => api.post<MeetingDetailDto>(`${endpoints.meetings.detail(id)}/start`, rowVersion ? { rowVersion } : {}),
  end: (id: string, rowVersion?: string) => api.post<MeetingDetailDto>(`${endpoints.meetings.detail(id)}/end`, rowVersion ? { rowVersion } : {}),
  delete: (id: string) => api.delete<void>(endpoints.meetings.detail(id)),
  minutes: (id: string) => api.get<MeetingMinutesDto | null>(endpoints.meetings.minutes(id)),
  createMinutes: (id: string, body: Record<string, string | undefined>) => api.post<MeetingMinutesDto>(endpoints.meetings.minutes(id), body),
  updateMinutes: (id: string, body: Record<string, string | undefined>) => api.put<MeetingMinutesDto>(endpoints.meetings.minutes(id), body),
  finalizeMinutes: (id: string, rowVersion?: string) => api.post<MeetingMinutesDto>(endpoints.meetings.finalizeMinutes(id), rowVersion ? { rowVersion } : {}),
  checklist: (id: string) => api.get<ChecklistItemDto[]>(endpoints.meetings.checklist(id)),
  createChecklist: (id: string, title: string) => api.post<ChecklistItemDto>(endpoints.meetings.checklist(id), { title }),
  updateChecklist: (meetingId: string, itemId: string, body: { title: string; isCompleted: boolean }) => api.put<ChecklistItemDto>(endpoints.meetings.checklistItem(meetingId, itemId), body),
  deleteChecklist: (meetingId: string, itemId: string) => api.delete<void>(endpoints.meetings.checklistItem(meetingId, itemId)),
  reorderChecklist: (id: string, itemIds: string[]) => api.put<ChecklistItemDto[]>(endpoints.meetings.checklistReorder(id), { itemIds }),
  attachments: (id: string) => api.get<MeetingAttachmentDto[]>(endpoints.meetings.attachments(id)),
  uploadAttachments: (id: string, files: File[]) => { const form = new FormData(); files.forEach((file) => form.append("files", file, file.name)); return api.upload<MeetingAttachmentDto[]>(endpoints.meetings.attachments(id), form); },
  downloadAttachment: (meetingId: string, attachmentId: string) => api.download<Blob>(endpoints.meetings.attachmentContent(meetingId, attachmentId)),
  deleteAttachment: (meetingId: string, attachmentId: string) => api.delete<void>(endpoints.meetings.attachment(meetingId, attachmentId)),
  comments: (id: string) => api.get<MeetingCommentDto[]>(endpoints.meetings.comments(id)),
  sharingRecipients: (id: string, search?: string) => api.get<MeetingSharingRecipientsResponse>(endpoints.meetings.sharingRecipients(id), { search }),
  sharingHistory: (id: string) => api.get<MeetingSharingHistoryEntryDto[]>(endpoints.meetings.sharingHistory(id)),
  shareMinutesByEmail: (id: string, body: { recipientUserIds: string[]; minutesId: string; minutesRowVersion: string; requestId: string; locale: "en" | "ar" }) => api.post<MeetingSharingOperationResult>(endpoints.meetings.shareMinutesByEmail(id), body),
  notifyMeetingCompletion: (id: string, body: { recipientUserIds: string[]; requestId: string; locale: "en" | "ar" }) => api.post<MeetingSharingOperationResult>(endpoints.meetings.notifyMeetingCompletion(id), body),
  addComment: (id: string, body: { commentText: string; parentCommentId?: string }) => api.post<MeetingCommentDto>(endpoints.meetings.comments(id), body),
  agenda: (id: string) => api.get<MeetingAgendaItemDto[]>(`${endpoints.meetings.detail(id)}/agenda`),
  createAgendaItem: (id: string, body: { topic: string; description?: string; durationMinutes?: number }) => api.post<MeetingAgendaItemDto>(`${endpoints.meetings.detail(id)}/agenda`, body),
  updateAgendaItem: (meetingId: string, itemId: string, body: { topic: string; description?: string; durationMinutes?: number | null; rowVersion?: string }) => api.put<MeetingAgendaItemDto>(`${endpoints.meetings.detail(meetingId)}/agenda/${encodeURIComponent(itemId)}`, body),
  deleteAgendaItem: (meetingId: string, itemId: string) => api.delete<void>(`${endpoints.meetings.detail(meetingId)}/agenda/${encodeURIComponent(itemId)}`),
  reorderAgenda: (id: string, itemIds: string[]) => api.put<MeetingAgendaItemDto[]>(`${endpoints.meetings.detail(id)}/agenda/reorder`, { itemIds }),
  participants: (id: string) => api.get<MeetingParticipantStatusDto[]>(`${endpoints.meetings.detail(id)}/participants`),
  updateMyAttendance: (id: string, attendanceStatus: MeetingInvitationResponseStatus, rowVersion?: string) => api.put<MeetingParticipantStatusDto>(`${endpoints.meetings.detail(id)}/attendance`, { attendanceStatus, rowVersion }),
  updateAttendance: (meetingId: string, participantId: string, actualAttendanceStatus: MeetingAttendanceStatus, rowVersion?: string) => api.put<MeetingParticipantStatusDto>(`${endpoints.meetings.detail(meetingId)}/participants/${encodeURIComponent(participantId)}/attendance`, { actualAttendanceStatus, rowVersion }),
  updateOrganizerAttendance: (meetingId: string, participantId: string, actualAttendanceStatus: MeetingAttendanceStatus, rowVersion?: string) => api.put<MeetingParticipantStatusDto>(`${endpoints.meetings.detail(meetingId)}/participants/${encodeURIComponent(participantId)}/attendance/organizer`, { actualAttendanceStatus, rowVersion }),
  decisions: (id: string) => api.get<MeetingDecisionDto>(`${endpoints.meetings.detail(id)}/decisions`),
  saveDecisions: (id: string, body: { decisions?: string; rowVersion?: string }) => api.put<MeetingDecisionDto>(`${endpoints.meetings.detail(id)}/decisions`, body),
  actions: (id: string) => api.get<MeetingActionItemDto[]>(`${endpoints.meetings.detail(id)}/actions`),
  createActionItem: (id: string, body: { title: string; description?: string; assigneeId?: string; dueDate?: string; agendaItemId?: string }) => api.post<MeetingActionItemDto>(`${endpoints.meetings.detail(id)}/actions`, body),
  updateActionItem: (meetingId: string, itemId: string, body: { title: string; description?: string; assigneeId?: string; dueDate?: string; agendaItemId?: string; status: MeetingActionItemDto["status"]; rowVersion?: string }) => api.put<MeetingActionItemDto>(`${endpoints.meetings.detail(meetingId)}/actions/${encodeURIComponent(itemId)}`, body),
  linkActionWorkItem: (meetingId: string, itemId: string, workItemId: string) => api.post<MeetingActionItemDto>(`${endpoints.meetings.detail(meetingId)}/actions/${encodeURIComponent(itemId)}/work-item`, { workItemId }),
  unlinkActionWorkItem: (meetingId: string, itemId: string) => api.delete<void>(`${endpoints.meetings.detail(meetingId)}/actions/${encodeURIComponent(itemId)}/work-item`),
  deleteActionItem: (meetingId: string, itemId: string) => api.delete<void>(`${endpoints.meetings.detail(meetingId)}/actions/${encodeURIComponent(itemId)}`),
};

export function useFullMeetings(params: Record<string, string | number | boolean | undefined> = {}, enabled = true) { return useQuery({ queryKey: meetingKeys.list(params), queryFn: () => meetingsApiFull.list(params), enabled, staleTime: 30_000 }); }
export function useMeetingCalendar(params: Record<string, string | number | boolean | undefined> = {}, enabled = true) { return useQuery({ queryKey: meetingKeys.calendar(params), queryFn: () => meetingsApiFull.calendar(params), enabled, staleTime: 30_000 }); }
export function useMeetingRecurrencePreview(rule: MeetingRecurrenceRule | null, enabled = true) {
  return useQuery({
    queryKey: [...meetingKeys.all, "recurrence-preview", rule],
    queryFn: () => meetingsApiFull.recurrencePreview(rule!),
    enabled: Boolean(rule) && enabled,
    staleTime: 30_000,
  });
}
export function useMeetingSeries(id?: string, enabled = true) {
  return useQuery({
    queryKey: [...meetingKeys.all, "series", id ?? ""],
    queryFn: () => meetingsApiFull.series(id!),
    enabled: Boolean(id) && enabled,
    staleTime: 30_000,
  });
}
export function useMeetingProjectOptions(params: Record<string, string | number | boolean | undefined> = {}, enabled = true) { return useQuery({ queryKey: meetingKeys.projectOptions(), queryFn: () => meetingsApiFull.projectOptions(params), enabled, staleTime: 5 * 60_000 }); }
export function useFullMeeting(publicId?: string, enabled = true) { return useQuery({ queryKey: meetingKeys.detail(publicId ?? ""), queryFn: () => meetingsApiFull.detail(publicId!), enabled: Boolean(publicId) && enabled, staleTime: 30_000 }); }
export function useMeetingMinutes(id?: string, enabled = true) { return useQuery({ queryKey: meetingKeys.minutes(id ?? ""), queryFn: () => meetingsApiFull.minutes(id!), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useMeetingMinutesChecklist(id?: string, enabled = true) { return useQuery({ queryKey: meetingKeys.checklist(id ?? ""), queryFn: () => meetingsApiFull.checklist(id!), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useMeetingMinutesAttachments(id?: string, enabled = true) { return useQuery({ queryKey: meetingKeys.attachments(id ?? ""), queryFn: () => meetingsApiFull.attachments(id!), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useMeetingMinutesComments(id?: string, enabled = true) { return useQuery({ queryKey: meetingKeys.comments(id ?? ""), queryFn: () => meetingsApiFull.comments(id!), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useMeetingAgenda(id?: string, enabled = true) { return useQuery({ queryKey: meetingKeys.agenda(id ?? ""), queryFn: () => meetingsApiFull.agenda(id!), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useMeetingParticipants(id?: string, enabled = true) { return useQuery({ queryKey: meetingKeys.participants(id ?? ""), queryFn: () => meetingsApiFull.participants(id!), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useMeetingActionItems(id?: string, enabled = true) { return useQuery({ queryKey: meetingKeys.actions(id ?? ""), queryFn: () => meetingsApiFull.actions(id!), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useMeetingDecisions(id?: string, enabled = true) { return useQuery({ queryKey: meetingKeys.decisions(id ?? ""), queryFn: () => meetingsApiFull.decisions(id!), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useMeetingFullMutations(id?: string, detailPublicId?: string) {
  const client = useQueryClient();
  const invalidateLists = async () => {
    await Promise.all([
      client.invalidateQueries({ queryKey: meetingKeys.lists() }),
      client.invalidateQueries({ queryKey: meetingKeys.calendars() }),
    ]);
  };
  const refreshDetail = async (meeting: MeetingDetailDto) => {
    if (!detailPublicId) return;
    client.setQueryData(meetingKeys.detail(detailPublicId), meeting);
    await client.invalidateQueries({ queryKey: meetingKeys.detail(detailPublicId), exact: true });
  };
  return {
    create: useMutation({ mutationFn: meetingsApiFull.create, onSuccess: invalidateLists }),
    createSeries: useMutation({ mutationFn: meetingsApiFull.createSeries, onSuccess: invalidateLists }),
    update: useMutation({ mutationFn: ({ body }: { body: MeetingInput & { rowVersion?: string; status?: string } }) => meetingsApiFull.update(id!, body), onSuccess: async (meeting) => { await Promise.all([invalidateLists(), refreshDetail(meeting)]); } }),
    start: useMutation({ mutationFn: (rowVersion?: string) => meetingsApiFull.start(id!, rowVersion), onSuccess: async (meeting) => { await Promise.all([invalidateLists(), refreshDetail(meeting)]); } }),
    end: useMutation({ mutationFn: (rowVersion?: string) => meetingsApiFull.end(id!, rowVersion), onSuccess: async (meeting) => {
      // The end response is the authoritative Completed record. Publish it immediately so
      // the completed workspace can open under either a PublicId or legacy internal-ID URL.
      await Promise.all([
        invalidateLists(),
        refreshDetail(meeting),
        client.invalidateQueries({ queryKey: meetingKeys.minutes(meeting.id), exact: true }),
      ]);
    } }),
    delete: useMutation({ mutationFn: meetingsApiFull.delete, onSuccess: async () => { await invalidateLists(); if (detailPublicId) client.removeQueries({ queryKey: meetingKeys.detail(detailPublicId), exact: true }); } }),
    createMinutes: useMutation({ mutationFn: (body: Record<string, string | undefined>) => meetingsApiFull.createMinutes(id!, body), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.minutes(id ?? "") }) }),
    updateMinutes: useMutation({ mutationFn: (body: Record<string, string | undefined>) => meetingsApiFull.updateMinutes(id!, body), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.minutes(id ?? "") }) }),
    finalizeMinutes: useMutation({ mutationFn: (rowVersion?: string) => meetingsApiFull.finalizeMinutes(id!, rowVersion), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.minutes(id ?? "") }) }),
    createChecklist: useMutation({ mutationFn: (title: string) => meetingsApiFull.createChecklist(id!, title), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.checklist(id ?? "") }) }),
    updateChecklist: useMutation({ mutationFn: ({ itemId, body }: { itemId: string; body: { title: string; isCompleted: boolean } }) => meetingsApiFull.updateChecklist(id!, itemId, body), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.checklist(id ?? "") }) }),
    deleteChecklist: useMutation({ mutationFn: (itemId: string) => meetingsApiFull.deleteChecklist(id!, itemId), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.checklist(id ?? "") }) }),
    reorderChecklist: useMutation({ mutationFn: (itemIds: string[]) => meetingsApiFull.reorderChecklist(id!, itemIds), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.checklist(id ?? "") }) }),
    uploadAttachments: useMutation({ mutationFn: (files: File[]) => meetingsApiFull.uploadAttachments(id!, files), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.attachments(id ?? "") }) }),
    deleteAttachment: useMutation({ mutationFn: (attachmentId: string) => meetingsApiFull.deleteAttachment(id!, attachmentId), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.attachments(id ?? "") }) }),
    addComment: useMutation({ mutationFn: (body: { commentText: string; parentCommentId?: string }) => meetingsApiFull.addComment(id!, body), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.comments(id ?? "") }) }),
  };
}

export function useMeetingSharingRecipients(id?: string, search = "", enabled = true) {
  return useQuery({
    queryKey: [...meetingKeys.sharing(id ?? ""), "recipients", search],
    queryFn: () => meetingsApiFull.sharingRecipients(id!, search),
    enabled: Boolean(id) && enabled,
    staleTime: 30_000,
  });
}

export function useMeetingSharingHistory(id?: string, enabled = true) {
  return useQuery({
    queryKey: [...meetingKeys.sharing(id ?? ""), "history"],
    queryFn: () => meetingsApiFull.sharingHistory(id!),
    enabled: Boolean(id) && enabled,
    staleTime: 10_000,
  });
}

export function useMeetingSharingMutations(id?: string) {
  const client = useQueryClient();
  const historyKey = [...meetingKeys.sharing(id ?? ""), "history"];
  return {
    shareMinutesByEmail: useMutation({ mutationFn: (body: { recipientUserIds: string[]; minutesId: string; minutesRowVersion: string; requestId: string; locale: "en" | "ar" }) => meetingsApiFull.shareMinutesByEmail(id!, body), onSuccess: () => client.invalidateQueries({ queryKey: historyKey }) }),
    notifyMeetingCompletion: useMutation({ mutationFn: (body: { recipientUserIds: string[]; requestId: string; locale: "en" | "ar" }) => meetingsApiFull.notifyMeetingCompletion(id!, body), onSuccess: () => client.invalidateQueries({ queryKey: historyKey }) }),
  };
}

export function useMeetingWorkspaceMutations(id?: string) {
  const client = useQueryClient();
  const invalidate = async (key: ReturnType<typeof meetingKeys.agenda> | ReturnType<typeof meetingKeys.participants> | ReturnType<typeof meetingKeys.actions> | ReturnType<typeof meetingKeys.decisions>) => client.invalidateQueries({ queryKey: key });
  return {
    createAgendaItem: useMutation({ mutationFn: (body: { topic: string; description?: string; durationMinutes?: number }) => meetingsApiFull.createAgendaItem(id!, body), onSuccess: () => invalidate(meetingKeys.agenda(id ?? "")) }),
    updateAgendaItem: useMutation({ mutationFn: ({ itemId, body }: { itemId: string; body: { topic: string; description?: string; durationMinutes?: number | null; rowVersion?: string } }) => meetingsApiFull.updateAgendaItem(id!, itemId, body), onSuccess: () => invalidate(meetingKeys.agenda(id ?? "")) }),
    deleteAgendaItem: useMutation({ mutationFn: (itemId: string) => meetingsApiFull.deleteAgendaItem(id!, itemId), onSuccess: () => invalidate(meetingKeys.agenda(id ?? "")) }),
    reorderAgenda: useMutation({ mutationFn: (itemIds: string[]) => meetingsApiFull.reorderAgenda(id!, itemIds), onSuccess: () => invalidate(meetingKeys.agenda(id ?? "")) }),
    updateMyAttendance: useMutation({ mutationFn: ({ status, rowVersion }: { status: MeetingInvitationResponseStatus; rowVersion?: string }) => meetingsApiFull.updateMyAttendance(id!, status, rowVersion), onSuccess: () => invalidate(meetingKeys.participants(id ?? "")) }),
    updateAttendance: useMutation({ mutationFn: ({ participantId, status, rowVersion }: { participantId: string; status: MeetingAttendanceStatus; rowVersion?: string }) => meetingsApiFull.updateAttendance(id!, participantId, status, rowVersion), onSuccess: () => invalidate(meetingKeys.participants(id ?? "")) }),
    updateOrganizerAttendance: useMutation({ mutationFn: ({ participantId, status, rowVersion }: { participantId: string; status: MeetingAttendanceStatus; rowVersion?: string }) => meetingsApiFull.updateOrganizerAttendance(id!, participantId, status, rowVersion), onSuccess: () => invalidate(meetingKeys.participants(id ?? "")) }),
    saveDecisions: useMutation({ mutationFn: (body: { decisions?: string; rowVersion?: string }) => meetingsApiFull.saveDecisions(id!, body), onSuccess: () => invalidate(meetingKeys.decisions(id ?? "")) }),
    createActionItem: useMutation({ mutationFn: (body: { title: string; description?: string; assigneeId?: string; dueDate?: string; agendaItemId?: string }) => meetingsApiFull.createActionItem(id!, body), onSuccess: () => invalidate(meetingKeys.actions(id ?? "")) }),
    updateActionItem: useMutation({ mutationFn: ({ itemId, body }: { itemId: string; body: { title: string; description?: string; assigneeId?: string; dueDate?: string; agendaItemId?: string; status: MeetingActionItemDto["status"]; rowVersion?: string } }) => meetingsApiFull.updateActionItem(id!, itemId, body), onSuccess: () => invalidate(meetingKeys.actions(id ?? "")) }),
    linkActionWorkItem: useMutation({ mutationFn: ({ itemId, workItemId }: { itemId: string; workItemId: string }) => meetingsApiFull.linkActionWorkItem(id!, itemId, workItemId), onSuccess: () => invalidate(meetingKeys.actions(id ?? "")) }),
    unlinkActionWorkItem: useMutation({ mutationFn: (itemId: string) => meetingsApiFull.unlinkActionWorkItem(id!, itemId), onSuccess: () => invalidate(meetingKeys.actions(id ?? "")) }),
    deleteActionItem: useMutation({ mutationFn: (itemId: string) => meetingsApiFull.deleteActionItem(id!, itemId), onSuccess: () => invalidate(meetingKeys.actions(id ?? "")) }),
  };
}
